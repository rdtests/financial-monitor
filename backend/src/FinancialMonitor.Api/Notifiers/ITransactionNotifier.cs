using FinancialMonitor.Api.Models;

namespace FinancialMonitor.Api.Notifiers;

/// <summary>Pushes a transaction change to connected dashboard clients.</summary>
public interface ITransactionNotifier
{
    Task NotifyAsync(Transaction transaction, CancellationToken cancellationToken);
}
