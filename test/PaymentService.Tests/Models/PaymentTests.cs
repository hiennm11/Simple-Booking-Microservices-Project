using Xunit;
using FluentAssertions;
using PaymentService.Models;

namespace PaymentService.Tests.Models;

/// <summary>
/// Unit tests for Payment model
/// </summary>
public class PaymentTests
{
    [Fact]
    public void Payment_DefaultValues_AreSetCorrectly()
    {
        // Act
        var payment = new Payment();

        // Assert
        payment.Id.Should().NotBeEmpty("Id should be auto-generated");
        payment.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        payment.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Payment_AllProperties_CanBeSet()
    {
        // Arrange
        var id = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var updatedAt = DateTime.UtcNow;
        var processedAt = DateTime.UtcNow.AddSeconds(5);

        // Act
        var payment = new Payment
        {
            Id = id,
            BookingId = bookingId,
            Amount = 500000,
            Status = "SUCCESS",
            PaymentMethod = "CREDIT_CARD",
            TransactionId = "TXN-123456",
            ErrorMessage = null,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            ProcessedAt = processedAt
        };

        // Assert
        payment.Id.Should().Be(id);
        payment.BookingId.Should().Be(bookingId);
        payment.Amount.Should().Be(500000);
        payment.Status.Should().Be("SUCCESS");
        payment.PaymentMethod.Should().Be("CREDIT_CARD");
        payment.TransactionId.Should().Be("TXN-123456");
        payment.ErrorMessage.Should().BeNull();
        payment.CreatedAt.Should().Be(createdAt);
        payment.UpdatedAt.Should().Be(updatedAt);
        payment.ProcessedAt.Should().Be(processedAt);
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("SUCCESS")]
    [InlineData("FAILED")]
    public void Payment_Status_AcceptsValidValues(string status)
    {
        // Act
        var payment = new Payment { Status = status };

        // Assert
        payment.Status.Should().Be(status);
    }

    [Theory]
    [InlineData("CREDIT_CARD")]
    [InlineData("DEBIT_CARD")]
    [InlineData("BANK_TRANSFER")]
    [InlineData("E_WALLET")]
    public void Payment_PaymentMethod_AcceptsValidValues(string paymentMethod)
    {
        // Act
        var payment = new Payment { PaymentMethod = paymentMethod };

        // Assert
        payment.PaymentMethod.Should().Be(paymentMethod);
    }

    [Fact]
    public void Payment_FailedPayment_HasErrorMessage()
    {
        // Act
        var payment = new Payment
        {
            Status = "FAILED",
            ErrorMessage = "Payment processing failed"
        };

        // Assert
        payment.Status.Should().Be("FAILED");
        payment.ErrorMessage.Should().Be("Payment processing failed");
        payment.TransactionId.Should().BeNull();
    }

    [Fact]
    public void Payment_SuccessfulPayment_HasTransactionId()
    {
        // Act
        var payment = new Payment
        {
            Status = "SUCCESS",
            TransactionId = "TXN-ABC123",
            ProcessedAt = DateTime.UtcNow
        };

        // Assert
        payment.Status.Should().Be("SUCCESS");
        payment.TransactionId.Should().NotBeNullOrEmpty();
        payment.ProcessedAt.Should().NotBeNull();
        payment.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Payment_PendingPayment_NoProcessingDetails()
    {
        // Act
        var payment = new Payment
        {
            Status = "PENDING"
        };

        // Assert
        payment.Status.Should().Be("PENDING");
        payment.TransactionId.Should().BeNull();
        payment.ProcessedAt.Should().BeNull();
        payment.ErrorMessage.Should().BeNull();
    }
}
