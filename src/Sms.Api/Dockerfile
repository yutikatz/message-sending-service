# ---- build / test ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore in its own layer so code edits do not invalidate the package cache.
COPY SmsService.sln ./
COPY src/Sms.Api/Sms.Api.csproj src/Sms.Api/
COPY tests/Sms.Tests/Sms.Tests.csproj tests/Sms.Tests/
RUN dotnet restore

COPY . .
RUN dotnet publish src/Sms.Api/Sms.Api.csproj -c Release -o /app/publish --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Non-root user: standard container hygiene.
RUN useradd --create-home --uid 1001 appuser && chown -R appuser /app
USER appuser

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Docker
EXPOSE 8080
ENTRYPOINT ["dotnet", "Sms.Api.dll"]
