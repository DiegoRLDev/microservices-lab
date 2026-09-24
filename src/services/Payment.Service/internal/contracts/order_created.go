package contracts

import (
	"encoding/json"
	"fmt"
	"time"
)

const OrderCreatedEventType = "OrderCreated"

type EventEnvelope[TData any] struct {
	EventID       string    `json:"eventId"`
	EventType     string    `json:"eventType"`
	OccurredAt    time.Time `json:"occurredAt"`
	CorrelationID string    `json:"correlationId"`
	Data          TData     `json:"data"`
}

type OrderCreatedData struct {
	OrderID    string    `json:"orderId"`
	ProductID  string    `json:"productId"`
	Quantity   int       `json:"quantity"`
	UnitPrice  float64   `json:"unitPrice"`
	TotalPrice float64   `json:"totalPrice"`
	Status     string    `json:"status"`
	CreatedAt  time.Time `json:"createdAt"`
}

type OrderCreatedEvent = EventEnvelope[OrderCreatedData]

func DecodeOrderCreated(payload []byte) (OrderCreatedEvent, error) {
	var event OrderCreatedEvent
	if err := json.Unmarshal(payload, &event); err != nil {
		return OrderCreatedEvent{}, fmt.Errorf("decode order created event: %w", err)
	}

	if event.EventType != OrderCreatedEventType {
		return OrderCreatedEvent{}, fmt.Errorf("unexpected event type %q", event.EventType)
	}

	if event.EventID == "" {
		return OrderCreatedEvent{}, fmt.Errorf("eventId is required")
	}

	if event.Data.OrderID == "" {
		return OrderCreatedEvent{}, fmt.Errorf("data.orderId is required")
	}

	return event, nil
}
