package config

import (
	"fmt"
	"os"
	"strconv"
	"strings"
)

const (
	defaultKafkaBrokers          = "localhost:9092"
	defaultOrderCreatedTopic     = "order-created"
	defaultPaymentApprovedTopic  = "payment-approved"
	defaultPaymentRejectedTopic  = "payment-rejected"
	defaultGroupID               = "payment-service"
	defaultMaxAutoApprovalAmount = 10000.00
)

type Config struct {
	Kafka   KafkaConfig
	Payment PaymentConfig
}

type KafkaConfig struct {
	Brokers              []string
	OrderCreatedTopic    string
	PaymentApprovedTopic string
	PaymentRejectedTopic string
	GroupID              string
}

type PaymentConfig struct {
	MaxAutoApprovalAmount float64
}

func Load() (Config, error) {
	maxAutoApprovalAmount, err := readFloat("PAYMENT_MAX_AUTO_APPROVAL_AMOUNT", defaultMaxAutoApprovalAmount)
	if err != nil {
		return Config{}, err
	}

	return Config{
		Kafka: KafkaConfig{
			Brokers:              readCSV("KAFKA_BROKERS", defaultKafkaBrokers),
			OrderCreatedTopic:    readString("KAFKA_ORDER_CREATED_TOPIC", defaultOrderCreatedTopic),
			PaymentApprovedTopic: readString("KAFKA_PAYMENT_APPROVED_TOPIC", defaultPaymentApprovedTopic),
			PaymentRejectedTopic: readString("KAFKA_PAYMENT_REJECTED_TOPIC", defaultPaymentRejectedTopic),
			GroupID:              readString("KAFKA_GROUP_ID", defaultGroupID),
		},
		Payment: PaymentConfig{
			MaxAutoApprovalAmount: maxAutoApprovalAmount,
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

func readFloat(key string, fallback float64) (float64, error) {
	raw := strings.TrimSpace(os.Getenv(key))
	if raw == "" {
		return fallback, nil
	}

	value, err := strconv.ParseFloat(raw, 64)
	if err != nil {
		return 0, fmt.Errorf("%s must be a valid number: %w", key, err)
	}

	return value, nil
}
