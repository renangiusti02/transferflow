using Microsoft.EntityFrameworkCore;
using TransferFlow.Application.Wallets;
using TransferFlow.Infrastructure.Persistence;
using TransferFlow.Infrastructure.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException(
        "Connection string 'Database' was not found.");

builder.Services.AddDbContext<TransferFlowDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IWalletRepository, WalletRepository>();

builder.Services.AddScoped<CreateWalletUseCase>();
builder.Services.AddScoped<GetWalletByIdUseCase>();

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
