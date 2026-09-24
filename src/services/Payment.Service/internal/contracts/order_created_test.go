package contracts

import "testing"

func TestDecodeOrderCreated(t *testing.T) {
	payload := []byte(`{
		"eventId":"71340ca0-cde8-413a-8833-ecbd9cec7fe3",
		"eventType":"OrderCreated",
		"occurredAt":"2026-09-01T19:31:40.6164926+00:00",
		"correlationId":"5a3f55aeef30c4d7b1929a70f7fbeaca",
		"data":{
			"orderId":"93c03028-fa50-451c-94d6-def231caf1e3",
			"productId":"3eb208f9-41fc-42f8-961a-1412b0ec4541",
			"quantity":1,
			"unitPrice":5500.00,
			"totalPrice":5500.00,
			"status":"Pending",
			"createdAt":"2026-09-01T19:31:40.3505856+00:00"
		}
	}`)

	event, err := DecodeOrderCreated(payload)
	if err != nil {
		t.Fatalf("DecodeOrderCreated returned error: %v", err)
	}

	if event.EventType != OrderCreatedEventType {
		t.Fatalf("unexpected event type: %s", event.EventType)
	}

	if event.Data.OrderID != "93c03028-fa50-451c-94d6-def231caf1e3" {
		t.Fatalf("unexpected order id: %s", event.Data.OrderID)
	}

	if event.Data.TotalPrice != 5500 {
		t.Fatalf("unexpected total price: %f", event.Data.TotalPrice)
	}
}

func TestDecodeOrderCreatedRejectsUnexpectedEventType(t *testing.T) {
	payload := []byte(`{
		"eventId":"71340ca0-cde8-413a-8833-ecbd9cec7fe3",
		"eventType":"PaymentApproved",
		"occurredAt":"2026-09-01T19:31:40.6164926+00:00",
		"correlationId":"5a3f55aeef30c4d7b1929a70f7fbeaca",
		"data":{"orderId":"93c03028-fa50-451c-94d6-def231caf1e3"}
	}`)

	if _, err := DecodeOrderCreated(payload); err == nil {
		t.Fatal("expected error for unexpected event type")
	}
}
