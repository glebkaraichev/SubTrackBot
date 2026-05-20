using MediatR;
using SubTrack.Domain.Entities;
using SubTrack.Application.Common; // ВМЕСТО Infrastructure теперь подключаем Common!

namespace SubTrack.Application.Subscriptions.Commands;

public class CreateSubscriptionCommandHandler : IRequestHandler<CreateSubscriptionCommand, Guid>
{
    // Вместо ApplicationDbContext используем интерфейс IApplicationDbContext
    private readonly IApplicationDbContext _context;

    public CreateSubscriptionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Price = request.Price,
            Currency = request.Currency,
            NextPaymentDate = request.NextPaymentDate,
            Period = (PaymentPeriod)request.PaymentPeriod
        };

       
        _context.AddSubscription(subscription);

        await _context.SaveChangesAsync(cancellationToken);

        return subscription.Id;
    }
}