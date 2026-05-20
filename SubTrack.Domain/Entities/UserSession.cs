using System.ComponentModel.DataAnnotations;

namespace SubTrack.Domain.Entities;

public enum UserState
{
    None = 0,
    WaitingForName = 1,
    WaitingForPrice = 2,
    WaitingForDate = 3
}

public class UserSession
{
    [Key]
    public long ChatId { get; set; } // ID чата в Telegram
    public UserState CurrentState { get; set; } = UserState.None;

    // Временные карманы для сборки подписки по шагам
    public string? TempName { get; set; }
    public decimal? TempPrice { get; set; }
}