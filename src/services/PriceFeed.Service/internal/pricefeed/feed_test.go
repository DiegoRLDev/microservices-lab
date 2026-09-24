package pricefeed

import (
	"context"
	"errors"
	"testing"
	"time"
)

func TestFeedPublishesProviderUpdates(t *testing.T) {
	provider := &stubProvider{
		updates: []PriceUpdate{
			{Symbol: "PETR4", PriceCents: 3810, OccurredAt: time.Now().UTC()},
			{Symbol: "VALE3", PriceCents: 7421, OccurredAt: time.Now().UTC()},
		},
	}
	publisher := &recordingPublisher{}

	feed := NewFeed(provider, publisher)
	if err := feed.Run(context.Background()); err != nil {
		t.Fatalf("Run() error = %v", err)
	}

	if len(publisher.updates) != 2 {
		t.Fatalf("published updates = %d, want 2", len(publisher.updates))
	}
}

func TestFeedUsesProviderInterface(t *testing.T) {
	var _ PriceProvider = (*stubProvider)(nil)
	var _ Publisher = (*recordingPublisher)(nil)
}

func TestFeedReturnsPublisherError(t *testing.T) {
	publishErr := errors.New("publisher unavailable")
	provider := &stubProvider{
		updates: []PriceUpdate{
			{Symbol: "PETR4", PriceCents: 3810, OccurredAt: time.Now().UTC()},
		},
	}
	publisher := &recordingPublisher{err: publishErr}

	feed := NewFeed(provider, publisher)
	err := feed.Run(context.Background())
	if !errors.Is(err, publishErr) {
		t.Fatalf("Run() error = %v, want publisher error", err)
	}
}

func TestFeedCancelsProviderWhenPublisherFails(t *testing.T) {
	publishErr := errors.New("publisher unavailable")
	provider := &cancellableProvider{canceled: make(chan struct{})}
	publisher := &recordingPublisher{err: publishErr}

	feed := NewFeed(provider, publisher)
	err := feed.Run(context.Background())
	if !errors.Is(err, publishErr) {
		t.Fatalf("Run() error = %v, want publisher error", err)
	}

	select {
	case <-provider.canceled:
	case <-time.After(500 * time.Millisecond):
		t.Fatal("provider context was not canceled")
	}
}

type stubProvider struct {
	updates []PriceUpdate
}

func (p *stubProvider) Stream(ctx context.Context) (<-chan PriceUpdate, error) {
	ch := make(chan PriceUpdate, len(p.updates))
	for _, update := range p.updates {
		ch <- update
	}
	close(ch)
	return ch, nil
}

type cancellableProvider struct {
	canceled chan struct{}
}

func (p *cancellableProvider) Stream(ctx context.Context) (<-chan PriceUpdate, error) {
	ch := make(chan PriceUpdate, 1)
	ch <- PriceUpdate{Symbol: "PETR4", PriceCents: 3810, OccurredAt: time.Now().UTC()}

	go func() {
		<-ctx.Done()
		close(p.canceled)
		close(ch)
	}()

	return ch, nil
}

type recordingPublisher struct {
	updates []PriceUpdate
	err     error
}

func (p *recordingPublisher) Publish(ctx context.Context, update PriceUpdate) error {
	if p.err != nil {
		return p.err
	}

	p.updates = append(p.updates, update)
	return nil
}
