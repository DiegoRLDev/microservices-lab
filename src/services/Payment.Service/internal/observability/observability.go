package observability

import (
	"context"
	"log/slog"
	"os"
	"strings"
	"time"

	"github.com/segmentio/kafka-go"
	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/attribute"
	"go.opentelemetry.io/otel/exporters/otlp/otlpmetric/otlpmetricgrpc"
	"go.opentelemetry.io/otel/exporters/otlp/otlptrace/otlptracegrpc"
	"go.opentelemetry.io/otel/metric"
	"go.opentelemetry.io/otel/propagation"
	"go.opentelemetry.io/otel/sdk/resource"
	sdkmetric "go.opentelemetry.io/otel/sdk/metric"
	sdktrace "go.opentelemetry.io/otel/sdk/trace"
	semconv "go.opentelemetry.io/otel/semconv/v1.37.0"
	"go.opentelemetry.io/otel/trace"
	"google.golang.org/grpc/credentials/insecure"
)

const ServiceName = "MicroservicesLab.PaymentService"

var (
	Tracer = otel.Tracer(ServiceName)
	Meter  = otel.Meter(ServiceName)

	paymentsApproved metric.Int64Counter
	paymentsRejected metric.Int64Counter
)

func Init(ctx context.Context, logger *slog.Logger) (func(context.Context) error, error) {
	endpoint := strings.TrimPrefix(os.Getenv("OTEL_EXPORTER_OTLP_ENDPOINT"), "http://")
	endpoint = strings.TrimPrefix(endpoint, "https://")
	if endpoint == "" {
		endpoint = "otel-collector:4317"
	}

	res, err := resource.New(
		ctx,
		resource.WithAttributes(
			semconv.ServiceName(ServiceName),
			attribute.String("deployment.environment", getenv("APP_ENV", "Development")),
		),
	)
	if err != nil {
		return nil, err
	}

	traceExporter, err := otlptracegrpc.New(
		ctx,
		otlptracegrpc.WithEndpoint(endpoint),
		otlptracegrpc.WithTLSCredentials(insecure.NewCredentials()),
	)
	if err != nil {
		return nil, err
	}

	traceProvider := sdktrace.NewTracerProvider(
		sdktrace.WithResource(res),
		sdktrace.WithBatcher(traceExporter),
	)

	metricExporter, err := otlpmetricgrpc.New(
		ctx,
		otlpmetricgrpc.WithEndpoint(endpoint),
		otlpmetricgrpc.WithTLSCredentials(insecure.NewCredentials()),
	)
	if err != nil {
		return nil, err
	}

	meterProvider := sdkmetric.NewMeterProvider(
		sdkmetric.WithResource(res),
		sdkmetric.WithReader(sdkmetric.NewPeriodicReader(metricExporter, sdkmetric.WithInterval(5*time.Second))),
	)

	otel.SetTracerProvider(traceProvider)
	otel.SetMeterProvider(meterProvider)
	otel.SetTextMapPropagator(propagation.NewCompositeTextMapPropagator(
		propagation.TraceContext{},
		propagation.Baggage{},
	))

	Tracer = otel.Tracer(ServiceName)
	Meter = otel.Meter(ServiceName)

	paymentsApproved, err = Meter.Int64Counter(
		"payments_approved_total",
		metric.WithDescription("Total number of approved payments published by Payment.Service."),
	)
	if err != nil {
		return nil, err
	}

	paymentsRejected, err = Meter.Int64Counter(
		"payments_rejected_total",
		metric.WithDescription("Total number of rejected payments published by Payment.Service."),
	)
	if err != nil {
		return nil, err
	}

	logger.Info("OpenTelemetry initialized", slog.String("otlpEndpoint", endpoint))

	return func(shutdownCtx context.Context) error {
		traceErr := traceProvider.Shutdown(shutdownCtx)
		metricErr := meterProvider.Shutdown(shutdownCtx)
		if traceErr != nil {
			return traceErr
		}
		return metricErr
	}, nil
}

func RecordPayment(ctx context.Context, eventType string) {
	switch eventType {
	case "PaymentApproved":
		paymentsApproved.Add(ctx, 1)
	case "PaymentRejected":
		paymentsRejected.Add(ctx, 1)
	}
}

func InjectKafkaHeaders(ctx context.Context, headers []kafka.Header) []kafka.Header {
	carrier := kafkaHeaderCarrier{headers: headers}
	otel.GetTextMapPropagator().Inject(ctx, &carrier)
	return carrier.headers
}

func ExtractKafkaHeaders(ctx context.Context, headers []kafka.Header) context.Context {
	carrier := kafkaHeaderCarrier{headers: headers}
	return otel.GetTextMapPropagator().Extract(ctx, &carrier)
}

func TraceLogAttrs(ctx context.Context) []any {
	spanContext := trace.SpanContextFromContext(ctx)
	if !spanContext.IsValid() {
		return nil
	}

	return []any{
		slog.String("traceId", spanContext.TraceID().String()),
		slog.String("spanId", spanContext.SpanID().String()),
	}
}

type kafkaHeaderCarrier struct {
	headers []kafka.Header
}

func (c kafkaHeaderCarrier) Get(key string) string {
	for _, header := range c.headers {
		if header.Key == key {
			return string(header.Value)
		}
	}

	return ""
}

func (c *kafkaHeaderCarrier) Set(key string, value string) {
	for index, header := range c.headers {
		if header.Key == key {
			c.headers[index].Value = []byte(value)
			return
		}
	}

	c.headers = append(c.headers, kafka.Header{Key: key, Value: []byte(value)})
}

func (c kafkaHeaderCarrier) Keys() []string {
	keys := make([]string, 0, len(c.headers))
	for _, header := range c.headers {
		keys = append(keys, header.Key)
	}
	return keys
}

func getenv(key string, fallback string) string {
	value := os.Getenv(key)
	if value == "" {
		return fallback
	}
	return value
}
