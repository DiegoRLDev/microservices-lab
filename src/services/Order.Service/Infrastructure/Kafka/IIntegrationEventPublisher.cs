using MicroservicesLab.Shared.Contracts.Events;

namespace MicroservicesLab.OrderService.Infrastructure.Kafka;

public interface IIntegrationEventPublisher
{
    Task PublishAsync<TData>(
        string topic,
        string key,
        EventEnvelope<TData> envelope,
        CancellationToken cancellationToken);
}

