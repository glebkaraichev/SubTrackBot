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
                ?? throw new InvalidOperationException("Telegram Bot Token не найден!");
    return new TelegramBotClient(token);
});

builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(SubTrack.Application.Subscriptions.Commands.CreateSubscriptionCommand).Assembly);
});

// Настройка подключения
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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

// 2. ПАЙПЛАЙН (Middleware)
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// 3. БЕЗОПАСНАЯ ПРИМЕНЕНИЕ МИГРАЦИЙ
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    int retries = 10;
    while (retries > 0)
    {
        try
        {
            Console.WriteLine("Попытка подключения к базе данных...");
            db.Database.Migrate();
            Console.WriteLine("База данных успешно обновлена!");
            break;
        }
        catch (Exception ex)
        {
            retries--;
            Console.WriteLine($"Ошибка БД: {ex.Message}. Осталось попыток: {retries}. Ждем 10 секунд...");
            Thread.Sleep(10000);
        }
    }
}

app.Run();