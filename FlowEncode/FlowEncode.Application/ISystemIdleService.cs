namespace FlowEncode.Application;

public interface ISystemIdleService
{
    TimeSpan GetIdleDuration();
}
