namespace SubTrack.Domain.Entities;

public class Subscription
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
    public required string Currency { get; set; }
    public DateTime NextPaymentDate { get; set; }
    public PaymentPeriod Period { get; set; }
    public string? Category { get; set; }
    public bool IsAutoRenewal { get; set; } = true;
}

public enum PaymentPeriod
{
    Monthly,
    Quarterly,
    Yearly
}