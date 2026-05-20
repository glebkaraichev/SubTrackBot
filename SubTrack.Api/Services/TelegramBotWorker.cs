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
    private TelegramBotClient? _botClient;

    public TelegramBotWorker(ILogger<TelegramBotWorker> logger, IConfiguration configuration, UpdateRouter updateRouter)
    {
        _logger = logger;
        _configuration = configuration;
        _updateRouter = updateRouter; // Внедряем наш роутер
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var token = _configuration["BotConfiguration:BotToken"];
        if (string.IsNullOrEmpty(token)) return;

        _botClient = new TelegramBotClient(token);
        var me = await _botClient.GetMe(cancellationToken: stoppingToken);
        _logger.LogInformation("Бот @{BotUsername} запущен по архитектуре State Machine!", me.Username);

        await _botClient.ReceiveAsync(
            updateHandler: async (bot, upd, ct) => await _updateRouter.RouteUpdateAsync(bot, upd, ct),
            errorHandler: (bot, ex, ct) => { _logger.LogError(ex, "Ошибка Поллинга"); return Task.CompletedTask; },
            receiverOptions: new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() },
            cancellationToken: stoppingToken
        );
    }
}