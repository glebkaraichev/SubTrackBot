using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SubTrack.Api.Services;

public class TelegramBotWorker : BackgroundService
{
    private readonly ILogger<TelegramBotWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly UpdateRouter _updateRouter;

    public TelegramBotWorker(ILogger<TelegramBotWorker> logger, IConfiguration configuration, UpdateRouter updateRouter)
    {
        _logger = logger;
        _configuration = configuration;
        _updateRouter = updateRouter;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var token = _configuration["BotConfiguration:BotToken"];
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogCritical("Токен бота не найден в конфигурации!");
            return;
        }

        var botClient = new TelegramBotClient(token);

        try
        {
            var me = await botClient.GetMe(cancellationToken: stoppingToken);
            _logger.LogInformation("Бот @{BotUsername} запущен и готов к работе!", me.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось подключиться к Telegram API");
            return;
        }

        // --- САМОЕ ВАЖНОЕ: Логирование каждого сообщения ---
        await botClient.ReceiveAsync(
            updateHandler: async (bot, upd, ct) =>
            {
                // Если пришло сообщение, выведем его в логи Render
                if (upd.Message != null)
                {
                    Console.WriteLine($"DEBUG: Получено сообщение от {upd.Message.From?.Username}: {upd.Message.Text}");
                }

                await _updateRouter.RouteUpdateAsync(bot, upd, ct);
            },
            errorHandler: (bot, ex, ct) =>
            {
                _logger.LogError(ex, "Ошибка Поллинга");
                return Task.CompletedTask;
            },
            receiverOptions: new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() },
            cancellationToken: stoppingToken
        );
    }
}