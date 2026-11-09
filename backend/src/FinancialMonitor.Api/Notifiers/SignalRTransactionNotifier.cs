using FinancialMonitor.Api.Contracts;
using FinancialMonitor.Api.Hubs;
using FinancialMonitor.Api.Models;
using Microsoft.AspNetCore.SignalR;

namespace FinancialMonitor.Api.Notifiers;

/// <summary>
/// Broadcasts to all clients through IHubContext, which is thread-safe and manages the connections itself,
/// so concurrent requests can call this without any locking of our own.
/// </summary>
public class SignalRTransactionNotifier(IHubContext<TransactionsHub> hubContext) : ITransactionNotifier
{
    // Sends the API contract, not the domain model: the same shape GET /api/transactions returns.
    public Task NotifyAsync(Transaction transaction, CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync(
            TransactionsHub.TransactionUpdatedEvent, transaction.ToResponse(), cancellationToken);
}
