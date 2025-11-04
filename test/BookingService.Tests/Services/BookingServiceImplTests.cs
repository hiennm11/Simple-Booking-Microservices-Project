using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BookingService.Services;
using BookingService.Data;
using BookingService.DTOs;
using BookingService.Models;
using BookingService.EventBus;
using Shared.EventBus;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;

namespace BookingService.Tests.Services;

/// <summary>
/// Unit tests for BookingServiceImpl with retry logic
/// </summary>
public class BookingServiceImplTests : IDisposable
{
    private readonly Mock<IEventBus> _eventBusMock;
    private readonly Mock<IResiliencePipelineService> _resiliencePipelineServiceMock;
    private readonly Mock<ILogger<BookingServiceImpl>> _loggerMock;
    private readonly BookingDbContext _dbContext;
    private readonly BookingServiceImpl _sut;
    private readonly RabbitMQSettings _rabbitMQSettings;

    public BookingServiceImplTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(databaseName: $"BookingTestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new BookingDbContext(options);

        // Setup mocks
        _eventBusMock = new Mock<IEventBus>();
        _resiliencePipelineServiceMock = new Mock<IResiliencePipelineService>();
        _loggerMock = new Mock<ILogger<BookingServiceImpl>>();

        // Setup RabbitMQ settings
        _rabbitMQSettings = new RabbitMQSettings
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "guest",
            Password = "guest",
            VirtualHost = "/",
            Queues = new Dictionary<string, string>
            {
                { "BookingCreated", "booking_created" }
            }
        };

        var rabbitMQOptions = Options.Create(_rabbitMQSettings);

        // Setup resilience pipeline that executes immediately (no actual retry in tests)
        var mockPipeline = new ResiliencePipelineBuilder().Build();
        _resiliencePipelineServiceMock
            .Setup(x => x.GetEventPublishingPipeline())
            .Returns(mockPipeline);

