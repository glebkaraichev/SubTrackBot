using MediatR;

namespace SubTrack.Application.Subscriptions.Commands;

public record CreateSubscriptionCommand(
    string Name,
    decimal Price,
    string Currency,
    DateTime NextPaymentDate,
    int PaymentPeriod
) : IRequest<Guid>;