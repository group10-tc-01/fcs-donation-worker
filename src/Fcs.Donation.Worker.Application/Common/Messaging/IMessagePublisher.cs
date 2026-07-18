namespace Fcs.Donation.Worker.Application.Common.Messaging;

public interface IMessagePublisher
{
    Task PublishAsync<TMessage>(string topicName, TMessage message, CancellationToken cancellationToken = default);
}
