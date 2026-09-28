package pricefeed

import (
	"context"
	"sync"
)

const DefaultSubscriberBuffer = 16

type Broadcaster struct {
	mu          sync.RWMutex
	nextID      int
	bufferSize  int
	subscribers map[int]chan PriceUpdate
}

func NewBroadcaster(bufferSize int) *Broadcaster {
	if bufferSize <= 0 {
		bufferSize = DefaultSubscriberBuffer
	}

	return &Broadcaster{
		bufferSize:  bufferSize,
		subscribers: make(map[int]chan PriceUpdate),
	}
}

func (b *Broadcaster) Subscribe(ctx context.Context) (<-chan PriceUpdate, func()) {
	ch := make(chan PriceUpdate, b.bufferSize)

	b.mu.Lock()
	id := b.nextID
	b.nextID++
	b.subscribers[id] = ch
	b.mu.Unlock()

	var once sync.Once
	done := make(chan struct{})
	unsubscribe := func() {
		once.Do(func() {
			b.mu.Lock()
			if subscriber, ok := b.subscribers[id]; ok {
				delete(b.subscribers, id)
				close(subscriber)
			}
			b.mu.Unlock()
			close(done)
		})
	}

	go func() {
		select {
		case <-ctx.Done():
			unsubscribe()
		case <-done:
		}
	}()

	return ch, unsubscribe
}

func (b *Broadcaster) Publish(ctx context.Context, update PriceUpdate) error {
	b.mu.RLock()
	defer b.mu.RUnlock()

	for _, subscriber := range b.subscribers {
		select {
		case <-ctx.Done():
			return ctx.Err()
		case subscriber <- update:
		default:
		}
	}

	return nil
}

func (b *Broadcaster) SubscriberCount() int {
	b.mu.RLock()
	defer b.mu.RUnlock()

	return len(b.subscribers)
}
