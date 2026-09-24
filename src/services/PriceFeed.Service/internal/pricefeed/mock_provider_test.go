package pricefeed

import (
	"context"
	"testing"
	"time"
)

func TestMockPriceProviderProducesUpdatesForMultipleSymbols(t *testing.T) {
	provider := newTestProvider(t, []string{"PETR4", "VALE3"}, map[string]int64{
		"PETR4": 3810,
		"VALE3": 7421,
	})

	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	updates, err := provider.Stream(ctx)
	if err != nil {
		t.Fatalf("Stream() error = %v", err)
	}

	seen := map[string]bool{}
	for len(seen) < 2 {
		update := receiveUpdate(t, updates)
		seen[update.Symbol] = true
		if update.PriceCents <= 0 {
			t.Fatalf("PriceCents = %d, want positive", update.PriceCents)
		}
	}
}

func TestMockPriceProviderAppliesBoundedOscillation(t *testing.T) {
	provider := newTestProvider(t, []string{"PETR4"}, map[string]int64{"PETR4": 3810})

	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	updates, err := provider.Stream(ctx)
	if err != nil {
		t.Fatalf("Stream() error = %v", err)
	}

	first := receiveUpdate(t, updates)
	second := receiveUpdate(t, updates)

	diff := second.PriceCents - first.PriceCents
	if diff < 0 {
		diff = -diff
	}

	if diff == 0 {
		t.Fatal("price did not change")
	}

	if diff > 5 {
		t.Fatalf("diff = %d cents, want <= 5", diff)
	}
}

func TestMockPriceProviderStopsWhenContextIsCanceled(t *testing.T) {
	provider := newTestProvider(t, []string{"PETR4"}, map[string]int64{"PETR4": 3810})

	ctx, cancel := context.WithCancel(context.Background())
	updates, err := provider.Stream(ctx)
	if err != nil {
		t.Fatalf("Stream() error = %v", err)
	}

	_ = receiveUpdate(t, updates)
	cancel()

	select {
	case _, ok := <-updates:
		if ok {
			t.Fatal("updates channel still open after context cancellation")
		}
	case <-time.After(500 * time.Millisecond):
		t.Fatal("updates channel was not closed after context cancellation")
	}
}

func newTestProvider(t *testing.T, symbols []string, prices map[string]int64) *MockPriceProvider {
	t.Helper()

	provider, err := NewMockPriceProvider(MockPriceProviderOptions{
		Symbols:       symbols,
		InitialPrices: prices,
		Interval:      time.Millisecond,
		MaxStepCents:  5,
		Seed:          42,
	})
	if err != nil {
		t.Fatalf("NewMockPriceProvider() error = %v", err)
	}

	return provider
}

func receiveUpdate(t *testing.T, updates <-chan PriceUpdate) PriceUpdate {
	t.Helper()

	select {
	case update, ok := <-updates:
		if !ok {
			t.Fatal("updates channel closed before receiving update")
		}
		return update
	case <-time.After(500 * time.Millisecond):
		t.Fatal("timed out waiting for price update")
	}

	return PriceUpdate{}
}
