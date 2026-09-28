package httpdelivery

import (
	"context"
	"encoding/json"
	"fmt"
	"log/slog"
	"net/http"
	"time"

	"microserviceslab/pricefeed-service/internal/pricefeed"
)

type Server struct {
	broadcaster *pricefeed.Broadcaster
	logger      *slog.Logger
}

type priceUpdateEvent struct {
	Symbol     string    `json:"symbol"`
	Price      int64     `json:"price"`
	OccurredAt time.Time `json:"occurredAt"`
}

func NewServer(broadcaster *pricefeed.Broadcaster, logger *slog.Logger) *Server {
	if logger == nil {
		logger = slog.Default()
	}

	return &Server{
		broadcaster: broadcaster,
		logger:      logger,
	}
}

func (s *Server) Handler() http.Handler {
	mux := http.NewServeMux()
	mux.HandleFunc("GET /health", s.handleHealth)
	mux.HandleFunc("GET /prices/stream", s.handlePriceStream)
	return mux
}

func (s *Server) handleHealth(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "text/plain; charset=utf-8")
	w.WriteHeader(http.StatusOK)
	_, _ = w.Write([]byte("ok\n"))
}

func (s *Server) handlePriceStream(w http.ResponseWriter, r *http.Request) {
	flusher, ok := w.(http.Flusher)
	if !ok {
		http.Error(w, "streaming unsupported", http.StatusInternalServerError)
		return
	}

	w.Header().Set("Content-Type", "text/event-stream")
	w.Header().Set("Cache-Control", "no-cache")
	w.Header().Set("Connection", "keep-alive")
	w.Header().Set("X-Accel-Buffering", "no")
	w.WriteHeader(http.StatusOK)
	flusher.Flush()

	updates, unsubscribe := s.broadcaster.Subscribe(r.Context())
	defer unsubscribe()

	for {
		select {
		case <-r.Context().Done():
			return
		case update, ok := <-updates:
			if !ok {
				return
			}

			if err := writeSSE(w, update); err != nil {
				s.logger.Debug("failed to write SSE event", slog.String("error", err.Error()))
				return
			}
			flusher.Flush()
		}
	}
}

func writeSSE(w http.ResponseWriter, update pricefeed.PriceUpdate) error {
	event := priceUpdateEvent{
		Symbol:     update.Symbol,
		Price:      update.PriceCents,
		OccurredAt: update.OccurredAt,
	}

	payload, err := json.Marshal(event)
	if err != nil {
		return err
	}

	_, err = fmt.Fprintf(w, "event: price-update\ndata: %s\n\n", payload)
	return err
}

func Shutdown(ctx context.Context, server *http.Server) error {
	if server == nil {
		return nil
	}

	return server.Shutdown(ctx)
}
