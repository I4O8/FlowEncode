using System.Text.Json;
using FlowEncode.Application;
using FlowEncode.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlowEncode.Domain.Tests;

[TestClass]
public sealed class VapourSynthPreviewServiceTests
{
    [TestMethod]
    public async Task OpenSessionAsync_WhenReadyResponseReceived_ReturnsSortedOutputs()
    {
        using var context = CreateContext();
        context.Session.EnqueueResponse(CreateReadyResponseJson(
            (1, "Output B", 1280, 720, 200),
            (0, "Output A", 1920, 1080, 100)));

        var result = await context.Service.OpenSessionAsync(CreateOpenRequest());

        CollectionAssert.AreEqual(new[] { 0, 1 }, result.Outputs.Select(static output => output.Index).ToArray());
        Assert.AreEqual("Output A", result.Outputs[0].Name);
        Assert.AreEqual("Output B", result.Outputs[1].Name);
    }

    [TestMethod]
    public async Task OpenSessionAsync_WhenLogResponsePrecedesReady_EmitsLogAndSucceeds()
    {
        using var context = CreateContext();
        var emittedLogs = new List<VapourSynthPreviewLogEntry>();
        context.Service.LogEmitted += (_, args) => emittedLogs.Add(args.Entry);
        context.Session.EnqueueResponse(CreateLogResponseJson("warning", "helper", "warming up"));
        context.Session.EnqueueResponse(CreateReadyResponseJson((0, "Preview", 1920, 1080, 100)));

        var result = await context.Service.OpenSessionAsync(CreateOpenRequest());

        Assert.AreEqual(1, result.Outputs.Count);
        Assert.AreEqual(1, emittedLogs.Count);
        Assert.AreEqual(VapourSynthPreviewLogLevel.Warning, emittedLogs[0].Level);
        Assert.AreEqual("helper", emittedLogs[0].Source);
        Assert.AreEqual("warming up", emittedLogs[0].Message);
    }

    [TestMethod]
    public async Task RenderFrameAsync_WhenRequestIdMismatch_Throws()
    {
        using var context = CreateContext();
        context.Session.EnqueueResponse(CreateReadyResponseJson((0, "Preview", 1920, 1080, 100)));
        context.Session.EnqueueResponse(CreateFrameResponseJson(
            requestId: 999,
            outputIndex: 0,
            frameNumber: 12,
            rawPixelPath: Path.Combine(context.TempRootPath, "frame-999.bgra")));
        await context.Service.OpenSessionAsync(CreateOpenRequest());

        var exception = await AssertThrowsAsync<InvalidOperationException>(
            () => context.Service.RenderFrameAsync(0, 12));

        StringAssert.Contains(exception.Message, "mismatched frame response");
    }

    [TestMethod]
    public async Task RenderFrameAsync_WhenRawPixelPathMissing_Throws()
    {
        using var context = CreateContext();
        context.Session.EnqueueResponse(CreateReadyResponseJson((0, "Preview", 1920, 1080, 100)));
        context.Session.EnqueueResponse(CreateFrameResponseJson(
            requestId: 1,
            outputIndex: 0,
            frameNumber: 5,
            rawPixelPath: Path.Combine(context.TempRootPath, "missing-frame.bgra")));
        await context.Service.OpenSessionAsync(CreateOpenRequest());

        var exception = await AssertThrowsAsync<InvalidOperationException>(
            () => context.Service.RenderFrameAsync(0, 5));

        StringAssert.Contains(exception.Message, "did not produce a frame buffer");
    }

    [TestMethod]
    public async Task CloseSessionAsync_WhenGracefulCloseTimesOut_KillsHostAndCleansSession()
    {
        using var context = CreateContext();
        context.Session.EnqueueResponse(CreateReadyResponseJson((0, "Preview", 1920, 1080, 100)));
        context.Session.EnqueueCancellationWaitBehavior(static cancellationToken => Task.Delay(Timeout.Infinite, cancellationToken));
        context.Session.EnqueueWaitBehavior(session =>
        {
            session.HasExited = true;
            return Task.CompletedTask;
        });

        await context.Service.OpenSessionAsync(CreateOpenRequest());
        var sessionDirectory = Path.GetDirectoryName(context.Factory.StartupPath)!;
        Assert.IsTrue(Directory.Exists(sessionDirectory));

        await context.Service.CloseSessionAsync();

        Assert.IsTrue(context.Session.KillCalled);
        Assert.IsTrue(context.Session.WrittenLines.Any(static line => line.Contains("\"command\":\"close\"", StringComparison.Ordinal)));
        Assert.IsFalse(Directory.Exists(sessionDirectory));
    }

