package pricefeed

import (
	"context"
	"errors"
	"fmt"
)

type Feed struct {
	provider  PriceProvider
	publisher Publisher
}

func NewFeed(provider PriceProvider, publisher Publisher) *Feed {
	return &Feed{
		provider:  provider,
		publisher: publisher,
	}
}

func (f *Feed) Run(ctx context.Context) error {
	runCtx, cancel := context.WithCancel(ctx)
	defer cancel()

	updates, err := f.provider.Stream(runCtx)
	if err != nil {
		return err
	}

	for {
		select {
		case <-runCtx.Done():
			return nil
		case update, ok := <-updates:
			if !ok {
				return nil
			}

			if err := f.publisher.Publish(runCtx, update); err != nil {
				if errors.Is(err, context.Canceled) {
					return nil
				}

				return fmt.Errorf("publish price update: %w", err)
			}
		}
	}
}
