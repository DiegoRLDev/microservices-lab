package config

import "testing"

func TestLoadUsesDefaults(t *testing.T) {
	t.Setenv("KAFKA_BROKERS", "")
	t.Setenv("KAFKA_ORDER_CREATED_TOPIC", "")
	t.Setenv("KAFKA_PAYMENT_APPROVED_TOPIC", "")
	t.Setenv("KAFKA_PAYMENT_REJECTED_TOPIC", "")
	t.Setenv("KAFKA_GROUP_ID", "")
	t.Setenv("PAYMENT_MAX_AUTO_APPROVAL_AMOUNT", "")

	cfg, err := Load()
	if err != nil {
		t.Fatalf("Load returned error: %v", err)
	}

	if got := cfg.Kafka.Brokers; len(got) != 1 || got[0] != "localhost:9092" {
		t.Fatalf("unexpected brokers: %#v", got)
	}

	if cfg.Kafka.OrderCreatedTopic != "order-created" {
		t.Fatalf("unexpected topic: %s", cfg.Kafka.OrderCreatedTopic)
	}

	if cfg.Kafka.PaymentApprovedTopic != "payment-approved" {
		t.Fatalf("unexpected payment approved topic: %s", cfg.Kafka.PaymentApprovedTopic)
	}

	if cfg.Kafka.PaymentRejectedTopic != "payment-rejected" {
		t.Fatalf("unexpected payment rejected topic: %s", cfg.Kafka.PaymentRejectedTopic)
	}

	if cfg.Kafka.GroupID != "payment-service" {
		t.Fatalf("unexpected group id: %s", cfg.Kafka.GroupID)
	}

	if cfg.Payment.MaxAutoApprovalAmount != 10000 {
		t.Fatalf("unexpected max auto approval amount: %f", cfg.Payment.MaxAutoApprovalAmount)
	}
}

func TestLoadParsesEnvironment(t *testing.T) {
	t.Setenv("KAFKA_BROKERS", "localhost:9092, kafka:29092")
	t.Setenv("KAFKA_ORDER_CREATED_TOPIC", "orders")
	t.Setenv("KAFKA_PAYMENT_APPROVED_TOPIC", "payments-ok")
	t.Setenv("KAFKA_PAYMENT_REJECTED_TOPIC", "payments-fail")
	t.Setenv("KAFKA_GROUP_ID", "payments")
	t.Setenv("PAYMENT_MAX_AUTO_APPROVAL_AMOUNT", "50.25")

	cfg, err := Load()
	if err != nil {
		t.Fatalf("Load returned error: %v", err)
	}

	if len(cfg.Kafka.Brokers) != 2 || cfg.Kafka.Brokers[0] != "localhost:9092" || cfg.Kafka.Brokers[1] != "kafka:29092" {
		t.Fatalf("unexpected brokers: %#v", cfg.Kafka.Brokers)
	}

	if cfg.Kafka.OrderCreatedTopic != "orders" {
		t.Fatalf("unexpected topic: %s", cfg.Kafka.OrderCreatedTopic)
	}

	if cfg.Kafka.PaymentApprovedTopic != "payments-ok" {
		t.Fatalf("unexpected payment approved topic: %s", cfg.Kafka.PaymentApprovedTopic)
	}

	if cfg.Kafka.PaymentRejectedTopic != "payments-fail" {
		t.Fatalf("unexpected payment rejected topic: %s", cfg.Kafka.PaymentRejectedTopic)
	}

	if cfg.Kafka.GroupID != "payments" {
		t.Fatalf("unexpected group id: %s", cfg.Kafka.GroupID)
	}

	if cfg.Payment.MaxAutoApprovalAmount != 50.25 {
		t.Fatalf("unexpected max auto approval amount: %f", cfg.Payment.MaxAutoApprovalAmount)
	}
}
