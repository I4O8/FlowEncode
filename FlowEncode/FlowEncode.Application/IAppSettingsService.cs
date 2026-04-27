using FlowEncode.Domain;

namespace FlowEncode.Application;

public interface IAppSettingsService
{
    AppSettings Load();

    void Save(AppSettings settings);
}
