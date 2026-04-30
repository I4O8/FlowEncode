using FlowEncode.Domain;
using FlowEncode.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlowEncode.Domain.Tests;

[TestClass]
public sealed class LocalAppSettingsServiceTests
{
    private string? _settingsRoot;

    [TestInitialize]
    public void SetUp()
    {
        _settingsRoot = Path.Combine(Path.GetTempPath(), "FlowEncodeSettingsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_settingsRoot);
    }

    [TestCleanup]
    public void TearDown()
    {
        if (!string.IsNullOrWhiteSpace(_settingsRoot) && Directory.Exists(_settingsRoot))
        {
            Directory.Delete(_settingsRoot, recursive: true);
        }
    }

    [TestMethod]
    public void Save_WritesSettingsAtomicallyWithoutLeavingTempFile()
    {
        var paths = CreatePaths();
        var service = new LocalAppSettingsService(paths);
        var settings = AppSettings.Default with
        {
            WorkspaceRootPath = @"D:\workspace\flowencode",
            AutoCheckUpdatesOnStartup = false
        };

        service.Save(settings);

        Assert.IsTrue(File.Exists(paths.SettingsPath));
        Assert.AreEqual(settings, service.Load());
        Assert.IsFalse(File.Exists(paths.SettingsPath + ".tmp"));
    }

    [TestMethod]
    public void Load_BacksUpBrokenSettingsAndFallsBackToDefault()
    {
        var paths = CreatePaths();
        Directory.CreateDirectory(paths.SettingsRootPath);
        File.WriteAllText(paths.SettingsPath, "{not-json");

        var service = new LocalAppSettingsService(paths);

        var loaded = service.Load();
        var recoveryInfo = service.ConsumeLastLoadRecoveryInfo();
        var backupFiles = Directory.GetFiles(paths.SettingsRootPath, "settings.json.broken-*");

        Assert.AreEqual(AppSettings.Default, loaded);
        Assert.IsNotNull(recoveryInfo);
        Assert.AreEqual(1, backupFiles.Length);
        Assert.AreEqual(backupFiles[0], recoveryInfo.BackupPath);
        Assert.IsFalse(File.Exists(paths.SettingsPath));
        Assert.IsNull(service.ConsumeLastLoadRecoveryInfo());
    }

    private LocalAppPaths CreatePaths()
    {
        return new LocalAppPaths(_settingsRoot!, _settingsRoot!);
    }
}
