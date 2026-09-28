package grpcdelivery

import (
	"context"
	"net"
	"testing"
	"time"

	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
	"google.golang.org/grpc/test/bufconn"
	"microserviceslab/pricefeed-service/internal/grpcdelivery/pricefeedpb"
	"microserviceslab/pricefeed-service/internal/pricefeed"
)

const bufferSize = 1024 * 1024

func TestStreamPricesSendsUpdatesAndStopsOnCancel(t *testing.T) {
	broadcaster := pricefeed.NewBroadcaster(1)
	listener := bufconn.Listen(bufferSize)
	server := grpc.NewServer()
	pricefeedpb.RegisterPriceFeedServer(server, NewPriceFeedServer(broadcaster))

	go func() {
		_ = server.Serve(listener)
	}()
	defer server.Stop()

	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	conn, err := grpc.DialContext(
		ctx,
		"bufnet",
		grpc.WithContextDialer(func(context.Context, string) (net.Conn, error) {
			return listener.Dial()
		}),
		grpc.WithTransportCredentials(insecure.NewCredentials()),
	)
	if err != nil {
		t.Fatalf("DialContext() error = %v", err)
	}
	defer conn.Close()

	client := pricefeedpb.NewPriceFeedClient(conn)
	stream, err := client.StreamPrices(ctx, &pricefeedpb.StreamPricesRequest{})
	if err != nil {
		t.Fatalf("StreamPrices() error = %v", err)
	}

	waitForSubscribers(t, broadcaster, 1)

	update := pricefeed.PriceUpdate{
		Symbol:     "PETR4",
		PriceCents: 3810,
		OccurredAt: time.Date(2026, 9, 28, 14, 30, 1, 0, time.UTC),
	}
	if err := broadcaster.Publish(ctx, update); err != nil {
		t.Fatalf("Publish() error = %v", err)
	}

	got, err := stream.Recv()
	if err != nil {
		t.Fatalf("Recv() error = %v", err)
	}

	if got.Symbol != update.Symbol || got.Price != update.PriceCents {
		t.Fatalf("update = %s %d, want %s %d", got.Symbol, got.Price, update.Symbol, update.PriceCents)
	}

	cancel()
	waitForSubscribers(t, broadcaster, 0)
}

func waitForSubscribers(t *testing.T, broadcaster *pricefeed.Broadcaster, want int) {
	t.Helper()

	deadline := time.After(500 * time.Millisecond)
	ticker := time.NewTicker(10 * time.Millisecond)
	defer ticker.Stop()

	for {
		if broadcaster.SubscriberCount() == want {
			return
		}

		select {
		case <-deadline:
			t.Fatalf("SubscriberCount() = %d, want %d", broadcaster.SubscriberCount(), want)
		case <-ticker.C:
		}
	}
}
