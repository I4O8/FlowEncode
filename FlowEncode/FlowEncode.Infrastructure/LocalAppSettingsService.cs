using System.Text.Json;
using FlowEncode.Application;
using FlowEncode.Domain;

namespace FlowEncode.Infrastructure;

public sealed class LocalAppSettingsService : IAppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly LocalAppPaths _paths;
    private readonly object _gate = new();
    private AppSettings? _cache;

    public LocalAppSettingsService(LocalAppPaths paths)
    {
        _paths = paths;
    }

    public AppSettings Load()
    {
        lock (_gate)
        {
            if (_cache is not null)
            {
                return _cache;
            }

            if (!File.Exists(_paths.SettingsPath))
            {
                _cache = AppSettings.Default;
                return _cache;
            }

            try
            {
                var json = File.ReadAllText(_paths.SettingsPath);
                _cache = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? AppSettings.Default;
            }
            catch
            {
                _cache = AppSettings.Default;
            }

            return _cache;
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_paths.SettingsPath) ?? _paths.SettingsRootPath);
            File.WriteAllText(_paths.SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
            _cache = settings;
        }
    }
}
