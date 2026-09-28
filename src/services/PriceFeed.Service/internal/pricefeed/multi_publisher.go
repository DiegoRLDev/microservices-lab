package pricefeed

import (
	"context"
	"fmt"
)

type MultiPublisher struct {
	publishers []Publisher
}

func NewMultiPublisher(publishers ...Publisher) *MultiPublisher {
	return &MultiPublisher{publishers: publishers}
}

func (p *MultiPublisher) Publish(ctx context.Context, update PriceUpdate) error {
	for _, publisher := range p.publishers {
		if err := publisher.Publish(ctx, update); err != nil {
			return fmt.Errorf("publish to %T: %w", publisher, err)
		}
	}

	return nil
}
