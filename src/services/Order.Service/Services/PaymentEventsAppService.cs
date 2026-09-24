using Microsoft.EntityFrameworkCore;
using MicroservicesLab.OrderService.Infrastructure;
using MicroservicesLab.Shared.Contracts.Events;

namespace MicroservicesLab.OrderService.Services;

public sealed class PaymentEventsAppService(
    OrdersDbContext dbContext,
    ILogger<PaymentEventsAppService> logger)
{
    public Task ApplyPaymentApprovedAsync(
        EventEnvelope<PaymentApprovedData> envelope,
        CancellationToken cancellationToken)
    {
        return ApplyAsync(
            envelope.Data.OrderId,
            envelope.EventId,
            approved: true,
            cancellationToken);
    }

    public Task ApplyPaymentRejectedAsync(
        EventEnvelope<PaymentRejectedData> envelope,
        CancellationToken cancellationToken)
    {
        return ApplyAsync(
            envelope.Data.OrderId,
            envelope.EventId,
            approved: false,
            cancellationToken);
    }

    private async Task ApplyAsync(
        Guid orderId,
        Guid eventId,
        bool approved,
        CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders
            .SingleOrDefaultAsync(order => order.Id == orderId, cancellationToken);

        if (order is null)
        {
            logger.LogWarning(
                "Payment event {EventId} references missing order {OrderId}",
                eventId,
                orderId);

            return;
        }

        var changed = approved ? order.MarkAsPaid() : order.RejectPayment();

        if (!changed)
        {
            logger.LogInformation(
                "Payment event {EventId} for order {OrderId} was already applied. Current status: {OrderStatus}",
                eventId,
                orderId,
                order.Status);

            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Applied payment event {EventId} to order {OrderId}. New status: {OrderStatus}",
            eventId,
            orderId,
            order.Status);
    }
}
