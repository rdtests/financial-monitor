namespace FinancialMonitor.Api.Models;

/// <summary>What the repository did with an incoming transaction.</summary>
public enum UpsertResult
{
    Created,
    Updated,
    IgnoredStale
}
