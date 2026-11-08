using Microsoft.EntityFrameworkCore;
using Sms.Api.Data;
using Sms.Api.Endpoints;
using Sms.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// DB: SQLite by default so the app boots with zero setup.
// Database:Provider=SqlServer switches to SQL Server (docker-compose sets this).
var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var conn = builder.Configuration.GetConnectionString("Default")
           ?? "Data Source=smsservice.db";

builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)) o.UseSqlServer(conn);
    else o.UseSqlite(conn);
});

builder.Services.AddScoped<ISmsProvider, MockSmsProvider>();
builder.Services.AddScoped<MessageService>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Create the schema and seed demo data on boot (idempotent).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    SeedData.EnsureSeeded(db);
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.MapMessageEndpoints();
app.Run();

// Exposed for WebApplicationFactory in integration tests.
public partial class Program { }