        // Create system under test
        _sut = new BookingServiceImpl(
            _dbContext,
            _eventBusMock.Object,
            rabbitMQOptions,
            _resiliencePipelineServiceMock.Object,
            _loggerMock.Object);
    }

    #region CreateBookingAsync Tests

    [Fact]
    public async Task CreateBookingAsync_ValidRequest_CreatesBookingAndPublishesEvent()
    {
        // Arrange
        var request = new CreateBookingRequest
        {
            UserId = Guid.NewGuid(),
            RoomId = "ROOM-001",
            Amount = 100.00m
        };

        // Act
        var result = await _sut.CreateBookingAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(request.UserId);
        result.RoomId.Should().Be(request.RoomId);
        result.Amount.Should().Be(request.Amount);
        result.Status.Should().Be("PENDING");

        // Verify booking was saved to database
        var booking = await _dbContext.Bookings.FirstOrDefaultAsync(b => b.Id == result.Id);
        booking.Should().NotBeNull();

        // Verify event was published
        _eventBusMock.Verify(
            x => x.PublishAsync(
                It.IsAny<object>(),
                "booking_created",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateBookingAsync_EventPublishingFails_BookingStillCreated()
    {
        // Arrange
        var request = new CreateBookingRequest
        {
            UserId = Guid.NewGuid(),
            RoomId = "ROOM-002",
            Amount = 150.00m
        };

        // Setup event bus to fail
        _eventBusMock
            .Setup(x => x.PublishAsync(
                It.IsAny<object>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("RabbitMQ connection failed"));

        // Act
        var result = await _sut.CreateBookingAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("PENDING");

        // Verify booking was still saved even though event failed
        var booking = await _dbContext.Bookings.FirstOrDefaultAsync(b => b.Id == result.Id);
        booking.Should().NotBeNull();

        // Verify error was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Failed to publish")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateBookingAsync_WithRetryPolicy_RetriesOnTransientFailure()
    {
        // Arrange
        var request = new CreateBookingRequest
        {
            UserId = Guid.NewGuid(),
            RoomId = "ROOM-003",
            Amount = 200.00m
        };

        var attemptCount = 0;
        _eventBusMock
            .Setup(x => x.PublishAsync(
                It.IsAny<object>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    throw new Exception("Transient failure");
                }
            })
            .Returns(Task.CompletedTask);

        // Setup actual resilience pipeline with retry
        var actualPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(10),
                BackoffType = DelayBackoffType.Constant
            })
            .Build();

        _resiliencePipelineServiceMock
            .Setup(x => x.GetEventPublishingPipeline())
            .Returns(actualPipeline);

        // Act
        var result = await _sut.CreateBookingAsync(request);

        // Assert
        result.Should().NotBeNull();
        attemptCount.Should().Be(3, "should retry until success");
        
        _eventBusMock.Verify(
            x => x.PublishAsync(
                It.IsAny<object>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    #endregion

    #region GetBookingByIdAsync Tests

    [Fact]
    public async Task GetBookingByIdAsync_ExistingBooking_ReturnsBooking()
    {
        // Arrange
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RoomId = "ROOM-101",
            Amount = 100.00m,
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Bookings.Add(booking);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetBookingByIdAsync(booking.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(booking.Id);
        result.Amount.Should().Be(booking.Amount);
    }

    [Fact]
    public async Task GetBookingByIdAsync_NonExistingBooking_ReturnsNull()
    {
        // Arrange
        var nonExistingId = Guid.NewGuid();

        // Act
        var result = await _sut.GetBookingByIdAsync(nonExistingId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetBookingsByUserIdAsync Tests

    [Fact]
    public async Task GetBookingsByUserIdAsync_ExistingBookings_ReturnsUserBookings()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var bookings = new[]
        {
            new Booking
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RoomId = "ROOM-201",
                Amount = 100.00m,
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow
            },
            new Booking
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RoomId = "ROOM-202",
                Amount = 150.00m,
                Status = "CONFIRMED",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        _dbContext.Bookings.AddRange(bookings);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.GetBookingsByUserIdAsync(userId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(b => b.UserId.Should().Be(userId));
    }

    [Fact]
    public async Task GetBookingsByUserIdAsync_NoBookings_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await _sut.GetBookingsByUserIdAsync(userId);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region UpdateBookingStatusAsync Tests

    [Fact]
    public async Task UpdateBookingStatusAsync_ToConfirmed_UpdatesStatusAndSetsConfirmedAt()
    {
        // Arrange
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RoomId = "ROOM-301",
            Amount = 100.00m,
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Bookings.Add(booking);
        await _dbContext.SaveChangesAsync();

        var updateRequest = new UpdateBookingStatusRequest
        {
            Status = "CONFIRMED"
        };

        // Act
        var result = await _sut.UpdateBookingStatusAsync(booking.Id, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be("CONFIRMED");
        result.ConfirmedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateBookingStatusAsync_ToCancelled_UpdatesStatusAndSetsCancelledAt()
    {
        // Arrange
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RoomId = "ROOM-302",
            Amount = 100.00m,
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Bookings.Add(booking);
        await _dbContext.SaveChangesAsync();

        var updateRequest = new UpdateBookingStatusRequest
        {
            Status = "CANCELLED",
            CancellationReason = "User requested cancellation"
        };

        // Act
        var result = await _sut.UpdateBookingStatusAsync(booking.Id, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be("CANCELLED");
        result.CancelledAt.Should().NotBeNull();
        result.CancellationReason.Should().Be("User requested cancellation");
    }

    [Fact]
    public async Task UpdateBookingStatusAsync_NonExistingBooking_ReturnsNull()
    {
        // Arrange
        var nonExistingId = Guid.NewGuid();
        var updateRequest = new UpdateBookingStatusRequest
        {
            Status = "CONFIRMED"
        };

        // Act
        var result = await _sut.UpdateBookingStatusAsync(nonExistingId, updateRequest);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Resilience Tests

    [Fact]
    public async Task CreateBookingAsync_UsesResiliencePipeline_ForEventPublishing()
    {
        // Arrange
        var request = new CreateBookingRequest
        {
            UserId = Guid.NewGuid(),
            RoomId = "ROOM-401",
            Amount = 100.00m
        };

        // Act
        await _sut.CreateBookingAsync(request);

        // Assert
        _resiliencePipelineServiceMock.Verify(
            x => x.GetEventPublishingPipeline(),
            Times.Once,
            "should retrieve resilience pipeline for event publishing");
    }

    #endregion

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}
