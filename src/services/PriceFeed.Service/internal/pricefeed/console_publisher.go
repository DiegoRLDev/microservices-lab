package pricefeed

import (
	"context"
	"fmt"
	"io"
)

type ConsolePublisher struct {
	writer io.Writer
}

func NewConsolePublisher(writer io.Writer) *ConsolePublisher {
	return &ConsolePublisher{writer: writer}
}

func (p *ConsolePublisher) Publish(ctx context.Context, update PriceUpdate) error {
	select {
	case <-ctx.Done():
		return ctx.Err()
	default:
	}

	_, err := fmt.Fprintf(
		p.writer,
		"%s %s %s\n",
		update.OccurredAt.Format("15:04:05.000"),
		update.Symbol,
		FormatPriceCents(update.PriceCents),
	)

	return err
}
