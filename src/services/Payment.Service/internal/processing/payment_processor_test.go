package processing

import (
	"context"
	"io"
	"log/slog"
	"testing"

	"microserviceslab/payment-service/internal/contracts"
)

func TestPaymentProcessorApprovesOrderWithinLimit(t *testing.T) {
	processor := NewPaymentProcessor(10000, slog.New(slog.NewTextHandler(io.Discard, nil)))

	decision, err := processor.Process(context.Background(), contracts.OrderCreatedEvent{
		EventID: "event-1",
		Data: contracts.OrderCreatedData{
			OrderID:    "order-1",
			ProductID:  "product-1",
			TotalPrice: 5500,
		},
	})
	if err != nil {
		t.Fatalf("Process returned error: %v", err)
	}

	if decision.Status != PaymentApproved {
		t.Fatalf("unexpected status: %s", decision.Status)
	}

	event := decision.ToIntegrationEvent("correlation-1")
	if event.EventType != contracts.PaymentApprovedEventType {
		t.Fatalf("unexpected event type: %s", event.EventType)
	}

	if event.Data.OrderID != "order-1" {
		t.Fatalf("unexpected order id: %s", event.Data.OrderID)
	}
}

func TestPaymentProcessorRejectsOrderAboveLimit(t *testing.T) {
	processor := NewPaymentProcessor(100, slog.New(slog.NewTextHandler(io.Discard, nil)))

	decision, err := processor.Process(context.Background(), contracts.OrderCreatedEvent{
		EventID: "event-1",
		Data: contracts.OrderCreatedData{
			OrderID:    "order-1",
			ProductID:  "product-1",
			TotalPrice: 150,
		},
	})
	if err != nil {
		t.Fatalf("Process returned error: %v", err)
	}

	if decision.Status != PaymentRejected {
		t.Fatalf("unexpected status: %s", decision.Status)
	}

	event := decision.ToIntegrationEvent("correlation-1")
	if event.EventType != contracts.PaymentRejectedEventType {
		t.Fatalf("unexpected event type: %s", event.EventType)
	}
}
