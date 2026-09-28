package config

import (
	"testing"
	"time"
)

func TestLoadReturnsDefaults(t *testing.T) {
	t.Setenv("PRICE_PROVIDER", "")
	t.Setenv("PRICE_SYMBOLS", "")
	t.Setenv("PRICE_INITIAL_PRICES", "")
	t.Setenv("PRICE_UPDATE_INTERVAL", "")
	t.Setenv("PRICE_MAX_STEP_CENTS", "")
	t.Setenv("PRICE_HTTP_ADDR", "")

	cfg, err := Load()
	if err != nil {
		t.Fatalf("Load() error = %v", err)
	}

	if cfg.PriceFeed.Provider != "mock" {
		t.Fatalf("Provider = %q, want mock", cfg.PriceFeed.Provider)
	}

	if len(cfg.PriceFeed.Symbols) != 5 {
		t.Fatalf("len(Symbols) = %d, want 5", len(cfg.PriceFeed.Symbols))
	}

	if cfg.PriceFeed.UpdateInterval != 500*time.Millisecond {
		t.Fatalf("UpdateInterval = %s, want 500ms", cfg.PriceFeed.UpdateInterval)
	}

	if cfg.PriceFeed.InitialPrices["PETR4"] != 3810 {
		t.Fatalf("PETR4 initial price = %d, want 3810", cfg.PriceFeed.InitialPrices["PETR4"])
	}

	if cfg.HTTP.Addr != ":8080" {
		t.Fatalf("HTTP Addr = %q, want :8080", cfg.HTTP.Addr)
	}
}

func TestLoadReadsEnvironment(t *testing.T) {
	t.Setenv("PRICE_PROVIDER", "mock")
	t.Setenv("PRICE_SYMBOLS", "abc, xyz")
	t.Setenv("PRICE_INITIAL_PRICES", "ABC=10.50,XYZ=20.75")
	t.Setenv("PRICE_UPDATE_INTERVAL", "100ms")
	t.Setenv("PRICE_MAX_STEP_CENTS", "7")
	t.Setenv("PRICE_HTTP_ADDR", ":9090")

	cfg, err := Load()
	if err != nil {
		t.Fatalf("Load() error = %v", err)
	}

	if got := cfg.PriceFeed.Symbols; len(got) != 2 || got[0] != "ABC" || got[1] != "XYZ" {
		t.Fatalf("Symbols = %#v, want ABC and XYZ", got)
	}

	if cfg.PriceFeed.InitialPrices["ABC"] != 1050 {
		t.Fatalf("ABC initial price = %d, want 1050", cfg.PriceFeed.InitialPrices["ABC"])
	}

	if cfg.PriceFeed.UpdateInterval != 100*time.Millisecond {
		t.Fatalf("UpdateInterval = %s, want 100ms", cfg.PriceFeed.UpdateInterval)
	}

	if cfg.PriceFeed.MaxStepCents != 7 {
		t.Fatalf("MaxStepCents = %d, want 7", cfg.PriceFeed.MaxStepCents)
	}

	if cfg.HTTP.Addr != ":9090" {
		t.Fatalf("HTTP Addr = %q, want :9090", cfg.HTTP.Addr)
	}
}

func TestLoadRejectsMissingInitialPrice(t *testing.T) {
	t.Setenv("PRICE_SYMBOLS", "PETR4,VALE3")
	t.Setenv("PRICE_INITIAL_PRICES", "PETR4=38.10")

	_, err := Load()
	if err == nil {
		t.Fatal("Load() error = nil, want error")
	}
}
