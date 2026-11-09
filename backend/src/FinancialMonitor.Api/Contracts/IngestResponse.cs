namespace FinancialMonitor.Api.Contracts;

/// <summary>Body of the POST response: which transaction was processed and what happened to it.</summary>
/// <param name="Outcome">"Created", "Updated" or "IgnoredStale".</param>
public record IngestResponse(Guid TransactionId, string Outcome);
