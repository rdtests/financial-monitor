using Microsoft.AspNetCore.SignalR;

namespace FinancialMonitor.Api.Hubs;

/// <summary>
/// SignalR endpoint the dashboard connects to. Clients only listen; the server pushes
/// through IHubContext, so the hub needs no methods of its own.
/// </summary>
public class TransactionsHub : Hub
{
    public const string TransactionUpdatedEvent = "TransactionUpdated";
}
