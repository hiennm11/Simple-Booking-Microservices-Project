using Microsoft.Extensions.Options;
using MongoDB.Driver;
using PaymentService.Data;
using PaymentService.DTOs;
using PaymentService.EventBus;
using PaymentService.Models;
using Shared.Contracts;
using Shared.EventBus;
using Polly;

namespace PaymentService.Services;

/// <summary>
/// Payment service implementation with Outbox pattern for reliable event publishing
/// </summary>
public class PaymentServiceImpl : IPaymentService
{
    private readonly MongoDbContext _dbContext;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<PaymentServiceImpl> _logger;

    public PaymentServiceImpl(
        MongoDbContext dbContext,
        IOutboxService outboxService,
        ILogger<PaymentServiceImpl> logger)
    {
        _dbContext = dbContext;
        _outboxService = outboxService;
        _logger = logger;
    }

    public async Task<PaymentResponse> ProcessPaymentAsync(ProcessPaymentRequest request)
    {
        _logger.LogInformation("Processing payment for BookingId: {BookingId}, Amount: {Amount}", 
            request.BookingId, request.Amount);

        // Check if payment already exists for this booking
        var existingPayment = await _dbContext.Payments
            .Find(p => p.BookingId == request.BookingId)
            .FirstOrDefaultAsync();

        if (existingPayment != null)
        {
            _logger.LogWarning("Payment already exists for BookingId: {BookingId}", request.BookingId);
            return MapToResponse(existingPayment);
        }

        // Create payment record
        var payment = new Payment
        {
            BookingId = request.BookingId,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Save payment to database
        await _dbContext.Payments.InsertOneAsync(payment);
        _logger.LogInformation("Payment record created with ID: {PaymentId}", payment.Id);

        // Simulate payment processing
        var isSuccess = await SimulatePaymentProcessingAsync(payment);

        if (isSuccess)
        {
            // Update payment status
            payment.Status = "SUCCESS";
            payment.TransactionId = $"TXN-{Guid.NewGuid():N}";
            payment.ProcessedAt = DateTime.UtcNow;
            payment.UpdatedAt = DateTime.UtcNow;

            var update = Builders<Payment>.Update
                .Set(p => p.Status, payment.Status)
                .Set(p => p.TransactionId, payment.TransactionId)
                .Set(p => p.ProcessedAt, payment.ProcessedAt)
                .Set(p => p.UpdatedAt, payment.UpdatedAt);

            await _dbContext.Payments.UpdateOneAsync(p => p.Id == payment.Id, update);

            _logger.LogInformation("Payment processed successfully: {PaymentId}", payment.Id);

            // Save PaymentSucceeded event to outbox
            var paymentEvent = new PaymentSucceededEvent
            {
                EventId = Guid.NewGuid(),
                EventName = "PaymentSucceeded",
                Timestamp = DateTime.UtcNow,
                Data = new PaymentSucceededData
                {
                    PaymentId = payment.Id,
                    BookingId = payment.BookingId,
                    Amount = payment.Amount,
                    Status = "SUCCESS"
                }
            };

            try
            {
                // Save event to outbox for guaranteed delivery
                await _outboxService.AddToOutboxAsync(
                    paymentEvent,
                    "PaymentSucceeded");

                _logger.LogInformation(
                    "PaymentSucceeded event saved to outbox for PaymentId: {PaymentId}, BookingId: {BookingId}",
                    payment.Id,
                    payment.BookingId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to save PaymentSucceeded event to outbox for PaymentId: {PaymentId}",
                    payment.Id);
                // Note: In MongoDB without transactions, this could result in payment without event
                // Consider implementing MongoDB transactions for atomic operations if needed
            }
        }
        else
        {
            // Update payment status to FAILED
            payment.Status = "FAILED";
            payment.ErrorMessage = "Payment processing failed";
            payment.ProcessedAt = DateTime.UtcNow;
            payment.UpdatedAt = DateTime.UtcNow;

            var update = Builders<Payment>.Update
                .Set(p => p.Status, payment.Status)
                .Set(p => p.ErrorMessage, payment.ErrorMessage)
                .Set(p => p.ProcessedAt, payment.ProcessedAt)
                .Set(p => p.UpdatedAt, payment.UpdatedAt);

            await _dbContext.Payments.UpdateOneAsync(p => p.Id == payment.Id, update);

            _logger.LogWarning("Payment processing failed: {PaymentId}", payment.Id);

            // Save PaymentFailed event to outbox
            var paymentFailedEvent = new PaymentFailedEvent
            {
                EventId = Guid.NewGuid(),
                EventName = "PaymentFailed",
                Timestamp = DateTime.UtcNow,
                Data = new PaymentFailedData
                {
                    PaymentId = payment.Id,
                    BookingId = payment.BookingId,
                    Amount = payment.Amount,
                    Reason = payment.ErrorMessage ?? "Payment processing failed",
                    Status = "FAILED"
                }
            };

            try
            {
                // Save event to outbox for guaranteed delivery
                await _outboxService.AddToOutboxAsync(
                    paymentFailedEvent,
                    "PaymentFailed");

                _logger.LogInformation(
                    "PaymentFailed event saved to outbox for PaymentId: {PaymentId}, BookingId: {BookingId}",
                    payment.Id,
                    payment.BookingId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to save PaymentFailed event to outbox for PaymentId: {PaymentId}",
                    payment.Id);
            }
        }

        return MapToResponse(payment);
    }

    public async Task<PaymentResponse?> GetPaymentByIdAsync(Guid paymentId)
    {
        _logger.LogInformation("Getting payment by ID: {PaymentId}", paymentId);

        var payment = await _dbContext.Payments
            .Find(p => p.Id == paymentId)
            .FirstOrDefaultAsync();

        return payment != null ? MapToResponse(payment) : null;
    }

    public async Task<PaymentResponse?> GetPaymentByBookingIdAsync(Guid bookingId)
    {
        _logger.LogInformation("Getting payment by BookingId: {BookingId}", bookingId);

        var payment = await _dbContext.Payments
            .Find(p => p.BookingId == bookingId)
            .FirstOrDefaultAsync();

        return payment != null ? MapToResponse(payment) : null;
    }

    /// <summary>
    /// Simulate payment processing with 90% success rate
    /// </summary>
    private async Task<bool> SimulatePaymentProcessingAsync(Payment payment)
    {
        _logger.LogInformation("Simulating payment processing for PaymentId: {PaymentId}", payment.Id);

        // Simulate processing delay
        await Task.Delay(1000);

        // 90% success rate
        var random = new Random();
        var isSuccess = random.Next(1, 11) <= 9;

        _logger.LogInformation("Payment simulation result: {Result}", isSuccess ? "SUCCESS" : "FAILED");

        return isSuccess;
    }

    /// <summary>
    /// Map Payment model to PaymentResponse DTO
    /// </summary>
    private static PaymentResponse MapToResponse(Payment payment)
    {
        return new PaymentResponse
        {
            Id = payment.Id,
            BookingId = payment.BookingId,
            Amount = payment.Amount,
            Status = payment.Status,
            PaymentMethod = payment.PaymentMethod,
            TransactionId = payment.TransactionId,
            ErrorMessage = payment.ErrorMessage,
            CreatedAt = payment.CreatedAt,
            ProcessedAt = payment.ProcessedAt
        };
    }
}
