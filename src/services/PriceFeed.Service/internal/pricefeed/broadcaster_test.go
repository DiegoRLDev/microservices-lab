package pricefeed

import (
	"context"
	"testing"
	"time"
)

func TestBroadcasterPublishesToMultipleSubscribers(t *testing.T) {
	broadcaster := NewBroadcaster(1)
	ctx := context.Background()
	first, unsubscribeFirst := broadcaster.Subscribe(ctx)
	defer unsubscribeFirst()
	second, unsubscribeSecond := broadcaster.Subscribe(ctx)
	defer unsubscribeSecond()

	update := PriceUpdate{Symbol: "PETR4", PriceCents: 3810, OccurredAt: time.Now().UTC()}
	if err := broadcaster.Publish(ctx, update); err != nil {
		t.Fatalf("Publish() error = %v", err)
	}

	assertUpdate(t, first, update)
	assertUpdate(t, second, update)
}

func TestBroadcasterRemovesSubscriberWhenContextIsCanceled(t *testing.T) {
	broadcaster := NewBroadcaster(1)
	ctx, cancel := context.WithCancel(context.Background())

	updates, unsubscribe := broadcaster.Subscribe(ctx)
	defer unsubscribe()

	if broadcaster.SubscriberCount() != 1 {
		t.Fatalf("SubscriberCount() = %d, want 1", broadcaster.SubscriberCount())
	}

	cancel()

	select {
	case _, ok := <-updates:
		if ok {
			t.Fatal("subscriber channel is still open")
		}
	case <-time.After(500 * time.Millisecond):
		t.Fatal("subscriber was not removed")
	}

	if broadcaster.SubscriberCount() != 0 {
		t.Fatalf("SubscriberCount() = %d, want 0", broadcaster.SubscriberCount())
	}
}

func TestBroadcasterDoesNotBlockOnSlowSubscriber(t *testing.T) {
	broadcaster := NewBroadcaster(1)
	ctx := context.Background()
	slow, unsubscribeSlow := broadcaster.Subscribe(ctx)
	defer unsubscribeSlow()
	fast, unsubscribeFast := broadcaster.Subscribe(ctx)
	defer unsubscribeFast()

	first := PriceUpdate{Symbol: "PETR4", PriceCents: 3810, OccurredAt: time.Now().UTC()}
	second := PriceUpdate{Symbol: "PETR4", PriceCents: 3811, OccurredAt: time.Now().UTC()}

	if err := broadcaster.Publish(ctx, first); err != nil {
		t.Fatalf("Publish(first) error = %v", err)
	}
	assertUpdate(t, fast, first)

	if err := broadcaster.Publish(ctx, second); err != nil {
		t.Fatalf("Publish(second) error = %v", err)
	}

	assertUpdate(t, fast, second)
	assertUpdate(t, slow, first)

	select {
	case update := <-slow:
		t.Fatalf("slow subscriber received %v after its buffer was full", update)
	default:
	}
}

func assertUpdate(t *testing.T, updates <-chan PriceUpdate, want PriceUpdate) {
	t.Helper()

	select {
	case got := <-updates:
		if got != want {
			t.Fatalf("update = %#v, want %#v", got, want)
		}
	case <-time.After(500 * time.Millisecond):
		t.Fatal("timed out waiting for update")
	}
}
