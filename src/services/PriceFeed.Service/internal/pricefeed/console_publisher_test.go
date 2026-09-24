package pricefeed

import (
	"bytes"
	"context"
	"testing"
	"time"
)

func TestConsolePublisherWritesFormattedUpdate(t *testing.T) {
	var output bytes.Buffer
	publisher := NewConsolePublisher(&output)

	update := PriceUpdate{
		Symbol:     "PETR4",
		PriceCents: 3810,
		OccurredAt: time.Date(2026, 9, 24, 14, 30, 1, 102000000, time.UTC),
	}

	if err := publisher.Publish(context.Background(), update); err != nil {
		t.Fatalf("Publish() error = %v", err)
	}

	want := "14:30:01.102 PETR4 38.10\n"
	if output.String() != want {
		t.Fatalf("output = %q, want %q", output.String(), want)
	}
}
