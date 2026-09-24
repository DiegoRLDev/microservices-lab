package pricefeed

import "testing"

func TestParsePriceCents(t *testing.T) {
	tests := map[string]int64{
		"38":    3800,
		"38.1":  3810,
		"38.10": 3810,
		"0.99":  99,
	}

	for input, want := range tests {
		got, err := ParsePriceCents(input)
		if err != nil {
			t.Fatalf("ParsePriceCents(%q) error = %v", input, err)
		}

		if got != want {
			t.Fatalf("ParsePriceCents(%q) = %d, want %d", input, got, want)
		}
	}
}

func TestFormatPriceCents(t *testing.T) {
	got := FormatPriceCents(3810)
	if got != "38.10" {
		t.Fatalf("FormatPriceCents(3810) = %q, want 38.10", got)
	}
}
