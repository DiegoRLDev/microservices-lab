package main

import (
	"context"
	"log/slog"
	"os"
	"os/signal"
	"syscall"
	"time"

	"microserviceslab/payment-service/internal/config"
	"microserviceslab/payment-service/internal/kafka"
	"microserviceslab/payment-service/internal/observability"
	"microserviceslab/payment-service/internal/processing"
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

	shutdownObservability, err := observability.Init(ctx, logger)
	if err != nil {
		logger.Error("failed to initialize OpenTelemetry", slog.String("error", err.Error()))
		os.Exit(1)
	}
	defer func() {
		shutdownCtx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
		defer cancel()
		if err := shutdownObservability(shutdownCtx); err != nil {
			logger.Error("failed to shutdown OpenTelemetry", slog.String("error", err.Error()))
		}
	}()

	processor := processing.NewPaymentProcessor(cfg.Payment.MaxAutoApprovalAmount, logger)
	publisher := kafka.NewKafkaPaymentEventPublisher(cfg.Kafka, logger)
	consumer := kafka.NewOrderCreatedConsumer(cfg.Kafka, processor, publisher, logger)

	logger.Info(
		"starting Payment.Service",
		slog.Any("brokers", cfg.Kafka.Brokers),
		slog.String("topic", cfg.Kafka.OrderCreatedTopic),
		slog.String("paymentApprovedTopic", cfg.Kafka.PaymentApprovedTopic),
		slog.String("paymentRejectedTopic", cfg.Kafka.PaymentRejectedTopic),
		slog.String("groupId", cfg.Kafka.GroupID),
	)

	if err := consumer.Run(ctx); err != nil {
		logger.Error("Payment.Service stopped with error", slog.String("error", err.Error()))
		os.Exit(1)
	}

	logger.Info("Payment.Service stopped")
}
