using SubTrack.Domain.Entities;

namespace SubTrack.Application.Common;

public interface IApplicationDbContext
{
    // Вместо DbSet используем IQueryable (это стандартный интерфейс C# для запросов)
    IQueryable<Subscription> Subscriptions { get; }

    // Метод добавления новой записи
    void AddSubscription(Subscription subscription);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
