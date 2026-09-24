package processing

import (
	"context"
	"log/slog"
	"strings"
	"time"

	"github.com/google/uuid"
	"go.opentelemetry.io/otel/attribute"
	"go.opentelemetry.io/otel/trace"

	"microserviceslab/payment-service/internal/contracts"
	"microserviceslab/payment-service/internal/observability"
)

type PaymentStatus string

const (
	PaymentApproved PaymentStatus = "Approved"
	PaymentRejected PaymentStatus = "Rejected"
)

type PaymentDecision struct {
	OrderID     string
	ProductID   string
	EventID     string
	PaymentID   string
	Amount      float64
	Status      PaymentStatus
	Reason      string
	ProcessedAt time.Time
}

type PaymentProcessor struct {
	maxAutoApprovalAmount float64
	logger                *slog.Logger
}

func NewPaymentProcessor(maxAutoApprovalAmount float64, logger *slog.Logger) *PaymentProcessor {
	return &PaymentProcessor{
		maxAutoApprovalAmount: maxAutoApprovalAmount,
		logger:                logger,
	}
}

func (p *PaymentProcessor) Process(ctx context.Context, event contracts.OrderCreatedEvent) (PaymentDecision, error) {
	ctx, span := observability.Tracer.Start(
		ctx,
		"process payment",
		trace.WithSpanKind(trace.SpanKindInternal),
		trace.WithAttributes(
			attribute.String("order.id", event.Data.OrderID),
			attribute.String("product.id", event.Data.ProductID),
			attribute.Float64("payment.amount", event.Data.TotalPrice),
		),
	)
	defer span.End()

	select {
	case <-ctx.Done():
		return PaymentDecision{}, ctx.Err()
	default:
	}

	decision := p.decide(event)

	attrs := append(observability.TraceLogAttrs(ctx),
		slog.String("eventId", decision.EventID),
		slog.String("orderId", decision.OrderID),
		slog.String("productId", decision.ProductID),
		slog.Float64("amount", decision.Amount),
		slog.String("status", string(decision.Status)),
		slog.String("reason", decision.Reason),
	)

	p.logger.Info(
		"processed OrderCreated payment simulation",
		attrs...,
	)

	return decision, nil
}

func (p *PaymentProcessor) decide(event contracts.OrderCreatedEvent) PaymentDecision {
	status := PaymentApproved
	reason := "amount within automatic approval limit"

	if event.Data.TotalPrice <= 0 {
		status = PaymentRejected
		reason = "total price must be greater than zero"
	} else if event.Data.TotalPrice > p.maxAutoApprovalAmount {
		status = PaymentRejected
		reason = "amount exceeds automatic approval limit"
	}

	return PaymentDecision{
		OrderID:     event.Data.OrderID,
		ProductID:   event.Data.ProductID,
		EventID:     event.EventID,
		PaymentID:   newID(),
		Amount:      event.Data.TotalPrice,
		Status:      status,
		Reason:      reason,
		ProcessedAt: time.Now().UTC(),
	}
}

func (d PaymentDecision) ToIntegrationEvent(correlationID string) contracts.PaymentEvent {
	eventType := contracts.PaymentApprovedEventType
	if d.Status == PaymentRejected {
		eventType = contracts.PaymentRejectedEventType
	}

	return contracts.PaymentEvent{
		EventID:       newID(),
		EventType:     eventType,
		OccurredAt:    time.Now().UTC(),
		CorrelationID: correlationID,
		Data: contracts.PaymentEventData{
			PaymentID:   d.PaymentID,
			OrderID:     d.OrderID,
			Amount:      d.Amount,
			Status:      string(d.Status),
			Reason:      d.Reason,
			ProcessedAt: d.ProcessedAt,
		},
	}
}

func newID() string {
	return strings.ToLower(uuid.NewString())
}
