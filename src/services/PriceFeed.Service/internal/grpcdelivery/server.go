package grpcdelivery

import (
	"context"

	"google.golang.org/protobuf/types/known/timestamppb"
	"microserviceslab/pricefeed-service/internal/grpcdelivery/pricefeedpb"
	"microserviceslab/pricefeed-service/internal/pricefeed"
)

type PriceFeedServer struct {
	pricefeedpb.UnimplementedPriceFeedServer
	broadcaster *pricefeed.Broadcaster
}

func NewPriceFeedServer(broadcaster *pricefeed.Broadcaster) *PriceFeedServer {
	return &PriceFeedServer{broadcaster: broadcaster}
}

func (s *PriceFeedServer) StreamPrices(request *pricefeedpb.StreamPricesRequest, stream pricefeedpb.PriceFeed_StreamPricesServer) error {
	updates, unsubscribe := s.broadcaster.Subscribe(stream.Context())
	defer unsubscribe()

	for {
		select {
		case <-stream.Context().Done():
			return nil
		case update, ok := <-updates:
			if !ok {
				return nil
			}

			if err := stream.Send(toProto(update)); err != nil {
				return err
			}
		}
	}
}

func toProto(update pricefeed.PriceUpdate) *pricefeedpb.PriceUpdate {
	return &pricefeedpb.PriceUpdate{
		Symbol:     update.Symbol,
		Price:      update.PriceCents,
		OccurredAt: timestamppb.New(update.OccurredAt),
	}
}

func Shutdown(ctx context.Context, stop func(), gracefulStop func()) {
	done := make(chan struct{})
	go func() {
		gracefulStop()
		close(done)
	}()

	select {
	case <-done:
	case <-ctx.Done():
		stop()
	}
}
