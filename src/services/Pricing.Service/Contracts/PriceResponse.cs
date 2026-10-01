namespace MicroservicesLab.PricingService.Contracts;

public sealed record PriceResponse(
    string Symbol,
    long Price,
    DateTimeOffset OccurredAt);
