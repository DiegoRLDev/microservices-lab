package kafkadelivery

import (
	"context"
	"encoding/json"
	"errors"
	"io"
	"log/slog"
	"testing"
	"time"

	"github.com/segmentio/kafka-go"
	"microserviceslab/pricefeed-service/internal/pricefeed"
)

func TestSerializePriceUpdatedUsesSimpleEventShape(t *testing.T) {
	update := pricefeed.PriceUpdate{
		Symbol:     "PETR4",
		PriceCents: 3815,
		OccurredAt: time.Date(2026, 9, 29, 14, 30, 0, 0, time.UTC),
	}

	payload, err := SerializePriceUpdated(update)
	if err != nil {
		t.Fatalf("SerializePriceUpdated() error = %v", err)
	}

	var got PriceUpdatedEvent
	if err := json.Unmarshal(payload, &got); err != nil {
		t.Fatalf("json.Unmarshal() error = %v", err)
	}

	if got.Symbol != update.Symbol {
		t.Fatalf("Symbol = %q, want %q", got.Symbol, update.Symbol)
	}

	if got.Price != update.PriceCents {
		t.Fatalf("Price = %d, want %d", got.Price, update.PriceCents)
	}

	if !got.OccurredAt.Equal(update.OccurredAt) {
		t.Fatalf("OccurredAt = %s, want %s", got.OccurredAt, update.OccurredAt)
	}
}

func TestKafkaPublisherUsesSymbolAsMessageKey(t *testing.T) {
	writer := &recordingWriter{}
	publisher := NewKafkaPublisherWithWriter(writer, "price-updated", discardLogger())
	update := pricefeed.PriceUpdate{
		Symbol:     "VALE3",
		PriceCents: 7421,
		OccurredAt: time.Date(2026, 9, 29, 14, 30, 0, 0, time.UTC),
	}

	if err := publisher.Publish(context.Background(), update); err != nil {
		t.Fatalf("Publish() error = %v", err)
	}

	if len(writer.messages) != 1 {
		t.Fatalf("len(messages) = %d, want 1", len(writer.messages))
	}

	message := writer.messages[0]
	if message.Topic != "price-updated" {
		t.Fatalf("Topic = %q, want price-updated", message.Topic)
	}

	if string(message.Key) != update.Symbol {
		t.Fatalf("Key = %q, want %q", string(message.Key), update.Symbol)
	}

	var event PriceUpdatedEvent
	if err := json.Unmarshal(message.Value, &event); err != nil {
		t.Fatalf("json.Unmarshal() error = %v", err)
	}

	if event.Symbol != update.Symbol || event.Price != update.PriceCents {
		t.Fatalf("event = %#v, want symbol %s and price %d", event, update.Symbol, update.PriceCents)
	}
}

func TestKafkaPublisherReturnsNilForKafkaWriteError(t *testing.T) {
	writer := &recordingWriter{writeErr: errors.New("broker unavailable")}
	publisher := NewKafkaPublisherWithWriter(writer, "price-updated", discardLogger())

	err := publisher.Publish(context.Background(), pricefeed.PriceUpdate{
		Symbol:     "ITUB4",
		PriceCents: 4295,
		OccurredAt: time.Date(2026, 9, 29, 14, 30, 0, 0, time.UTC),
	})
	if err != nil {
		t.Fatalf("Publish() error = %v, want nil", err)
	}
}

func TestKafkaPublisherReturnsContextErrorWhenCanceled(t *testing.T) {
	ctx, cancel := context.WithCancel(context.Background())
	cancel()

	writer := &recordingWriter{}
	publisher := NewKafkaPublisherWithWriter(writer, "price-updated", discardLogger())

	err := publisher.Publish(ctx, pricefeed.PriceUpdate{
		Symbol:     "WEGE3",
		PriceCents: 3980,
		OccurredAt: time.Date(2026, 9, 29, 14, 30, 0, 0, time.UTC),
	})
	if !errors.Is(err, context.Canceled) {
		t.Fatalf("Publish() error = %v, want context.Canceled", err)
	}
}

type recordingWriter struct {
	messages []kafka.Message
	writeErr error
	closed   bool
}

func (w *recordingWriter) WriteMessages(ctx context.Context, messages ...kafka.Message) error {
	if w.writeErr != nil {
		return w.writeErr
	}

	w.messages = append(w.messages, messages...)
	return nil
}

func (w *recordingWriter) Close() error {
	w.closed = true
	return nil
}

func discardLogger() *slog.Logger {
	return slog.New(slog.NewTextHandler(io.Discard, nil))
}
