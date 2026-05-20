using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using SubTrack.Domain.Entities;
using SubTrack.Infrastructure.Persistence;

namespace SubTrack.Api.Services;

public class UpdateRouter
{
    private readonly IServiceProvider _serviceProvider;

    public UpdateRouter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task RouteUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        // 1. Обработка кликов по инлайн-кнопкам удаления
        if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery is { } callbackQuery)
        {
            await HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
            return;
        }

        // Работаем только с текстовыми сообщениями
        if (update.Message is not { Text: { } messageText } message) return;
        var chatId = message.Chat.Id;

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Находим или создаем сессию пользователя в базе
        var session = dbContext.UserSessions.FirstOrDefault(s => s.ChatId == chatId);
        if (session == null)
        {
            session = new UserSession { ChatId = chatId, CurrentState = UserState.None };
            dbContext.UserSessions.Add(session);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Если нажата глобальная команда или кнопка сброса — прерываем любой шаг
        if (messageText.StartsWith("/start"))
        {
            session.CurrentState = UserState.None;
            await dbContext.SaveChangesAsync(cancellationToken);
            await SendMainMenuAsync(botClient, chatId, "👋 Добро пожаловать в **SubTrack**!\n\nИспользуй меню внизу экрана для управления расходами.", cancellationToken);
            return;
        }

        // СЛУШАЕМ ГЛАВНОЕ МЕНЮ (Если состояние None)
        if (session.CurrentState == UserState.None)
        {
            if (messageText == "📋 Мои подписки")
            {
                await ShowSubscriptionsAsync(botClient, dbContext, chatId, cancellationToken);
            }
            else if (messageText == "➕ Добавить подписку")
            {
                session.CurrentState = UserState.WaitingForName;
                await dbContext.SaveChangesAsync(cancellationToken);
                await botClient.SendMessage(chatId, "📝 Введи **название** подписки (например: `Netflix`):", parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
            }
            else if (messageText == "🧹 Очистить всё")
            {
                var all = dbContext.Subscriptions.ToList();
                dbContext.Subscriptions.RemoveRange(all);
                await dbContext.SaveChangesAsync(cancellationToken);
                await botClient.SendMessage(chatId, "🧹 Все подписки удалены!", cancellationToken: cancellationToken);
            }
            return;
        }

        // МАШИНА СОСТОЯНИЙ (Пошаговый конструктор)
        switch (session.CurrentState)
        {
            case UserState.WaitingForName:
                session.TempName = messageText;
                session.CurrentState = UserState.WaitingForPrice;
                await dbContext.SaveChangesAsync(cancellationToken);
                await botClient.SendMessage(chatId, $"💵 Отлично! Теперь введи **стоимость** для '{messageText}' (только число цифрами):", parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
                break;

            case UserState.WaitingForPrice:
                if (!decimal.TryParse(messageText, out decimal price))
                {
                    await botClient.SendMessage(chatId, "⚠️ Пожалуйста, введи корректное число. Пример: `399` или `250.50`:", cancellationToken: cancellationToken);
                    return;
                }
                session.TempPrice = price;
                session.CurrentState = UserState.WaitingForDate;
                await dbContext.SaveChangesAsync(cancellationToken);
                await botClient.SendMessage(chatId, "📅 Введи день ежемесячного списания цифрой (от `1` до `31`):", cancellationToken: cancellationToken);
                break;

            case UserState.WaitingForDate:
                if (!int.TryParse(messageText, out int day) || day < 1 || day > 31)
                {
                    await botClient.SendMessage(chatId, "⚠️ Введи число от 1 до 31:", cancellationToken: cancellationToken);
                    return;
                }

                // Вычисляем ближайшую дату оплаты
                var now = DateTime.UtcNow;
                var paymentDate = new DateTime(now.Year, now.Month, day, 0, 0, 0, DateTimeKind.Utc);
                if (paymentDate < now) paymentDate = paymentDate.AddMonths(1);

                // Сохраняем готовую подписку в Docker!
                var newSub = new Subscription
                {
                    Id = Guid.NewGuid(),
                    Name = session.TempName!,
                    Price = session.TempPrice!.Value,
                    Currency = "RUB",
                    NextPaymentDate = paymentDate
                };

                dbContext.Subscriptions.Add(newSub);

                // Сбрасываем сессию в исходное состояние
                session.CurrentState = UserState.None;
                session.TempName = null;
                session.TempPrice = null;

                await dbContext.SaveChangesAsync(cancellationToken);

                await SendMainMenuAsync(botClient, chatId, $"🎉 **Подписка успешно сохранена!**\n\n🔹 Название: *{newSub.Name}*\n💰 Цена: *{newSub.Price} RUB*\n📅 Дата списания: *{newSub.NextPaymentDate.ToLocalTime():dd.MM.yyyy}*", cancellationToken);
                break;
        }
    }

    private async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var data = callbackQuery.Data;
        var chatId = callbackQuery.Message!.Chat.Id;

        if (data != null && data.StartsWith("del_"))
        {
            var subIdStr = data.Replace("del_", "");
            if (Guid.TryParse(subIdStr, out Guid subId))
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var sub = dbContext.Subscriptions.FirstOrDefault(s => s.Id == subId);

                if (sub != null)
                {
                    dbContext.Subscriptions.Remove(sub);
                    await dbContext.SaveChangesAsync(cancellationToken);

                    await botClient.AnswerCallbackQuery(callbackQuery.Id, $"🗑 {sub.Name} удалена!", cancellationToken: cancellationToken);
                    await botClient.SendMessage(chatId, $"🗑 Подписка *{sub.Name}* удалена из базы данных.", parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
                }
            }
        }
    }

    private async Task ShowSubscriptionsAsync(ITelegramBotClient botClient, ApplicationDbContext dbContext, long chatId, CancellationToken cancellationToken)
    {
        var subscriptions = dbContext.Subscriptions.ToList();

        if (!subscriptions.Any())
        {
            await botClient.SendMessage(chatId, "У тебя пока нет активных подписок.", cancellationToken: cancellationToken);
            return;
        }

        await botClient.SendMessage(chatId, "📋 *Список твоих подписок:*", parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);

        foreach (var sub in subscriptions)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[] { InlineKeyboardButton.WithCallbackData("❌ Удалить подписку", $"del_{sub.Id}") });
            var subInfo = $"🔹 *{sub.Name}*\n💰 Цена: {sub.Price} {sub.Currency}\n📅 Списание: {sub.NextPaymentDate.ToLocalTime():dd.MM.yyyy}";

            await botClient.SendMessage(chatId, subInfo, replyMarkup: inlineKeyboard, parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
        }
    }

    private async Task SendMainMenuAsync(ITelegramBotClient botClient, long chatId, string text, CancellationToken cancellationToken)
    {
        var replyKeyboard = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "📋 Мои подписки", "➕ Добавить подписку" },
            new KeyboardButton[] { "🧹 Очистить всё" }
        })
        { ResizeKeyboard = true };

        await botClient.SendMessage(chatId, text, replyMarkup: replyKeyboard, parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
    }
}