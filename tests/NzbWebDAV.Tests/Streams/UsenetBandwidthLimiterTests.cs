using NzbWebDAV.Streams;
using NzbWebDAV.Tests.TestUtils;

namespace NzbWebDAV.Tests.Streams;

public class UsenetBandwidthLimiterTests
{
    [Fact]
    public async Task AcquireAsync_Unlimited_CompletesSynchronouslyWithoutCharging()
    {
        var limiter = new UsenetBandwidthLimiter();
        var task = limiter.AcquireAsync(1_000_000, CancellationToken.None);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal(0, limiter.TotalChargedBytes);
    }

    [Fact]
    public async Task AcquireAsync_WithinBurst_CompletesImmediatelyAndCharges()
    {
        var time = new ControllableTimeProvider();
        var limiter = new UsenetBandwidthLimiter(time);
        limiter.UpdateLimit(1_000_000);

        await limiter.AcquireAsync(64 * 1024, CancellationToken.None);

        Assert.Equal(64 * 1024, limiter.TotalChargedBytes);
    }

    [Fact]
    public async Task AcquireAsync_AfterBurst_WaitsForRefillAtConfiguredRate()
    {
        var time = new ControllableTimeProvider();
        var limiter = new UsenetBandwidthLimiter(time);
        limiter.UpdateLimit(10_000);

        await limiter.AcquireAsync(64 * 1024, CancellationToken.None);

        var waiting = limiter.AcquireAsync(10_000, CancellationToken.None).AsTask();
        Assert.False(waiting.IsCompleted);

        time.Advance(TimeSpan.FromSeconds(1));
        await waiting.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(64 * 1024 + 10_000, limiter.TotalChargedBytes);
    }

    [Fact]
    public async Task AcquireAsync_OversizedGrant_TakesBurstThenIncursDebt()
    {
        var time = new ControllableTimeProvider();
        var limiter = new UsenetBandwidthLimiter(time);
        limiter.UpdateLimit(10_000);

        await limiter.AcquireAsync(200_000, CancellationToken.None);
        Assert.Equal(200_000, limiter.TotalChargedBytes);

        var waiting = limiter.AcquireAsync(1, CancellationToken.None).AsTask();
        Assert.False(waiting.IsCompleted);

        time.Advance(TimeSpan.FromSeconds(20));
        await waiting.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task AcquireAsync_Fifo_SecondWaiterDoesNotOvertakeFirst()
    {
        var time = new ControllableTimeProvider();
        var limiter = new UsenetBandwidthLimiter(time);
        limiter.UpdateLimit(10_000);
        await limiter.AcquireAsync(64 * 1024, CancellationToken.None);

        var first = limiter.AcquireAsync(10_000, CancellationToken.None).AsTask();
        var second = limiter.AcquireAsync(10_000, CancellationToken.None).AsTask();
        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);

        time.Advance(TimeSpan.FromSeconds(1));
        await first.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(second.IsCompleted);

        time.Advance(TimeSpan.FromSeconds(1));
        await second.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task AcquireAsync_CancelledWaiter_ReleasesFifoHead()
    {
        var time = new ControllableTimeProvider();
        var limiter = new UsenetBandwidthLimiter(time);
        limiter.UpdateLimit(10_000);
        await limiter.AcquireAsync(64 * 1024, CancellationToken.None);

        using var cts = new CancellationTokenSource();
        var cancelled = limiter.AcquireAsync(10_000, cts.Token).AsTask();
        var next = limiter.AcquireAsync(10_000, CancellationToken.None).AsTask();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        Assert.False(next.IsCompleted);

        time.Advance(TimeSpan.FromSeconds(1));
        await next.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task UpdateLimit_Zero_CompletesWaitersWithoutChargingThem()
    {
        var time = new ControllableTimeProvider();
        var limiter = new UsenetBandwidthLimiter(time);
        limiter.UpdateLimit(10_000);
        await limiter.AcquireAsync(64 * 1024, CancellationToken.None);
        var charged = limiter.TotalChargedBytes;

        var waiting = limiter.AcquireAsync(10_000, CancellationToken.None).AsTask();
        Assert.False(waiting.IsCompleted);

        limiter.UpdateLimit(0);
        await waiting.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(charged, limiter.TotalChargedBytes);
        Assert.Equal(0, limiter.BytesPerSecond);
    }

    [Fact]
    public async Task UpdateLimit_Increase_WakesWaiterSooner()
    {
        var time = new ControllableTimeProvider();
        var limiter = new UsenetBandwidthLimiter(time);
        limiter.UpdateLimit(1_000);
        await limiter.AcquireAsync(64 * 1024, CancellationToken.None);

        var waiting = limiter.AcquireAsync(10_000, CancellationToken.None).AsTask();
        limiter.UpdateLimit(10_000);
        time.Advance(TimeSpan.FromSeconds(1));
        await waiting.WaitAsync(TimeSpan.FromSeconds(2));
    }
}
