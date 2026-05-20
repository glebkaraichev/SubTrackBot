using Microsoft.EntityFrameworkCore;
using SubTrack.Api.Services;
using SubTrack.Infrastructure.Persistence;
using Quartz;
using SubTrack.Api.Jobs;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

// 1. СЕРВИСЫ
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHostedService<TelegramBotWorker>();
builder.Services.AddSingleton<UpdateRouter>();

builder.Services.AddSingleton<ITelegramBotClient>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var token = configuration["BotConfiguration:BotToken"]
                ?? throw new InvalidOperationException("Telegram Bot Token не найден в конфигурации!");
    return new TelegramBotClient(token);
});

builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(SubTrack.Application.Subscriptions.Commands.CreateSubscriptionCommand).Assembly);
});

// 2. БЕЗОПАСНАЯ НАСТРОЙКА БД (УНИВЕРСАЛЬНЫЙ ПАРСЕР)
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
                       ?? builder.Configuration.GetConnectionString("DefaultConnection");

Console.WriteLine($"DEBUG: Полученная строка подключения: {connectionString?.Substring(0, Math.Min(15, connectionString?.Length ?? 0))}...");

if (string.IsNullOrEmpty(connectionString))
    throw new Exception("КРИТИЧЕСКАЯ ОШИБКА: Строка подключения пуста!");

// Если Render передает строку в формате postgresql://, преобразуем её
if (connectionString.StartsWith("postgresql://"))
{
    var uri = new Uri(connectionString);
    var username = uri.UserInfo.Split(':')[0];
    var password = uri.UserInfo.Split(':')[1];
    connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.LocalPath.Substring(1)};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<SubTrack.Application.Common.IApplicationDbContext>(provider =>
    provider.GetRequiredService<ApplicationDbContext>());

// Quartz
builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("NotificationJob");
    q.AddJob<NotificationJob>(opts => opts.WithIdentity(jobKey));
    q.AddTrigger(opts => opts.ForJob(jobKey).WithIdentity("NotificationJob-trigger").WithCronSchedule("0 * * ? * * *"));
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var app = builder.Build();

// 3. ПАЙПЛАЙН
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// 4. ПРИМЕНЕНИЕ МИГРАЦИЙ
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        Console.WriteLine("Попытка применения миграций...");
        db.Database.Migrate();
        Console.WriteLine("База данных успешно обновлена!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"КРИТИЧЕСКАЯ ОШИБКА МИГРАЦИИ: {ex.Message}");
    }
}

app.Run();