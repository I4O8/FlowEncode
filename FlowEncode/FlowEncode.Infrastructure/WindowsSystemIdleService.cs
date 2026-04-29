using System.Runtime.InteropServices;
using FlowEncode.Application;

namespace FlowEncode.Infrastructure;

public sealed class WindowsSystemIdleService : ISystemIdleService
{
    public TimeSpan GetIdleDuration()
    {
        var lastInputInfo = new LastInputInfo
        {
            cbSize = (uint)Marshal.SizeOf<LastInputInfo>()
        };

        if (!GetLastInputInfo(ref lastInputInfo))
        {
            return TimeSpan.Zero;
        }

        var currentTick = unchecked((uint)Environment.TickCount64);
        var idleMilliseconds = currentTick - lastInputInfo.dwTime;
        return TimeSpan.FromMilliseconds(idleMilliseconds);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetLastInputInfo(ref LastInputInfo plii);
}
