using FlowEncode.Domain;

namespace FlowEncode.Application;

public interface IQueueCompletionActionService
{
    Task<string?> ExecuteAsync(QueueCompletionAction action);
}
