package kafka

import (
	"context"
	"encoding/json"
	"log/slog"

	"github.com/segmentio/kafka-go"
	"go.opentelemetry.io/otel/attribute"
	"go.opentelemetry.io/otel/codes"
	"go.opentelemetry.io/otel/trace"

	"microserviceslab/payment-service/internal/config"
	"microserviceslab/payment-service/internal/contracts"
	"microserviceslab/payment-service/internal/observability"
)

type PaymentEventPublisher interface {
	Publish(ctx context.Context, event contracts.PaymentEvent) error
	Close() error
}

type KafkaPaymentEventPublisher struct {
	writer        *kafka.Writer
	approvedTopic string
	rejectedTopic string
	logger        *slog.Logger
}

func NewKafkaPaymentEventPublisher(cfg config.KafkaConfig, logger *slog.Logger) *KafkaPaymentEventPublisher {
	writer := &kafka.Writer{
		Addr:                   kafka.TCP(cfg.Brokers...),
		Balancer:               &kafka.Hash{},
		AllowAutoTopicCreation: false,
		RequiredAcks:           kafka.RequireAll,
	}

	return &KafkaPaymentEventPublisher{
		writer:        writer,
		approvedTopic: cfg.PaymentApprovedTopic,
		rejectedTopic: cfg.PaymentRejectedTopic,
		logger:        logger,
	}
}

func (p *KafkaPaymentEventPublisher) Publish(ctx context.Context, event contracts.PaymentEvent) error {
	payload, err := json.Marshal(event)
	if err != nil {
		return err
	}

	topic := p.approvedTopic
	if event.EventType == contracts.PaymentRejectedEventType {
		topic = p.rejectedTopic
	}

	message := kafka.Message{
		Topic: topic,
		Key:   []byte(event.Data.OrderID),
		Value: payload,
		Headers: []kafka.Header{
			{Key: "event-id", Value: []byte(event.EventID)},
			{Key: "event-type", Value: []byte(event.EventType)},
			{Key: "correlation-id", Value: []byte(event.CorrelationID)},
		},
	}

	ctx, span := observability.Tracer.Start(
		ctx,
		"Kafka produce "+topic,
		trace.WithSpanKind(trace.SpanKindProducer),
		trace.WithAttributes(
			attribute.String("messaging.system", "kafka"),
			attribute.String("messaging.destination.name", topic),
			attribute.String("messaging.operation", "publish"),
			attribute.String("messaging.kafka.message.key", event.Data.OrderID),
			attribute.String("messaging.message.id", event.EventID),
			attribute.String("event.type", event.EventType),
		),
	)
	defer span.End()

	message.Headers = observability.InjectKafkaHeaders(ctx, message.Headers)

	if err := p.writer.WriteMessages(ctx, message); err != nil {
		span.RecordError(err)
		span.SetStatus(codes.Error, err.Error())
		return err
	}

	observability.RecordPayment(ctx, event.EventType)

	attrs := append(observability.TraceLogAttrs(ctx),
		slog.String("eventId", event.EventID),
		slog.String("eventType", event.EventType),
		slog.String("orderId", event.Data.OrderID),
		slog.String("paymentId", event.Data.PaymentID),
		slog.String("topic", topic),
	)

	p.logger.Info(
		"published payment event",
		attrs...,
	)

	return nil
}

func (p *KafkaPaymentEventPublisher) Close() error {
	return p.writer.Close()
}
