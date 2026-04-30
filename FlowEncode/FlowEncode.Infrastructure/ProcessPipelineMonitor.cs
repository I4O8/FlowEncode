namespace FlowEncode.Infrastructure;

internal enum PipeCopyCompletion
{
    Completed,
    BrokenPipe
}

internal enum PipelineFirstCompletion
{
    ProducerExited,
    ConsumerExited,
    PipeCompleted,
    PipeBroken
}

internal static class ProcessPipelineMonitor
{
    public static async Task<PipeCopyCompletion> ObservePipeCopyAsync(Task copyTask)
    {
        try
        {
            await copyTask;
            return PipeCopyCompletion.Completed;
        }
        catch (IOException)
        {
            return PipeCopyCompletion.BrokenPipe;
        }
        catch (ObjectDisposedException)
        {
            return PipeCopyCompletion.BrokenPipe;
        }
    }

    public static async Task<PipelineFirstCompletion> WaitForFirstCompletionAsync(
        Task producerExitTask,
        Task consumerExitTask,
        Task<PipeCopyCompletion> pipeCopyTask)
    {
        var firstCompletedTask = await Task.WhenAny(
            producerExitTask,
            consumerExitTask,
            pipeCopyTask);

        if (ReferenceEquals(firstCompletedTask, producerExitTask))
        {
            return PipelineFirstCompletion.ProducerExited;
        }

        if (ReferenceEquals(firstCompletedTask, consumerExitTask))
        {
            return PipelineFirstCompletion.ConsumerExited;
        }

        return await pipeCopyTask == PipeCopyCompletion.BrokenPipe
            ? PipelineFirstCompletion.PipeBroken
            : PipelineFirstCompletion.PipeCompleted;
    }
}
