package main

import (
	"context"
	"errors"
	"log/slog"
	"net"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"google.golang.org/grpc"
	"microserviceslab/pricefeed-service/internal/config"
	"microserviceslab/pricefeed-service/internal/grpcdelivery"
	"microserviceslab/pricefeed-service/internal/grpcdelivery/pricefeedpb"
	"microserviceslab/pricefeed-service/internal/httpdelivery"
	"microserviceslab/pricefeed-service/internal/pricefeed"
)

func main() {
	logger := slog.New(slog.NewTextHandler(os.Stdout, &slog.HandlerOptions{
		Level: slog.LevelInfo,
	}))

	cfg, err := config.Load()
	if err != nil {
		logger.Error("invalid configuration", slog.String("error", err.Error()))
		os.Exit(1)
	}

	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()

	provider, err := pricefeed.NewMockPriceProvider(pricefeed.MockPriceProviderOptions{
		Symbols:       cfg.PriceFeed.Symbols,
		InitialPrices: cfg.PriceFeed.InitialPrices,
		Interval:      cfg.PriceFeed.UpdateInterval,
		MaxStepCents:  cfg.PriceFeed.MaxStepCents,
	})
	if err != nil {
		logger.Error("failed to create price provider", slog.String("error", err.Error()))
		os.Exit(1)
	}

	broadcaster := pricefeed.NewBroadcaster(pricefeed.DefaultSubscriberBuffer)
	publisher := pricefeed.NewMultiPublisher(
		pricefeed.NewConsolePublisher(os.Stdout),
		broadcaster,
	)
	feed := pricefeed.NewFeed(provider, publisher)
	httpDelivery := httpdelivery.NewServer(broadcaster, logger)
	httpServer := &http.Server{
		Addr:    cfg.HTTP.Addr,
		Handler: httpDelivery.Handler(),
		BaseContext: func(listener net.Listener) context.Context {
			return ctx
		},
	}
	grpcServer := grpc.NewServer()
	pricefeedpb.RegisterPriceFeedServer(grpcServer, grpcdelivery.NewPriceFeedServer(broadcaster))

	errs := make(chan error, 3)

	logger.Info(
		"starting PriceFeed.Service",
		slog.String("provider", cfg.PriceFeed.Provider),
		slog.Any("symbols", cfg.PriceFeed.Symbols),
		slog.Duration("updateInterval", cfg.PriceFeed.UpdateInterval),
		slog.Int("maxStepCents", cfg.PriceFeed.MaxStepCents),
		slog.String("httpAddr", cfg.HTTP.Addr),
		slog.String("grpcAddr", cfg.GRPC.Addr),
	)

	go func() {
		errs <- feed.Run(ctx)
	}()

	go func() {
		err := httpServer.ListenAndServe()
		if errors.Is(err, http.ErrServerClosed) {
			err = nil
		}
		errs <- err
	}()

	go func() {
		listener, err := net.Listen("tcp", cfg.GRPC.Addr)
		if err != nil {
			errs <- err
			return
		}

		err = grpcServer.Serve(listener)
		if errors.Is(err, grpc.ErrServerStopped) {
			err = nil
		}
		errs <- err
	}()

	var exitCode int
	select {
	case <-ctx.Done():
	case err := <-errs:
		if err != nil && !errors.Is(err, context.Canceled) {
			logger.Error("PriceFeed.Service stopped with error", slog.String("error", err.Error()))
			exitCode = 1
		}
		stop()
	}

	shutdownCtx, cancelShutdown := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancelShutdown()

	if err := httpdelivery.Shutdown(shutdownCtx, httpServer); err != nil {
		logger.Error("HTTP server shutdown failed", slog.String("error", err.Error()))
		exitCode = 1
	}
	grpcdelivery.Shutdown(shutdownCtx, grpcServer.Stop, grpcServer.GracefulStop)

	logger.Info("PriceFeed.Service stopped")

	if exitCode != 0 {
		os.Exit(exitCode)
	}
}
