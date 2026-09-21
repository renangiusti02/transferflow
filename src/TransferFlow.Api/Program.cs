using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using TransferFlow.Api.Health;
using TransferFlow.Api.Middleware;
using TransferFlow.Api.Observability;
using TransferFlow.Application.Common;
using TransferFlow.Application.Messaging;
using TransferFlow.Application.Observability;
using TransferFlow.Application.Transfers;
using TransferFlow.Application.Wallets;
using TransferFlow.Infrastructure.Messaging.Outbox;
using TransferFlow.Infrastructure.Persistence;
using TransferFlow.Infrastructure.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException(
        "Connection string 'Database' was not found.");

builder.Services.AddOutboxProcessing(builder.Configuration);

builder.Services.AddDbContext<TransferFlowDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICorrelationContext,HttpCorrelationContext>();

builder.Services.AddScoped<IOutbox, EfOutbox>();

builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();

builder.Services.AddScoped<IWalletRepository, WalletRepository>();
builder.Services.AddScoped<ITransferRepository, TransferRepository>();

builder.Services.AddScoped<CreateWalletUseCase>();
builder.Services.AddScoped<GetWalletByIdUseCase>();
builder.Services.AddScoped<CreateTransferUseCase>();
builder.Services.AddScoped<GetTransferByIdUseCase>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<TransferFlowDbContext>(
        name: "postgresql",
        tags: ["ready"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseMiddleware<CorrelationIdMiddleware>();

app.MapControllers();

app.MapHealthChecks(
    "/health",
    new HealthCheckOptions
    {
    Predicate =
        check => check.Tags.Contains("ready"),

        ResponseWriter =
            HealthCheckResponseWriter.WriteAsync
    });

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = _ => false
    });

app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate =
            check => check.Tags.Contains("ready"),

        ResponseWriter =
            HealthCheckResponseWriter.WriteAsync
    });

app.Run();
