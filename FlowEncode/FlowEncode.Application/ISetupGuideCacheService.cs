using FlowEncode.Domain;

namespace FlowEncode.Application;

public interface ISetupGuideCacheService
{
    SetupGuideCacheSnapshot? Load();

    void Save(SetupGuideCacheSnapshot snapshot);

    void Clear();
}
