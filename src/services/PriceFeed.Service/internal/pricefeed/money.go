package pricefeed

import (
	"fmt"
	"strconv"
	"strings"
)

func FormatPriceCents(cents int64) string {
	sign := ""
	if cents < 0 {
		sign = "-"
		cents = -cents
	}

	return fmt.Sprintf("%s%d.%02d", sign, cents/100, cents%100)
}

func ParsePriceCents(value string) (int64, error) {
	raw := strings.TrimSpace(value)
	if raw == "" {
		return 0, fmt.Errorf("price must not be empty")
	}

	if strings.Contains(raw, ",") {
		return 0, fmt.Errorf("use dot as decimal separator")
	}

	whole, fraction, hasFraction := strings.Cut(raw, ".")
	if strings.TrimSpace(whole) == "" {
		return 0, fmt.Errorf("price must include whole units")
	}

	units, err := strconv.ParseInt(strings.TrimSpace(whole), 10, 64)
	if err != nil {
		return 0, err
	}

	var cents int64
	if hasFraction {
		if len(fraction) > 2 {
			return 0, fmt.Errorf("price supports at most two decimal places")
		}

		for len(fraction) < 2 {
			fraction += "0"
		}

		cents, err = strconv.ParseInt(fraction, 10, 64)
		if err != nil {
			return 0, err
		}
	}

	if units < 0 || cents < 0 {
		return 0, fmt.Errorf("price must be positive")
	}

	return units*100 + cents, nil
}
