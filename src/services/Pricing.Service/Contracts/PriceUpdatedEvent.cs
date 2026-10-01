namespace MicroservicesLab.PricingService.Contracts;

public sealed record PriceUpdatedEvent(
    string Symbol,
    long Price,
    DateTimeOffset OccurredAt);
