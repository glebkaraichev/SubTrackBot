using Quartz;
using Telegram.Bot;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SubTrack.Infrastructure.Persistence;

namespace SubTrack.Api.Jobs;

public class NotificationJob : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<NotificationJob> _logger;

    public NotificationJob(IServiceProvider serviceProvider, ITelegramBotClient botClient, ILogger<NotificationJob> logger)
    {
        _serviceProvider = serviceProvider;
        _botClient = botClient;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("⏳ Quartz заустил проверку подписок для отправки уведомлений...");

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Вычисляем дату «завтра»
        var tomorrow = DateTime.UtcNow.Date.AddDays(1);

        // Ищем подписки, у которых день и месяц оплаты совпадают с завтрашним днем
        // (Для простоты берем проверку по дню списания)
        var subscriptionsToNotify = dbContext.Subscriptions
            .ToList() // Вытягиваем в память для простой фильтрации дат
            .Where(s => s.NextPaymentDate.Date == tomorrow)
            .ToList();

        if (!subscriptionsToNotify.Any())
        {
            _logger.LogInformation("Завтра списаний не планируется. Уведомления не отправлены.");
            return;
        }

        // Вытаскиваем все активные сессии пользователей, чтобы знать, кому слать (их ChatId)
        var userChatIds = dbContext.UserSessions.Select(us => us.ChatId).ToList();

        foreach (var chatId in userChatIds)
        {
            foreach (var sub in subscriptionsToNotify)
            {
                try
                {
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: $"⏰ **Напоминание о подписке!**\n\nЗавтра ({sub.NextPaymentDate.ToLocalTime():dd.MM.yyyy}) состоится списание за подписку *{sub.Name}*.\n💰 Сумма: *{sub.Price} RUB*",
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown
                    );

                    _logger.LogInformation("Уведомление по подписке '{SubName}' успешно отправлено в чат {ChatId}", sub.Name, chatId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Не удалось отправить уведомление пользователю {ChatId}", chatId);
                }
            }
        }
    }
}