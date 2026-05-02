using FlowEncode.Application;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlowEncode.Domain.Tests;

[TestClass]
public sealed class LatestRequestSchedulerTests
{
    [TestMethod]
    public async Task ScheduleAsync_WhenRequestsArriveWhileBusy_RunsFirstThenLatestOnly()
    {
        var executedRequests = new List<int>();
        var firstStarted = CreateSignal();
        var releaseFirst = CreateSignal();
        var latestExecuted = CreateSignal();

        using var scheduler = new LatestRequestScheduler<int>(async scheduledRequest =>
        {
            executedRequests.Add(scheduledRequest.Request);

            if (scheduledRequest.Request == 1)
            {
                firstStarted.TrySetResult();
                await releaseFirst.Task;
            }

            if (scheduledRequest.Request == 3)
            {
                latestExecuted.TrySetResult();
            }
        });

        var firstExecution = scheduler.ScheduleAsync(1);
        await firstStarted.Task;
        await scheduler.ScheduleAsync(2);
        await scheduler.ScheduleAsync(3);
        releaseFirst.TrySetResult();

        await latestExecuted.Task;
        await firstExecution;

        CollectionAssert.AreEqual(new[] { 1, 3 }, executedRequests);
    }

    [TestMethod]
    public async Task ScheduleAsync_WhenLatestRequestMatchesCurrent_DropsDuplicatePendingRequest()
    {
        var executedRequests = new List<int>();
        var firstStarted = CreateSignal();
        var releaseFirst = CreateSignal();
        var currentRequestStillMatchedLatest = false;

        using var scheduler = new LatestRequestScheduler<int>(async scheduledRequest =>
        {
            executedRequests.Add(scheduledRequest.Request);

            if (scheduledRequest.Request == 42)
            {
                firstStarted.TrySetResult();
                await releaseFirst.Task;
                currentRequestStillMatchedLatest = scheduledRequest.MatchesLatestRequest;
            }
        });

        var firstExecution = scheduler.ScheduleAsync(42);
        await firstStarted.Task;
        await scheduler.ScheduleAsync(42);
        releaseFirst.TrySetResult();

        await firstExecution;

        CollectionAssert.AreEqual(new[] { 42 }, executedRequests);
        Assert.IsTrue(currentRequestStillMatchedLatest);
    }

    [TestMethod]
    public async Task ClearPending_WhenCalledWhileBusy_DropsQueuedRequest()
    {
        var executedRequests = new List<int>();
        var firstStarted = CreateSignal();
        var releaseFirst = CreateSignal();

        using var scheduler = new LatestRequestScheduler<int>(async scheduledRequest =>
        {
            executedRequests.Add(scheduledRequest.Request);

            if (scheduledRequest.Request == 1)
            {
                firstStarted.TrySetResult();
                await releaseFirst.Task;
            }
        });

        var firstExecution = scheduler.ScheduleAsync(1);
        await firstStarted.Task;
        await scheduler.ScheduleAsync(2);
        scheduler.ClearPending();
        releaseFirst.TrySetResult();

        await firstExecution;

        CollectionAssert.AreEqual(new[] { 1 }, executedRequests);
    }

    [TestMethod]
    public async Task ScheduleAsync_DoesNotExecuteInParallel()
    {
        var firstStarted = CreateSignal();
        var releaseFirst = CreateSignal();
        var secondCompleted = CreateSignal();
        var concurrentExecutions = 0;
        var maxConcurrentExecutions = 0;

        using var scheduler = new LatestRequestScheduler<int>(async scheduledRequest =>
        {
            var currentConcurrentExecutions = Interlocked.Increment(ref concurrentExecutions);
            maxConcurrentExecutions = Math.Max(maxConcurrentExecutions, currentConcurrentExecutions);

            try
            {
                if (scheduledRequest.Request == 1)
                {
                    firstStarted.TrySetResult();
                    await releaseFirst.Task;
                }
                else
                {
                    secondCompleted.TrySetResult();
                }
            }
            finally
            {
                Interlocked.Decrement(ref concurrentExecutions);
            }
        });

        var firstExecution = scheduler.ScheduleAsync(1);
        await firstStarted.Task;
        await scheduler.ScheduleAsync(2);
        releaseFirst.TrySetResult();

        await secondCompleted.Task;
        await firstExecution;

        Assert.AreEqual(1, maxConcurrentExecutions);
    }

    [TestMethod]
    public async Task ScheduleAsync_WhenExecutionThrows_ClearsBusyStateAndAllowsRescheduling()
    {
        var executedRequests = new List<int>();

        using var scheduler = new LatestRequestScheduler<int>(scheduledRequest =>
        {
            executedRequests.Add(scheduledRequest.Request);

            if (scheduledRequest.Request == 1)
            {
                throw new InvalidOperationException("boom");
            }

            return Task.CompletedTask;
        });

        await AssertThrowsAsync<InvalidOperationException>(
            () => scheduler.ScheduleAsync(1));

        Assert.IsFalse(scheduler.IsBusy);

        await scheduler.ScheduleAsync(2);

        CollectionAssert.AreEqual(new[] { 1, 2 }, executedRequests);
        Assert.IsFalse(scheduler.IsBusy);
    }

    [TestMethod]
    public async Task ScheduleAsync_WhenExecutionThrowsWhileNewRequestQueued_ProcessesLatestQueuedRequest()
    {
        var executedRequests = new List<int>();
        var firstStarted = CreateSignal();
        var releaseFirst = CreateSignal();
        var secondCompleted = CreateSignal();
        var firstExecution = Task.CompletedTask;

        using var scheduler = new LatestRequestScheduler<int>(async scheduledRequest =>
        {
            executedRequests.Add(scheduledRequest.Request);

            if (scheduledRequest.Request == 1)
            {
                firstStarted.TrySetResult();
                await releaseFirst.Task;
                throw new InvalidOperationException("boom");
            }

            if (scheduledRequest.Request == 2)
            {
                secondCompleted.TrySetResult();
            }
        });

        firstExecution = scheduler.ScheduleAsync(1);
        await firstStarted.Task;
        await scheduler.ScheduleAsync(2);
        releaseFirst.TrySetResult();

        await secondCompleted.Task;
        await AssertThrowsAsync<InvalidOperationException>(() => firstExecution);

        CollectionAssert.AreEqual(new[] { 1, 2 }, executedRequests);
        Assert.IsFalse(scheduler.IsBusy);
    }

    private static TaskCompletionSource CreateSignal()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException ex)
        {
            return ex;
        }

        Assert.Fail($"Expected exception of type {typeof(TException).Name} was not thrown.");
        throw new InvalidOperationException("Unreachable assertion path.");
    }
}