    private static TestContext CreateContext()
    {
        var tempRootPath = Path.Combine(Path.GetTempPath(), "FlowEncodePreviewTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRootPath);

        var session = new FakePreviewHostSession();
        var factory = new FakePreviewHostFactory(session);
        var service = new VapourSynthPreviewService(
            new LocalAppPaths(),
            factory,
            tempRootPath);

        return new TestContext(tempRootPath, service, factory, session);
    }

    private static VapourSynthPreviewOpenRequest CreateOpenRequest()
    {
        return new VapourSynthPreviewOpenRequest(
            SourceFilePath: @"D:\preview\script.vpy",
            DisplayName: "script.vpy",
            Content: "clip = core.std.BlankClip()",
            WorkingDirectory: Path.GetTempPath());
    }

    private static string CreateReadyResponseJson(params (int Index, string Name, int Width, int Height, int TotalFrames)[] outputs)
    {
        return JsonSerializer.Serialize(new
        {
            type = "ready",
            outputs = outputs.Select(output => new
            {
                index = output.Index,
                name = output.Name,
                width = output.Width,
                height = output.Height,
                totalFrames = output.TotalFrames,
                fpsNumerator = 24000,
                fpsDenominator = 1001,
                formatName = "YUV420P8",
                bitsPerSample = 8
            }).ToArray()
        });
    }

    private static string CreateLogResponseJson(string level, string source, string message)
    {
        return JsonSerializer.Serialize(new
        {
            type = "log",
            level,
            source,
            message
        });
    }

    private static string CreateFrameResponseJson(int requestId, int outputIndex, int frameNumber, string rawPixelPath)
    {
        return JsonSerializer.Serialize(new
        {
            type = "frame",
            requestId,
            outputIndex,
            frameNumber,
            width = 1920,
            height = 1080,
            rawPixelPath,
            frameType = "I",
            properties = Array.Empty<object>()
        });
    }

    private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException ex)
        {
            return ex;
        }

        Assert.Fail($"Expected exception of type {typeof(TException).Name} was not thrown.");
        throw new InvalidOperationException("Unreachable assertion path.");
    }

    private sealed class TestContext : IDisposable
    {
        public TestContext(
            string tempRootPath,
            VapourSynthPreviewService service,
            FakePreviewHostFactory factory,
            FakePreviewHostSession session)
        {
            TempRootPath = tempRootPath;
            Service = service;
            Factory = factory;
            Session = session;
        }

        public string TempRootPath { get; }

        public VapourSynthPreviewService Service { get; }

        public FakePreviewHostFactory Factory { get; }

        public FakePreviewHostSession Session { get; }

        public void Dispose()
        {
            Service.Dispose();

            if (Directory.Exists(TempRootPath))
            {
                Directory.Delete(TempRootPath, recursive: true);
            }
        }
    }

    private sealed class FakePreviewHostFactory : IVapourSynthPreviewHostFactory
    {
        private readonly FakePreviewHostSession _session;

        public FakePreviewHostFactory(FakePreviewHostSession session)
        {
            _session = session;
        }

        public string? StartupPath { get; private set; }

        public Task<IVapourSynthPreviewHostSession> StartAsync(
            string workingDirectory,
            string startupPath,
            CancellationToken cancellationToken = default)
        {
            StartupPath = startupPath;
            return Task.FromResult<IVapourSynthPreviewHostSession>(_session);
        }
    }

    private sealed class FakePreviewHostSession : IVapourSynthPreviewHostSession
    {
        private readonly Queue<string?> _responses = new();
        private readonly Queue<Func<FakePreviewHostSession, CancellationToken, Task>> _waitBehaviors = new();
        private Action<string>? _stderrLineReceived;

        public List<string> WrittenLines { get; } = [];

        public bool HasExited { get; set; }

        public int ProcessId { get; set; } = 4242;

        public bool KillCalled { get; private set; }

        public event Action<string>? StderrLineReceived
        {
            add => _stderrLineReceived += value;
            remove => _stderrLineReceived -= value;
        }

        public void EnqueueResponse(string response)
        {
            _responses.Enqueue(response);
        }

        public void EnqueueWaitBehavior(Func<FakePreviewHostSession, Task> behavior)
        {
            _waitBehaviors.Enqueue((session, _) => behavior(session));
        }

        public void EnqueueCancellationWaitBehavior(Func<CancellationToken, Task> behavior)
        {
            _waitBehaviors.Enqueue((_, cancellationToken) => behavior(cancellationToken));
        }

        public Task WriteLineAsync(string line, CancellationToken cancellationToken = default)
        {
            WrittenLines.Add(line);
            return Task.CompletedTask;
        }

        public Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : null);
        }

        public Task WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            if (_waitBehaviors.Count > 0)
            {
                return _waitBehaviors.Dequeue()(this, cancellationToken);
            }

            HasExited = true;
            return Task.CompletedTask;
        }

        public void Kill(bool entireProcessTree = true)
        {
            KillCalled = true;
        }

        public void Dispose()
        {
        }

        public void EmitStderrLine(string line)
        {
            _stderrLineReceived?.Invoke(line);
        }
    }
}
