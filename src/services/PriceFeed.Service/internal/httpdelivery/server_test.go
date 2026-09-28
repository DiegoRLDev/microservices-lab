package httpdelivery

import (
	"bufio"
	"context"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
	"time"

	"microserviceslab/pricefeed-service/internal/pricefeed"
)

func TestHealthEndpoint(t *testing.T) {
	server := NewServer(pricefeed.NewBroadcaster(1), nil)
	recorder := httptest.NewRecorder()

	req := httptest.NewRequest(http.MethodGet, "/health", nil)
	server.Handler().ServeHTTP(recorder, req)

	if recorder.Code != http.StatusOK {
		t.Fatalf("status = %d, want 200", recorder.Code)
	}

	if strings.TrimSpace(recorder.Body.String()) != "ok" {
		t.Fatalf("body = %q, want ok", recorder.Body.String())
	}
}

func TestPriceStreamReturnsSSEHeadersAndUpdate(t *testing.T) {
	broadcaster := pricefeed.NewBroadcaster(2)
	testServer := httptest.NewServer(NewServer(broadcaster, nil).Handler())
	defer testServer.Close()

	resp, reader := openStream(t, testServer.URL)
	defer resp.Body.Close()

	waitForSubscribers(t, broadcaster, 1)

	update := pricefeed.PriceUpdate{
		Symbol:     "PETR4",
		PriceCents: 3810,
		OccurredAt: time.Date(2026, 9, 28, 14, 30, 1, 0, time.UTC),
	}
	if err := broadcaster.Publish(context.Background(), update); err != nil {
		t.Fatalf("Publish() error = %v", err)
	}

	event := readSSEEvent(t, reader)
	if event.Symbol != "PETR4" || event.Price != 3810 || !event.OccurredAt.Equal(update.OccurredAt) {
		t.Fatalf("event = %#v, want PETR4 3810 at %s", event, update.OccurredAt)
	}
}

func TestPriceStreamBroadcastsToMultipleClients(t *testing.T) {
	broadcaster := pricefeed.NewBroadcaster(2)
	testServer := httptest.NewServer(NewServer(broadcaster, nil).Handler())
	defer testServer.Close()

	firstResp, firstReader := openStream(t, testServer.URL)
	defer firstResp.Body.Close()
	secondResp, secondReader := openStream(t, testServer.URL)
	defer secondResp.Body.Close()

	waitForSubscribers(t, broadcaster, 2)

	update := pricefeed.PriceUpdate{
		Symbol:     "VALE3",
		PriceCents: 7421,
		OccurredAt: time.Now().UTC(),
	}
	if err := broadcaster.Publish(context.Background(), update); err != nil {
		t.Fatalf("Publish() error = %v", err)
	}

	if event := readSSEEvent(t, firstReader); event.Symbol != "VALE3" || event.Price != 7421 {
		t.Fatalf("first event = %#v, want VALE3 7421", event)
	}

	if event := readSSEEvent(t, secondReader); event.Symbol != "VALE3" || event.Price != 7421 {
		t.Fatalf("second event = %#v, want VALE3 7421", event)
	}
}

func TestPriceStreamDisconnectRemovesSubscriber(t *testing.T) {
	broadcaster := pricefeed.NewBroadcaster(2)
	testServer := httptest.NewServer(NewServer(broadcaster, nil).Handler())
	defer testServer.Close()

	resp, _ := openStream(t, testServer.URL)
	waitForSubscribers(t, broadcaster, 1)

	if err := resp.Body.Close(); err != nil {
		t.Fatalf("Close() error = %v", err)
	}

	waitForSubscribers(t, broadcaster, 0)
}

func openStream(t *testing.T, baseURL string) (*http.Response, *bufio.Reader) {
	t.Helper()

	resp, err := http.Get(baseURL + "/prices/stream")
	if err != nil {
		t.Fatalf("GET /prices/stream error = %v", err)
	}

	if resp.StatusCode != http.StatusOK {
		resp.Body.Close()
		t.Fatalf("status = %d, want 200", resp.StatusCode)
	}

	contentType := resp.Header.Get("Content-Type")
	if !strings.HasPrefix(contentType, "text/event-stream") {
		resp.Body.Close()
		t.Fatalf("Content-Type = %q, want text/event-stream", contentType)
	}

	return resp, bufio.NewReader(resp.Body)
}

func readSSEEvent(t *testing.T, reader *bufio.Reader) priceUpdateEvent {
	t.Helper()

	var event priceUpdateEvent
	for {
		line, err := reader.ReadString('\n')
		if err != nil {
			t.Fatalf("ReadString() error = %v", err)
		}

		line = strings.TrimSpace(line)
		if strings.HasPrefix(line, "data: ") {
			payload := strings.TrimPrefix(line, "data: ")
			if err := json.Unmarshal([]byte(payload), &event); err != nil {
				t.Fatalf("Unmarshal() error = %v", err)
			}
		}

		if line == "" && event.Symbol != "" {
			return event
		}
	}
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
