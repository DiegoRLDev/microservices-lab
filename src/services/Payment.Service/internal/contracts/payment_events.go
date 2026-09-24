package contracts

import "time"

const (
	PaymentApprovedEventType = "PaymentApproved"
	PaymentRejectedEventType = "PaymentRejected"
)

type PaymentEventData struct {
	PaymentID   string    `json:"paymentId"`
	OrderID     string    `json:"orderId"`
	Amount      float64   `json:"amount"`
	Status      string    `json:"status"`
	Reason      string    `json:"reason"`
	ProcessedAt time.Time `json:"processedAt"`
}

type PaymentEvent = EventEnvelope[PaymentEventData]
