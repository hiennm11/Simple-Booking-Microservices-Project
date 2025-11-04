using Xunit;
using FluentAssertions;
using PaymentService.DTOs;
using System.ComponentModel.DataAnnotations;

namespace PaymentService.Tests.DTOs;

/// <summary>
/// Unit tests for DTOs and their validation attributes
/// </summary>
public class ProcessPaymentRequestTests
{
    [Fact]
    public void ProcessPaymentRequest_ValidData_PassesValidation()
    {
        // Arrange
        var request = new ProcessPaymentRequest
        {
            BookingId = Guid.NewGuid(),
            Amount = 500000,
            PaymentMethod = "CREDIT_CARD"
        };

        // Act
        var validationResults = ValidateModel(request);

        // Assert
        validationResults.Should().BeEmpty("valid request should pass validation");
    }

    [Fact]
    public void ProcessPaymentRequest_EmptyBookingId_FailsValidation()
    {
        // Arrange
        var request = new ProcessPaymentRequest
        {
            BookingId = Guid.Empty,
            Amount = 500000,
            PaymentMethod = "CREDIT_CARD"
        };

        // Act
        var validationResults = ValidateModel(request);

        // Assert
        validationResults.Should().NotBeEmpty("empty GUID should fail validation");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    [InlineData(-0.01)]
    public void ProcessPaymentRequest_InvalidAmount_FailsValidation(decimal amount)
    {
        // Arrange
        var request = new ProcessPaymentRequest
        {
            BookingId = Guid.NewGuid(),
            Amount = amount,
            PaymentMethod = "CREDIT_CARD"
        };

        // Act
        var validationResults = ValidateModel(request);

        // Assert
        validationResults.Should().Contain(v => v.MemberNames.Contains(nameof(ProcessPaymentRequest.Amount)),
            "non-positive amounts should fail validation");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ProcessPaymentRequest_InvalidPaymentMethod_FailsValidation(string? paymentMethod)
    {
        // Arrange
        var request = new ProcessPaymentRequest
        {
            BookingId = Guid.NewGuid(),
            Amount = 500000,
            PaymentMethod = paymentMethod!
        };

        // Act
        var validationResults = ValidateModel(request);

        // Assert
        validationResults.Should().Contain(v => v.MemberNames.Contains(nameof(ProcessPaymentRequest.PaymentMethod)),
            "empty payment method should fail validation");
    }

    [Fact]
    public void PaymentResponse_AllProperties_MapCorrectly()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var processedAt = DateTime.UtcNow.AddSeconds(5);

        // Act
        var response = new PaymentResponse
        {
            Id = paymentId,
            BookingId = bookingId,
            Amount = 500000,
            Status = "SUCCESS",
            PaymentMethod = "CREDIT_CARD",
            TransactionId = "TXN-123",
            ErrorMessage = null,
            CreatedAt = createdAt,
            ProcessedAt = processedAt
        };

        // Assert
        response.Id.Should().Be(paymentId);
        response.BookingId.Should().Be(bookingId);
        response.Amount.Should().Be(500000);
        response.Status.Should().Be("SUCCESS");
        response.PaymentMethod.Should().Be("CREDIT_CARD");
        response.TransactionId.Should().Be("TXN-123");
        response.ErrorMessage.Should().BeNull();
        response.CreatedAt.Should().Be(createdAt);
        response.ProcessedAt.Should().Be(processedAt);
    }

    #region Helper Methods

    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }

    #endregion
}
