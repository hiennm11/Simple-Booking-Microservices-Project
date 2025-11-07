using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using PaymentService.Services;
using Polly;

namespace PaymentService.Tests.Services;

/// <summary>
/// Unit tests for ResiliencePipelineService
/// Tests retry logic, exponential backoff, and circuit breaker behavior
/// </summary>
public class ResiliencePipelineServiceTests
{
    private readonly Mock<ILogger<ResiliencePipelineService>> _loggerMock;
    private readonly ResiliencePipelineService _sut;

    public ResiliencePipelineServiceTests()
    {
        _loggerMock = new Mock<ILogger<ResiliencePipelineService>>();
        _sut = new ResiliencePipelineService(_loggerMock.Object);
    }

    #region Event Publishing Pipeline Tests

    [Fact]
    public async Task EventPublishingPipeline_SucceedsFirstAttempt_NoRetry()
    {
        // Arrange
        var pipeline = _sut.GetEventPublishingPipeline();
        var attemptCount = 0;

        // Act
        await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            await Task.CompletedTask; // Immediate success
        });

        // Assert
        attemptCount.Should().Be(1, "should succeed on first attempt without retry");
    }

    [Fact]
    public async Task EventPublishingPipeline_RetriesOnFailure_ThenSucceeds()
    {
        // Arrange
        var pipeline = _sut.GetEventPublishingPipeline();
        var attemptCount = 0;

        // Act
        await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            if (attemptCount < 3)
            {
                throw new Exception("Simulated transient failure");
            }
            await Task.CompletedTask; // Success on 3rd attempt
        });

        // Assert
        attemptCount.Should().Be(3, "should retry twice before succeeding on third attempt");
    }

    [Fact]
    public async Task EventPublishingPipeline_ExhaustsRetries_ThrowsException()
    {
        // Arrange
        var pipeline = _sut.GetEventPublishingPipeline();
        var attemptCount = 0;

        // Act
        var act = async () => await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            await Task.CompletedTask;
            throw new Exception("Always fails");
        });

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Always fails");
        attemptCount.Should().Be(3, "should attempt 3 times before throwing");
    }

    [Fact]
    public async Task EventPublishingPipeline_TimeoutsAfter10Seconds()
    {
        // Arrange
        var pipeline = _sut.GetEventPublishingPipeline();

        // Act
        var act = async () => await pipeline.ExecuteAsync(async ct =>
        {
            await Task.Delay(TimeSpan.FromSeconds(15), ct); // Longer than timeout
        });

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task EventPublishingPipeline_LogsRetryAttempts()
    {
        // Arrange
        var pipeline = _sut.GetEventPublishingPipeline();
        var attemptCount = 0;

        // Act
        await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                throw new InvalidOperationException("Transient error");
            }
            await Task.CompletedTask;
        });

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("retry")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log warning on retry attempt");
    }

    #endregion

    #region Database Pipeline Tests

    [Fact]
    public async Task DatabasePipeline_SucceedsImmediately_NoRetry()
    {
        // Arrange
        var pipeline = _sut.GetDatabasePipeline();
        var attemptCount = 0;

        // Act
        await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            await Task.CompletedTask;
        });

        // Assert
        attemptCount.Should().Be(1);
    }

    [Fact]
    public async Task DatabasePipeline_RetriesTransientException_MaxFiveAttempts()
    {
        // Arrange
        var pipeline = _sut.GetDatabasePipeline();
        var attemptCount = 0;

        // Act
        var act = async () => await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            if (attemptCount < 4)
            {
                throw new TimeoutException("Database connection timeout");
            }
            await Task.CompletedTask;
        });

        // Assert
        await act.Should().NotThrowAsync("should succeed on 4th attempt within max 5 retries");
        attemptCount.Should().Be(4, "should retry transient errors");
    }

    [Fact]
    public async Task DatabasePipeline_RetriesTimeoutException()
    {
        // Arrange
        var pipeline = _sut.GetDatabasePipeline();
        var attemptCount = 0;

        // Act
        await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            if (attemptCount < 4)
            {
                throw new TimeoutException("Database timeout");
            }
            await Task.CompletedTask;
        });

        // Assert
        attemptCount.Should().Be(4);
    }

    [Fact]
    public async Task DatabasePipeline_ExhaustsRetries_AfterMaxAttempts()
    {
        // Arrange
        var pipeline = _sut.GetDatabasePipeline();
        var attemptCount = 0;

        // Act
        var act = async () => await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            await Task.CompletedTask;
            throw new TimeoutException("Persistent timeout");
        });

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
        attemptCount.Should().Be(5, "database pipeline should allow 5 attempts");
    }

    [Fact]
    public async Task DatabasePipeline_DoesNotRetry_NonTransientExceptions()
    {
        // Arrange
        var pipeline = _sut.GetDatabasePipeline();
        var attemptCount = 0;

        // Act
        var act = async () => await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            await Task.CompletedTask;
            throw new InvalidOperationException("Non-transient error");
        });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        attemptCount.Should().Be(1, "should not retry non-transient exceptions");
    }

    #endregion

    #region Concurrent Execution Tests

    [Fact]
    public async Task EventPublishingPipeline_HandlesMultipleConcurrentCalls()
    {
        // Arrange
        var pipeline = _sut.GetEventPublishingPipeline();
        var successCount = 0;

        // Act
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            await pipeline.ExecuteAsync(async ct =>
            {
                await Task.Delay(10);
                Interlocked.Increment(ref successCount);
            });
        });

        await Task.WhenAll(tasks);

        // Assert
        successCount.Should().Be(10, "all concurrent operations should succeed");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task EventPublishingPipeline_ExponentialBackoff_IncreasesDelay()
    {
        // Arrange
        var pipeline = _sut.GetEventPublishingPipeline();
        var attemptTimes = new List<DateTime>();

        // Act
        await pipeline.ExecuteAsync(async ct =>
        {
            attemptTimes.Add(DateTime.UtcNow);
            if (attemptTimes.Count < 3)
            {
                throw new Exception("Retry test");
            }
            await Task.CompletedTask;
        });

        // Assert
        attemptTimes.Count.Should().Be(3);
        
        // Check that delay increases (with tolerance for jitter)
        var delay1 = (attemptTimes[1] - attemptTimes[0]).TotalSeconds;
        var delay2 = (attemptTimes[2] - attemptTimes[1]).TotalSeconds;
        
        delay1.Should().BeGreaterThan(1.5, "first retry should have ~2s delay");
        delay2.Should().BeGreaterThan(3.0, "second retry should have ~4s delay");
    }

    #endregion
}
