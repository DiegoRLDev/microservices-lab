package config

import (
	"fmt"
	"os"
	"strconv"
	"strings"
	"time"

	"microserviceslab/pricefeed-service/internal/pricefeed"
)

const (
	defaultProvider       = "mock"
	defaultSymbols        = "PETR4,VALE3,ITUB4,WEGE3,PRIO3"
	defaultInitialPrices  = "PETR4=38.10,VALE3=74.21,ITUB4=42.95,WEGE3=39.80,PRIO3=48.35"
	defaultUpdateInterval = 500 * time.Millisecond
	defaultMaxStepCents   = 5
	defaultHTTPAddr       = ":8080"
	defaultGRPCAddr       = ":9090"
	defaultKafkaEnabled   = false
	defaultKafkaBrokers   = "localhost:9092"
	defaultKafkaTopic     = "price-updated"
)

type Config struct {
	PriceFeed PriceFeedConfig
	HTTP      HTTPConfig
	GRPC      GRPCConfig
	Kafka     KafkaConfig
}

type PriceFeedConfig struct {
	Provider       string
	Symbols        []string
	InitialPrices  map[string]int64
	UpdateInterval time.Duration
	MaxStepCents   int
}

type HTTPConfig struct {
	Addr string
}

type GRPCConfig struct {
	Addr string
}

type KafkaConfig struct {
	Enabled bool
	Brokers []string
	Topic   string
}

func Load() (Config, error) {
	provider := readString("PRICE_PROVIDER", defaultProvider)
	if provider != "mock" {
		return Config{}, fmt.Errorf("PRICE_PROVIDER %q is not supported", provider)
	}

	symbols := readSymbolsCSV("PRICE_SYMBOLS", defaultSymbols)
	if len(symbols) == 0 {
		return Config{}, fmt.Errorf("PRICE_SYMBOLS must contain at least one symbol")
	}

	initialPrices, err := readInitialPrices("PRICE_INITIAL_PRICES", defaultInitialPrices)
	if err != nil {
		return Config{}, err
	}

	for _, symbol := range symbols {
		if _, ok := initialPrices[symbol]; !ok {
			return Config{}, fmt.Errorf("PRICE_INITIAL_PRICES must include a price for %s", symbol)
		}
	}

	updateInterval, err := readDuration("PRICE_UPDATE_INTERVAL", defaultUpdateInterval)
	if err != nil {
		return Config{}, err
	}

	maxStepCents, err := readPositiveInt("PRICE_MAX_STEP_CENTS", defaultMaxStepCents)
	if err != nil {
		return Config{}, err
	}

	kafkaEnabled, err := readBool("PRICE_KAFKA_ENABLED", defaultKafkaEnabled)
	if err != nil {
		return Config{}, err
	}

	kafkaBrokers := readCSV("PRICE_KAFKA_BOOTSTRAP_SERVERS", defaultKafkaBrokers)
	if kafkaEnabled && len(kafkaBrokers) == 0 {
		return Config{}, fmt.Errorf("PRICE_KAFKA_BOOTSTRAP_SERVERS must contain at least one broker when Kafka is enabled")
	}

	return Config{
		PriceFeed: PriceFeedConfig{
			Provider:       provider,
			Symbols:        symbols,
			InitialPrices:  initialPrices,
			UpdateInterval: updateInterval,
			MaxStepCents:   maxStepCents,
		},
		HTTP: HTTPConfig{
			Addr: readString("PRICE_HTTP_ADDR", defaultHTTPAddr),
		},
		GRPC: GRPCConfig{
			Addr: readString("PRICE_GRPC_ADDR", defaultGRPCAddr),
		},
		Kafka: KafkaConfig{
			Enabled: kafkaEnabled,
			Brokers: kafkaBrokers,
			Topic:   readString("PRICE_KAFKA_TOPIC", defaultKafkaTopic),
		},
	}, nil
}

func readString(key string, fallback string) string {
	value := strings.TrimSpace(os.Getenv(key))
	if value == "" {
		return fallback
	}

	return value
}

func readCSV(key string, fallback string) []string {
	raw := readString(key, fallback)
	parts := strings.Split(raw, ",")
	values := make([]string, 0, len(parts))

	for _, part := range parts {
		value := strings.TrimSpace(part)
		if value != "" {
			values = append(values, value)
		}
	}

	return values
}

func readSymbolsCSV(key string, fallback string) []string {
	values := readCSV(key, fallback)
	for index, value := range values {
		values[index] = strings.ToUpper(value)
	}

	return values
}

func readDuration(key string, fallback time.Duration) (time.Duration, error) {
	raw := strings.TrimSpace(os.Getenv(key))
	if raw == "" {
		return fallback, nil
	}

	value, err := time.ParseDuration(raw)
	if err != nil {
		return 0, fmt.Errorf("%s must be a valid duration: %w", key, err)
	}

	if value <= 0 {
		return 0, fmt.Errorf("%s must be greater than zero", key)
	}

	return value, nil
}

func readBool(key string, fallback bool) (bool, error) {
	raw := strings.TrimSpace(os.Getenv(key))
	if raw == "" {
		return fallback, nil
	}

	value, err := strconv.ParseBool(raw)
	if err != nil {
		return false, fmt.Errorf("%s must be a valid boolean: %w", key, err)
	}

	return value, nil
}

func readPositiveInt(key string, fallback int) (int, error) {
	raw := strings.TrimSpace(os.Getenv(key))
	if raw == "" {
		return fallback, nil
	}

	value, err := strconv.Atoi(raw)
	if err != nil {
		return 0, fmt.Errorf("%s must be a valid integer: %w", key, err)
	}

	if value <= 0 {
		return 0, fmt.Errorf("%s must be greater than zero", key)
	}

	return value, nil
}

func readInitialPrices(key string, fallback string) (map[string]int64, error) {
	raw := readString(key, fallback)
	parts := strings.Split(raw, ",")
	prices := make(map[string]int64, len(parts))

	for _, part := range parts {
		name, value, ok := strings.Cut(part, "=")
		if !ok {
			return nil, fmt.Errorf("%s entry %q must use SYMBOL=PRICE", key, part)
		}

		symbol := strings.ToUpper(strings.TrimSpace(name))
		if symbol == "" {
			return nil, fmt.Errorf("%s contains an empty symbol", key)
		}

		cents, err := pricefeed.ParsePriceCents(value)
		if err != nil {
			return nil, fmt.Errorf("%s has invalid price for %s: %w", key, symbol, err)
		}

		prices[symbol] = cents
	}

	return prices, nil
}
