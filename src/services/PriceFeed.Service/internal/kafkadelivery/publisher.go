package kafkadelivery

import (
	"context"
	"encoding/json"
	"log/slog"
	"time"

	"github.com/segmentio/kafka-go"
	"microserviceslab/pricefeed-service/internal/pricefeed"
)

const PriceUpdatedEventType = "PriceUpdated"

type messageWriter interface {
	WriteMessages(ctx context.Context, messages ...kafka.Message) error
	Close() error
}

type PriceUpdatedEvent struct {
	Symbol     string    `json:"symbol"`
	Price      int64     `json:"price"`
	OccurredAt time.Time `json:"occurredAt"`
}

type KafkaPublisher struct {
	writer messageWriter
	topic  string
	logger *slog.Logger
}

func NewKafkaPublisher(brokers []string, topic string, logger *slog.Logger) *KafkaPublisher {
	return NewKafkaPublisherWithWriter(
		&kafka.Writer{
			Addr:                   kafka.TCP(brokers...),
			Balancer:               &kafka.Hash{},
			AllowAutoTopicCreation: false,
			RequiredAcks:           kafka.RequireAll,
			Async:                  true,
			BatchTimeout:           10 * time.Millisecond,
			WriteTimeout:           2 * time.Second,
			Completion: func(messages []kafka.Message, err error) {
				if err == nil {
					return
				}

				for _, message := range messages {
					logger.Error(
						"failed to publish price update to Kafka",
						slog.String("error", err.Error()),
						slog.String("topic", message.Topic),
						slog.String("symbol", string(message.Key)),
					)
				}
			},
		},
		topic,
		logger,
	)
}

func NewKafkaPublisherWithWriter(writer messageWriter, topic string, logger *slog.Logger) *KafkaPublisher {
	return &KafkaPublisher{
		writer: writer,
		topic:  topic,
		logger: logger,
	}
}

func (p *KafkaPublisher) Publish(ctx context.Context, update pricefeed.PriceUpdate) error {
	if err := ctx.Err(); err != nil {
		return err
	}

	payload, err := SerializePriceUpdated(update)
	if err != nil {
		return err
	}

	message := kafka.Message{
		Topic: p.topic,
		Key:   []byte(update.Symbol),
		Value: payload,
		Headers: []kafka.Header{
			{Key: "event-type", Value: []byte(PriceUpdatedEventType)},
			{Key: "symbol", Value: []byte(update.Symbol)},
		},
	}

	if err := p.writer.WriteMessages(ctx, message); err != nil {
		if ctxErr := ctx.Err(); ctxErr != nil {
			return ctxErr
		}

		p.logger.Error(
			"failed to publish price update to Kafka",
			slog.String("error", err.Error()),
			slog.String("topic", p.topic),
			slog.String("symbol", update.Symbol),
		)

		return nil
	}

	return nil
}

func (p *KafkaPublisher) Close() error {
	return p.writer.Close()
}

func SerializePriceUpdated(update pricefeed.PriceUpdate) ([]byte, error) {
	return json.Marshal(PriceUpdatedEvent{
		Symbol:     update.Symbol,
		Price:      update.PriceCents,
		OccurredAt: update.OccurredAt,
	})
}
