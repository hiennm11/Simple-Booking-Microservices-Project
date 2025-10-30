namespace Shared.EventBus;

/// <summary>
/// Interface for publishing events to the message bus
/// </summary>
public interface IEventBus
{
    Task PublishAsync<T>(T @event, string queueName, CancellationToken cancellationToken = default) where T : class;
}
