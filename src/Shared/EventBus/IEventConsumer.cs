namespace Shared.EventBus;

/// <summary>
/// Interface for consuming events from the message bus
/// </summary>
public interface IEventConsumer
{
    Task StartConsuming(CancellationToken cancellationToken = default);
    Task StopConsuming();
}
