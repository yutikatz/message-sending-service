# ---- build ----
    FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
    WORKDIR /src
    
    # Kill any fallback folder inherited from a Windows host.
    ENV NUGET_FALLBACK_PACKAGES=""
    ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
    ENV DOTNET_NOLOGO=1
    
    # Copy ONLY the project file first: restore layer stays cached on code edits.
    COPY src/Sms.Api/Sms.Api.csproj src/Sms.Api/
    RUN dotnet restore src/Sms.Api/Sms.Api.csproj
    
    # Now the code. bin/ and obj/ are excluded by .dockerignore - this is essential.
    COPY . .
    RUN dotnet publish src/Sms.Api/Sms.Api.csproj -c Release -o /app/publish --no-restore
    
    # ---- runtime ----
    FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
    WORKDIR /app
    COPY --from=build /app/publish .
    
    RUN useradd --create-home --uid 1001 appuser && chown -R appuser /app
    USER appuser
    
    ENV ASPNETCORE_URLS=http://+:8080 \
        ASPNETCORE_ENVIRONMENT=Docker
    EXPOSE 8080
    ENTRYPOINT ["dotnet", "Sms.Api.dll"]