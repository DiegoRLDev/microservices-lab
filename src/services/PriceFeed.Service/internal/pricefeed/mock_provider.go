package pricefeed

import (
	"context"
	"fmt"
	"math/rand"
	"strings"
	"sync"
	"time"
)

type MockPriceProviderOptions struct {
	Symbols       []string
	InitialPrices map[string]int64
	Interval      time.Duration
	MaxStepCents  int
	Seed          int64
}

type MockPriceProvider struct {
	symbols       []string
	initialPrices map[string]int64
	interval      time.Duration
	maxStepCents  int
	seed          int64
}

func NewMockPriceProvider(options MockPriceProviderOptions) (*MockPriceProvider, error) {
	if len(options.Symbols) == 0 {
		return nil, fmt.Errorf("symbols must not be empty")
	}

	if options.Interval <= 0 {
		return nil, fmt.Errorf("interval must be greater than zero")
	}

	if options.MaxStepCents <= 0 {
		return nil, fmt.Errorf("max step cents must be greater than zero")
	}

	symbols := make([]string, 0, len(options.Symbols))
	initialPrices := make(map[string]int64, len(options.Symbols))
	seen := make(map[string]struct{}, len(options.Symbols))

	for _, raw := range options.Symbols {
		symbol := strings.ToUpper(strings.TrimSpace(raw))
		if symbol == "" {
			return nil, fmt.Errorf("symbol must not be empty")
		}

		if _, ok := seen[symbol]; ok {
			return nil, fmt.Errorf("symbol %s is duplicated", symbol)
		}

		price, ok := options.InitialPrices[symbol]
		if !ok {
			return nil, fmt.Errorf("missing initial price for %s", symbol)
		}

		if price <= 0 {
			return nil, fmt.Errorf("initial price for %s must be greater than zero", symbol)
		}

		seen[symbol] = struct{}{}
		symbols = append(symbols, symbol)
		initialPrices[symbol] = price
	}

	seed := options.Seed
	if seed == 0 {
		seed = time.Now().UnixNano()
	}

	return &MockPriceProvider{
		symbols:       symbols,
		initialPrices: initialPrices,
		interval:      options.Interval,
		maxStepCents:  options.MaxStepCents,
		seed:          seed,
	}, nil
}

func (p *MockPriceProvider) Stream(ctx context.Context) (<-chan PriceUpdate, error) {
	updates := make(chan PriceUpdate, len(p.symbols))
	var wg sync.WaitGroup

	for index, symbol := range p.symbols {
		wg.Add(1)
		go func(index int, symbol string) {
			defer wg.Done()
			p.streamSymbol(ctx, updates, index, symbol)
		}(index, symbol)
	}

	go func() {
		wg.Wait()
		close(updates)
	}()

	return updates, nil
}

func (p *MockPriceProvider) streamSymbol(ctx context.Context, updates chan<- PriceUpdate, index int, symbol string) {
	current := p.initialPrices[symbol]
	random := rand.New(rand.NewSource(p.seed + int64(index+1)*7919))
	ticker := time.NewTicker(p.interval)
	defer ticker.Stop()

	publish := func(occurredAt time.Time) bool {
		update := PriceUpdate{
			Symbol:     symbol,
			PriceCents: current,
			OccurredAt: occurredAt,
		}

		select {
		case <-ctx.Done():
			return false
		case updates <- update:
			return true
		}
	}

	if !publish(time.Now().UTC()) {
		return
	}

	for {
		select {
		case <-ctx.Done():
			return
		case occurredAt := <-ticker.C:
			current = nextPrice(current, p.maxStepCents, random)
			if !publish(occurredAt.UTC()) {
				return
			}
		}
	}
}

func nextPrice(current int64, maxStepCents int, random *rand.Rand) int64 {
	stepRange := maxStepCents*2 + 1
	delta := random.Intn(stepRange) - maxStepCents
	if delta == 0 {
		delta = 1
	}

	next := current + int64(delta)
	if next < 1 {
		return 1
	}

	return next
}
