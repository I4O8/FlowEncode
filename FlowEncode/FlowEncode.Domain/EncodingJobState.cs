namespace FlowEncode.Domain;

public enum EncodingJobState
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}
