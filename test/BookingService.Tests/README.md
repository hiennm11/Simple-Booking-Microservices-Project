# BookingService.Tests

This project contains unit tests for the BookingService, including comprehensive tests for retry logic and resilience patterns using Polly.

## Test Coverage

### ResiliencePipelineServiceTests
- Event publishing pipeline retry behavior
- Database operations pipeline retry behavior
- Exponential backoff with jitter
- Timeout handling
- Concurrent execution tests

### BookingServiceImplTests
- Booking creation with event publishing retry
- Retry logic integration tests
- Error handling and failure scenarios

## Running Tests

```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test /p:CollectCoverage=true

# Run specific test class
dotnet test --filter FullyQualifiedName~ResiliencePipelineServiceTests
```

## Test Dependencies

- xUnit - Test framework
- Moq - Mocking framework
- FluentAssertions - Assertion library
- EF Core InMemory - In-memory database for testing
