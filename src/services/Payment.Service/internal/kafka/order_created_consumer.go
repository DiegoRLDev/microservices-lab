package kafka

import (
	"context"
	"errors"
	"log/slog"

	"github.com/segmentio/kafka-go"
	"go.opentelemetry.io/otel/attribute"
	"go.opentelemetry.io/otel/codes"
	"go.opentelemetry.io/otel/trace"

	"microserviceslab/payment-service/internal/config"
	"microserviceslab/payment-service/internal/contracts"
	"microserviceslab/payment-service/internal/observability"
	"microserviceslab/payment-service/internal/processing"
)

type OrderCreatedConsumer struct {
	reader    *kafka.Reader
	processor *processing.PaymentProcessor
	publisher PaymentEventPublisher
	logger    *slog.Logger
}

func NewOrderCreatedConsumer(
	cfg config.KafkaConfig,
	processor *processing.PaymentProcessor,
	publisher PaymentEventPublisher,
	logger *slog.Logger) *OrderCreatedConsumer {
	reader := kafka.NewReader(kafka.ReaderConfig{
		Brokers:  cfg.Brokers,
		Topic:    cfg.OrderCreatedTopic,
		GroupID:  cfg.GroupID,
		MinBytes: 1,
		MaxBytes: 10e6,
	})

	return &OrderCreatedConsumer{
		reader:    reader,
		processor: processor,
		publisher: publisher,
		logger:    logger,
	}
}

func (c *OrderCreatedConsumer) Run(ctx context.Context) error {
	defer func() {
		if err := c.reader.Close(); err != nil {
			c.logger.Error("failed to close Kafka reader", slog.String("error", err.Error()))
		}
		if err := c.publisher.Close(); err != nil {
			c.logger.Error("failed to close Kafka payment event publisher", slog.String("error", err.Error()))
		}
	}()

	for {
		message, err := c.reader.FetchMessage(ctx)
		if err != nil {
			if errors.Is(err, context.Canceled) {
				return nil
			}

			return err
		}

		if err := c.handleMessage(ctx, message); err != nil {
			return err
		}

		if err := c.reader.CommitMessages(ctx, message); err != nil {
			return err
		}
	}
}

func (c *OrderCreatedConsumer) handleMessage(ctx context.Context, message kafka.Message) error {
	ctx = observability.ExtractKafkaHeaders(ctx, message.Headers)
	ctx, span := observability.Tracer.Start(
		ctx,
		"Kafka consume order-created",
		trace.WithSpanKind(trace.SpanKindConsumer),
		trace.WithAttributes(
			attribute.String("messaging.system", "kafka"),
			attribute.String("messaging.destination.name", message.Topic),
			attribute.String("messaging.operation", "consume"),
			attribute.String("messaging.kafka.message.key", string(message.Key)),
			attribute.Int("messaging.kafka.partition", message.Partition),
			attribute.Int64("messaging.kafka.offset", message.Offset),
		),
	)
	defer span.End()

	event, err := contracts.DecodeOrderCreated(message.Value)
	if err != nil {
		span.RecordError(err)
		span.SetStatus(codes.Error, err.Error())
		c.logger.Error(
			"failed to decode OrderCreated message",
			slog.String("error", err.Error()),
			slog.String("key", string(message.Key)),
			slog.Int("partition", message.Partition),
			slog.Int64("offset", message.Offset),
		)
		return err
	}

	if key := string(message.Key); key != event.Data.OrderID {
		attrs := append(observability.TraceLogAttrs(ctx),
			slog.String("key", key),
			slog.String("orderId", event.Data.OrderID),
			slog.String("eventId", event.EventID),
		)
		c.logger.Warn(
			"Kafka message key differs from OrderId",
			attrs...,
		)
	} else {
		attrs := append(observability.TraceLogAttrs(ctx),
			slog.String("key", key),
			slog.String("orderId", event.Data.OrderID),
			slog.String("eventId", event.EventID),
			slog.Int("partition", message.Partition),
			slog.Int64("offset", message.Offset),
		)
		c.logger.Info(
			"received OrderCreated message",
			attrs...,
		)
	}

	decision, err := c.processor.Process(ctx, event)
	if err != nil {
		span.RecordError(err)
		span.SetStatus(codes.Error, err.Error())
		return err
	}

	paymentEvent := decision.ToIntegrationEvent(event.CorrelationID)
	return c.publisher.Publish(ctx, paymentEvent)
}
