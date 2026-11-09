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
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Singletons: the repository holds the shared in-memory state; service and notifier are stateless.
// Bounded so memory cannot grow without limit (in Kubernetes that would end in OOMKilled).
var capacity = builder.Configuration.GetValue("Transactions:Capacity", InMemoryTransactionRepository.DefaultCapacity);
builder.Services.AddSingleton<ITransactionRepository>(new InMemoryTransactionRepository(capacity));
builder.Services.AddSingleton<ITransactionNotifier, SignalRTransactionNotifier>();
builder.Services.AddSingleton<ITransactionService, TransactionService>();

var app = builder.Build();

// One global handler instead of try/catch in controllers: logs the exception and returns a generic
// 500 ProblemDetails with a traceId, without exception details. In Development, .NET 8 shows the
// Developer Exception Page (with stack trace) instead, which is intended for local debugging.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// In the container the built frontend is copied into wwwroot, so the API and the UI share one origin
// (no CORS). In development wwwroot is empty and Vite serves the UI.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapHub<TransactionsHub>("/hubs/transactions");
app.MapHealthChecks("/healthz");

// Client-side routes (/monitor, /add) are handled by React Router, so any other path gets index.html.
// Unknown /api paths must stay a 404 for API clients rather than returning the HTML page.
app.Map("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();

// Makes the top-level Program class visible to WebApplicationFactory<Program> in the test project.
public partial class Program;
