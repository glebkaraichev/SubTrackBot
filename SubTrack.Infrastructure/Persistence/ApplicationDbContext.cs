using Microsoft.EntityFrameworkCore;
using SubTrack.Domain.Entities;
using SubTrack.Application.Common;

namespace SubTrack.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserSession> UserSessions { get; set; }
    // EF Core автоматически умеет приводить DbSet к IQueryable, так что эту строку менять не нужно
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    // Явно реализуем интерфейс IApplicationDbContext
    IQueryable<Subscription> IApplicationDbContext.Subscriptions => Subscriptions;

    // Реализуем метод добавления записи через стандартный DbSet
    public void AddSubscription(Subscription subscription)
    {
        Subscriptions.Add(subscription);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
        });
    }
}