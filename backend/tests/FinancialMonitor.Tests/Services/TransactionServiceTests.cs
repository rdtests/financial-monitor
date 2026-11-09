using FinancialMonitor.Api.Models;
using FinancialMonitor.Api.Notifiers;
using FinancialMonitor.Api.Repositories;
using FinancialMonitor.Api.Services;
using NSubstitute;

namespace FinancialMonitor.Tests.Services;

public class TransactionServiceTests
{
    private readonly ITransactionRepository _repository = Substitute.For<ITransactionRepository>();
    private readonly ITransactionNotifier _notifier = Substitute.For<ITransactionNotifier>();
    private readonly TransactionService _service;

    private readonly Transaction _transaction = new(
        Guid.NewGuid(), 1500.50m, "USD", TransactionStatus.Pending,
        new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero));

    public TransactionServiceTests()
    {
        _service = new TransactionService(_repository, _notifier);
    }

    [Fact]
    public async Task Created_transaction_is_stored_and_broadcast_once()
    {
        _repository.Upsert(_transaction).Returns(UpsertResult.Created);

        var result = await _service.ProcessAsync(_transaction, CancellationToken.None);

        result.ShouldBe(UpsertResult.Created);
        _repository.Received(1).Upsert(_transaction);
        await _notifier.Received(1).NotifyAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Updated_transaction_is_broadcast()
    {
        _repository.Upsert(_transaction).Returns(UpsertResult.Updated);

        var result = await _service.ProcessAsync(_transaction, CancellationToken.None);

        result.ShouldBe(UpsertResult.Updated);
        await _notifier.Received(1).NotifyAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Broadcast_data_is_exactly_what_was_stored()
    {
        Transaction? stored = null;
        Transaction? broadcast = null;
        _repository.Upsert(Arg.Do<Transaction>(t => stored = t)).Returns(UpsertResult.Created);
        await _notifier.NotifyAsync(Arg.Do<Transaction>(t => broadcast = t), Arg.Any<CancellationToken>());
        _notifier.ClearReceivedCalls();

        await _service.ProcessAsync(_transaction, CancellationToken.None);

        stored.ShouldBe(_transaction);
        broadcast.ShouldBe(stored);
    }
}
