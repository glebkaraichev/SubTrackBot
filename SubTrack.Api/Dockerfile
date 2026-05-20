# Этап 1: Базовый образ для запуска приложения
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Этап 2: Образ для сборки исходного кода
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем файлы проектов для восстановления зависимостей (NuGet пакетов)
COPY ["SubTrack.Api/SubTrack.Api.csproj", "SubTrack.Api/"]
COPY ["SubTrack.Domain/SubTrack.Domain.csproj", "SubTrack.Domain/"]
COPY ["SubTrack.Infrastructure/SubTrack.Infrastructure.csproj", "SubTrack.Infrastructure/"]
COPY ["SubTrack.Application/SubTrack.Application.csproj", "SubTrack.Application/"]

# Восстанавливаем зависимости
RUN dotnet restore "SubTrack.Api/SubTrack.Api.csproj"

# Копируем абсолютно весь исходный код и собираем проект
COPY . .
WORKDIR "/src/SubTrack.Api"
RUN dotnet build "SubTrack.Api.csproj" -c Release -o /app/build

# Этап 3: Публикация готовых бинарников
FROM build AS publish
RUN dotnet publish "SubTrack.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Этап 4: Финальный минимальный образ для продакшена
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SubTrack.Api.dll"]