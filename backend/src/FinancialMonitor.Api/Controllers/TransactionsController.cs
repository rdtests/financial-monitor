using FinancialMonitor.Api.Contracts;
using FinancialMonitor.Api.Models;
using FinancialMonitor.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinancialMonitor.Api.Controllers;

/// <summary>HTTP only: maps the contract to the domain and the outcome to a status code.</summary>
[ApiController]
[Route("api/transactions")]
public class TransactionsController(ITransactionService service) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<IngestResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<IngestResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IngestResponse>> Post(TransactionRequest request, CancellationToken cancellationToken)
    {
        var transaction = request.ToDomain();
        var result = await service.ProcessAsync(transaction, cancellationToken);
        var response = new IngestResponse(transaction.TransactionId, result.ToString());

        // 201 without a Location header: there is no GET-by-id endpoint to point to.
        return result == UpsertResult.Created
            ? StatusCode(StatusCodes.Status201Created, response)
            : Ok(response);
    }

    [HttpGet]
    public IEnumerable<TransactionResponse> Get() =>
        service.GetLatest().Select(transaction => transaction.ToResponse());
}
