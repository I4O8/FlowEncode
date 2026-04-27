using System;

namespace FlowEncode.Application;

public enum VapourSynthPreviewLogLevel
{
    Information,
    Warning,
    Error
}

public sealed record VapourSynthPreviewLogEntry(
    DateTimeOffset Timestamp,
    VapourSynthPreviewLogLevel Level,
    string Source,
    string Message);

public sealed class VapourSynthPreviewLogEventArgs : EventArgs
{
    public VapourSynthPreviewLogEventArgs(VapourSynthPreviewLogEntry entry)
    {
        Entry = entry;
    }

    public VapourSynthPreviewLogEntry Entry { get; }
}
