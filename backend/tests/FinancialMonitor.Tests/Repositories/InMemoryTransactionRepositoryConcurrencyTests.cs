using System.Collections.Concurrent;
using FinancialMonitor.Api.Models;
using FinancialMonitor.Api.Repositories;

namespace FinancialMonitor.Tests.Repositories;

/// <summary>
/// Race conditions are probabilistic, so these tests are built to make them show up reliably:
/// all threads start together on a Barrier, each scenario performs many operations and repeats many rounds,
/// and a timeout turns a hang (possible when a plain Dictionary is written concurrently) into a clear failure.
/// </summary>
public class InMemoryTransactionRepositoryConcurrencyTests
{
    private static readonly DateTimeOffset BaseTime = new(2024, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly int ThreadCount = Math.Max(8, Environment.ProcessorCount);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public void Parallel_upserts_of_different_ids_store_every_transaction()
    {
        const int rounds = 20;
        const int perThread = 1000;

        for (var round = 0; round < rounds; round++)
        {
            var repository = new InMemoryTransactionRepository(capacity: 1_000_000);

            RunConcurrently(_ =>
            {
                for (var i = 0; i < perThread; i++)
                    repository.Upsert(CreateTransaction(Guid.NewGuid(), BaseTime.AddSeconds(i)));
            });

            repository.GetLatest(int.MaxValue).Count.ShouldBe(ThreadCount * perThread, $"round {round}");
        }
    }

    [Fact]
    public void Parallel_updates_of_the_same_id_keep_the_newest_timestamp()
    {
        const int rounds = 200;
        const int updates = 100;

        for (var round = 0; round < rounds; round++)
        {
            var repository = new InMemoryTransactionRepository();
            var id = Guid.NewGuid();
            var timestamps = Enumerable.Range(0, updates)
                .Select(i => BaseTime.AddSeconds(i))
                .OrderBy(_ => Random.Shared.Next())
                .ToArray();

            RunConcurrently(thread =>
            {
                for (var i = thread; i < timestamps.Length; i += ThreadCount)
                    repository.Upsert(CreateTransaction(id, timestamps[i]));
            });

            repository.GetLatest(10).Single().Timestamp.ShouldBe(BaseTime.AddSeconds(updates - 1), $"round {round}");
        }
    }

    [Fact]
    public void Reads_during_writes_never_throw_or_return_a_broken_item()
    {
        const int rounds = 10;
        const int operations = 500;

        for (var round = 0; round < rounds; round++)
        {
            // Default capacity: writers fill it and keep evicting, so eviction is exercised concurrently too.
            var repository = new InMemoryTransactionRepository();

            RunConcurrently(thread =>
            {
                for (var i = 0; i < operations; i++)
                {
                    if (thread % 2 == 0)
                    {
                        repository.Upsert(CreateTransaction(Guid.NewGuid(), BaseTime.AddSeconds(i)));
                    }
                    else
                    {
                        foreach (var transaction in repository.GetLatest(100))
                        {
                            transaction.ShouldNotBeNull();
                            transaction.Currency.ShouldBe("USD");
                        }
                    }
                }
            });
        }
    }

    [Fact]
    public void Same_new_id_from_many_threads_is_Created_exactly_once()
    {
        const int rounds = 200;

        for (var round = 0; round < rounds; round++)
        {
            var repository = new InMemoryTransactionRepository();
            var id = Guid.NewGuid();
            var results = new ConcurrentBag<UpsertResult>();

            RunConcurrently(thread => results.Add(repository.Upsert(CreateTransaction(id, BaseTime.AddSeconds(thread)))));

            results.Count(r => r == UpsertResult.Created).ShouldBe(1, $"round {round}");
        }
    }

    /// <summary>
    /// Starts <see cref="ThreadCount"/> threads that wait on a Barrier and then run <paramref name="work"/>
    /// at the same moment. Rethrows anything they threw, and fails if they do not finish in time.
    /// </summary>
    private static void RunConcurrently(Action<int> work)
    {
        using var barrier = new Barrier(ThreadCount);
        var exceptions = new ConcurrentQueue<Exception>();

        var threads = Enumerable.Range(0, ThreadCount)
            .Select(index => new Thread(() =>
            {
                try
                {
                    barrier.SignalAndWait();
                    work(index);
                }
                catch (Exception ex)
                {
                    exceptions.Enqueue(ex);
                }
            }) { IsBackground = true }) // a hung thread must not keep the test process alive
            .ToList();

        threads.ForEach(t => t.Start());

        var deadline = DateTime.UtcNow + Timeout;
        foreach (var thread in threads)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining < TimeSpan.Zero || !thread.Join(remaining))
                throw new TimeoutException($"Threads did not finish within {Timeout} (likely stuck in a corrupted collection).");
        }

        if (!exceptions.IsEmpty)
            throw new AggregateException(exceptions);
    }

    private static Transaction CreateTransaction(Guid id, DateTimeOffset timestamp) =>
        new(id, 10m, "USD", TransactionStatus.Pending, timestamp);
}
