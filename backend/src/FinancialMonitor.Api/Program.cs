using FinancialMonitor.Api.Contracts;
using FinancialMonitor.Api.Hubs;
using FinancialMonitor.Api.Notifiers;
using FinancialMonitor.Api.Repositories;
using FinancialMonitor.Api.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
        // Validation errors are keyed by the JSON name the client sent ("amount"), not the C# name ("Amount").
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()))
    .AddJsonOptions(options => ApiJson.Configure(options.JsonSerializerOptions));
builder.Services.AddSignalR()
    .AddJsonProtocol(options => ApiJson.Configure(options.PayloadSerializerOptions));
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Singletons: the repository holds the shared in-memory state; service and notifier are stateless.
builder.Services.AddSingleton<ITransactionRepository, InMemoryTransactionRepository>();
builder.Services.AddSingleton<ITransactionNotifier, SignalRTransactionNotifier>();
builder.Services.AddSingleton<ITransactionService, TransactionService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHub<TransactionsHub>("/hubs/transactions");
app.MapHealthChecks("/healthz");

app.Run();

// Makes the top-level Program class visible to WebApplicationFactory<Program> in the test project.
public partial class Program;
