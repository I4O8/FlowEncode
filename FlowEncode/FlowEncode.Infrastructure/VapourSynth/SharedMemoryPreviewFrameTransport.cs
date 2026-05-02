using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;

namespace FlowEncode.Infrastructure;

internal interface IPreviewFrameTransportFactory
{
    IPreviewFrameTransportSession CreateSession(int capacityBytes);
}

internal interface IPreviewFrameTransportSession : IDisposable
{
    PreviewFrameTransportDescriptor Descriptor { get; }

    Task<byte[]> ReadFrameAsync(
        int byteLength,
        byte[]? reusableBuffer = null,
        CancellationToken cancellationToken = default);
}

internal sealed record PreviewFrameTransportDescriptor(
    string Kind,
    string SharedMemoryName,
    int CapacityBytes);

 [SupportedOSPlatform("windows")]
internal sealed class SharedMemoryPreviewFrameTransportFactory : IPreviewFrameTransportFactory
{
    public IPreviewFrameTransportSession CreateSession(int capacityBytes)
    {
        return new SharedMemoryPreviewFrameTransportSession(capacityBytes);
    }
}

[SupportedOSPlatform("windows")]
internal sealed class SharedMemoryPreviewFrameTransportSession : IPreviewFrameTransportSession
{
    private readonly MemoryMappedFile _memoryMappedFile;
    private readonly int _capacityBytes;
    private bool _disposed;

    public SharedMemoryPreviewFrameTransportSession(int capacityBytes)
    {
        _capacityBytes = Math.Max(1, capacityBytes);
        var sharedMemoryName = $@"Local\FlowEncode.VapourSynthPreview.{Guid.NewGuid():N}";
        _memoryMappedFile = MemoryMappedFile.CreateOrOpen(
            sharedMemoryName,
            _capacityBytes,
            MemoryMappedFileAccess.ReadWrite);
        Descriptor = new PreviewFrameTransportDescriptor(
            "sharedMemory",
            sharedMemoryName,
            _capacityBytes);
    }

    public PreviewFrameTransportDescriptor Descriptor { get; }

    public Task<byte[]> ReadFrameAsync(
        int byteLength,
        byte[]? reusableBuffer = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteLength);

        if (byteLength > _capacityBytes)
        {
            throw new InvalidOperationException("Preview frame exceeds the shared memory transport capacity.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var pixels = reusableBuffer is { Length: var reusableLength } && reusableLength == byteLength
            ? reusableBuffer
            : GC.AllocateUninitializedArray<byte>(byteLength);

        using var stream = _memoryMappedFile.CreateViewStream(
            0,
            byteLength,
            MemoryMappedFileAccess.Read);
        stream.ReadExactly(pixels, 0, byteLength);
        return Task.FromResult(pixels);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _memoryMappedFile.Dispose();
    }
}
