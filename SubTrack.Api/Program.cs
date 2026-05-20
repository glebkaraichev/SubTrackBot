using Microsoft.EntityFrameworkCore;
using SubTrack.Api.Services;
using SubTrack.Infrastructure.Persistence;
using Quartz;
using SubTrack.Api.Jobs;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHostedService<TelegramBotWorker>();
builder.Services.AddSingleton<UpdateRouter>();

// Токен бота
builder.Services.AddSingleton<ITelegramBotClient>(provider =>
{
    var token = builder.Configuration["BotConfiguration:BotToken"]
                ?? throw new Exception("Токен не найден!");
    return new TelegramBotClient(token);
});

// Настройка БД: максимально простая
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory")));

builder.Services.AddScoped<SubTrack.Application.Common.IApplicationDbContext>(provider =>
    provider.GetRequiredService<ApplicationDbContext>());

// Quartz
builder.Services.AddQuartz(q => {
    q.AddJob<NotificationJob>(opts => opts.WithIdentity("NotificationJob"));
    q.AddTrigger(opts => opts.ForJob("NotificationJob").WithCronSchedule("0 * * ? * * *"));
});
builder.Services.AddQuartzHostedService();

var app = builder.Build();

// Применение миграций
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try { db.Database.Migrate(); }
    catch (Exception ex) { Console.WriteLine($"Ошибка миграции (может быть нормально): {ex.Message}"); }
}

app.MapControllers();
app.Run();