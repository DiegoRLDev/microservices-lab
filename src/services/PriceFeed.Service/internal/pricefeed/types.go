package pricefeed

import (
	"context"
	"time"
)

type PriceUpdate struct {
	Symbol     string
	PriceCents int64
	OccurredAt time.Time
}

type PriceProvider interface {
	Stream(ctx context.Context) (<-chan PriceUpdate, error)
}

type Publisher interface {
	Publish(ctx context.Context, update PriceUpdate) error
}
