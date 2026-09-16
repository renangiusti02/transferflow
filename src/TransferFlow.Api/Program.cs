using Microsoft.EntityFrameworkCore;
using TransferFlow.Application.Common;
using TransferFlow.Application.Messaging;
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

builder.Services.AddOutboxProcessing(
    builder.Configuration);

builder.Services.AddDbContext<TransferFlowDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IOutbox, EfOutbox>();

builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();

builder.Services.AddScoped<IWalletRepository, WalletRepository>();
builder.Services.AddScoped<ITransferRepository, TransferRepository>();

builder.Services.AddScoped<CreateWalletUseCase>();
builder.Services.AddScoped<GetWalletByIdUseCase>();
builder.Services.AddScoped<CreateTransferUseCase>();
builder.Services.AddScoped<GetTransferByIdUseCase>();

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
