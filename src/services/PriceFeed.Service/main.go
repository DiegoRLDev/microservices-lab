package main

import (
	"context"
	"errors"
	"log/slog"
	"os"
	"os/signal"
	"syscall"

	"microserviceslab/pricefeed-service/internal/config"
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

	feed := pricefeed.NewFeed(provider, pricefeed.NewConsolePublisher(os.Stdout))

	logger.Info(
		"starting PriceFeed.Service",
		slog.String("provider", cfg.PriceFeed.Provider),
		slog.Any("symbols", cfg.PriceFeed.Symbols),
		slog.Duration("updateInterval", cfg.PriceFeed.UpdateInterval),
		slog.Int("maxStepCents", cfg.PriceFeed.MaxStepCents),
	)

	if err := feed.Run(ctx); err != nil && !errors.Is(err, context.Canceled) {
		logger.Error("PriceFeed.Service stopped with error", slog.String("error", err.Error()))
		os.Exit(1)
	}

	logger.Info("PriceFeed.Service stopped")
}
