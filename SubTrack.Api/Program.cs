using MediatR;
using Microsoft.EntityFrameworkCore;
using SubTrack.Api.Services;
using SubTrack.Infrastructure.Persistence;
using Quartz;
using SubTrack.Api.Jobs;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. СЕРВИСЫ (Регистрация зависимостей в DI)
// ==========================================

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Регистрация бота и его маршрутизатора
builder.Services.AddHostedService<TelegramBotWorker>();
builder.Services.AddSingleton<UpdateRouter>();

// РЕШЕНИЕ ОШИБКИ QUARTZ: Регистрируем ITelegramBotClient глобально в DI-контейнере
builder.Services.AddSingleton<ITelegramBotClient>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var token = configuration["BotConfiguration:BotToken"];
    if (string.IsNullOrEmpty(token))
    {
        throw new InvalidOperationException("Telegram Bot Token отсутствует в конфигурации appsettings.json!");
    }
    return new TelegramBotClient(token);
});

// Регистрация MediatR
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(SubTrack.Application.Subscriptions.Commands.CreateSubscriptionCommand).Assembly);
});

// Настройка подключения к PostgreSQL в Docker
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Связываем интерфейс контекста базы данных с реализацией
builder.Services.AddScoped<SubTrack.Application.Common.IApplicationDbContext>(provider =>
    provider.GetRequiredService<ApplicationDbContext>());

// Настройка планировщика задач Quartz
builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("NotificationJob");
    q.AddJob<NotificationJob>(opts => opts.WithIdentity(jobKey));

    // Проверка каждую минуту (для тестирования уведомлений)
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("NotificationJob-trigger")
        .WithCronSchedule("0 * * ? * * *"));
});

// Запуск Quartz как фоновой службы
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// ==========================================
// 2. СБОРКА И НАСТРОЙКА ПАЙПЛАЙНА (Middleware)
// ==========================================

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        options.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();