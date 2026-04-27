using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace FlowEncode;

internal static class NativeFileDialogHelper
{
    private const int OfnExplorer = 0x00080000;
    private const int OfnPathMustExist = 0x00000800;
    private const int OfnFileMustExist = 0x00001000;
    private const int OfnHideReadOnly = 0x00000004;
    private const int OfnNoChangeDir = 0x00000008;
    private const int OfnOverwritePrompt = 0x00000002;
    private const int BufferSize = 32768;

    [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileName(ref OpenFileName openFileName);

    [DllImport("comdlg32.dll", EntryPoint = "GetSaveFileNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSaveFileName(ref OpenFileName openFileName);

    public static string? ShowOpenFileDialog(
        nint ownerWindowHandle,
        string title,
        string initialDirectory,
        params FileDialogFilter[] filters)
    {
        return ShowFileDialog(
            ownerWindowHandle,
            title,
            initialDirectory,
            defaultFileName: null,
            defaultExtension: null,
            saveDialog: false,
            filters);
    }

    public static string? ShowSaveFileDialog(
        nint ownerWindowHandle,
        string title,
        string initialDirectory,
        string defaultFileName,
        string defaultExtension,
        params FileDialogFilter[] filters)
    {
        return ShowFileDialog(
            ownerWindowHandle,
            title,
            initialDirectory,
            defaultFileName,
            defaultExtension,
            saveDialog: true,
            filters);
    }

    private static string? ShowFileDialog(
        nint ownerWindowHandle,
        string title,
        string initialDirectory,
        string? defaultFileName,
        string? defaultExtension,
        bool saveDialog,
        params FileDialogFilter[] filters)
    {
        var fileBuffer = AllocateFileBuffer(defaultFileName);
        var filterBuffer = AllocateString(BuildFilterString(filters));
        var initialDirectoryBuffer = AllocateString(string.IsNullOrWhiteSpace(initialDirectory) ? null : initialDirectory);
        var titleBuffer = AllocateString(string.IsNullOrWhiteSpace(title) ? null : title);
        var defaultExtensionBuffer = AllocateString(NormalizeDefaultExtension(defaultExtension));

        var openFileName = new OpenFileName
        {
            lStructSize = Marshal.SizeOf<OpenFileName>(),
            hwndOwner = ownerWindowHandle,
            lpstrFilter = filterBuffer,
            nFilterIndex = 1,
            lpstrFile = fileBuffer,
            nMaxFile = BufferSize,
            lpstrInitialDir = initialDirectoryBuffer,
            lpstrTitle = titleBuffer,
            lpstrDefExt = defaultExtensionBuffer,
            Flags = saveDialog
                ? OfnExplorer | OfnPathMustExist | OfnHideReadOnly | OfnNoChangeDir | OfnOverwritePrompt
                : OfnExplorer | OfnPathMustExist | OfnFileMustExist | OfnHideReadOnly | OfnNoChangeDir
        };

        try
        {
            var success = saveDialog
                ? GetSaveFileName(ref openFileName)
                : GetOpenFileName(ref openFileName);

            if (!success)
            {
                return null;
            }

            var selectedPath = Marshal.PtrToStringUni(fileBuffer)?.Trim();
            return string.IsNullOrWhiteSpace(selectedPath) ? null : selectedPath;
        }
        finally
        {
            FreeBuffer(fileBuffer);
            FreeBuffer(filterBuffer);
            FreeBuffer(initialDirectoryBuffer);
            FreeBuffer(titleBuffer);
            FreeBuffer(defaultExtensionBuffer);
        }
    }

    private static string BuildFilterString(IReadOnlyList<FileDialogFilter> filters)
    {
        if (filters.Count == 0)
        {
            return "All Files (*.*)\0*.*\0\0";
        }

        var builder = new StringBuilder();
        foreach (var filter in filters)
        {
            builder.Append(filter.Label);
            builder.Append('\0');
            builder.Append(filter.Pattern);
            builder.Append('\0');
        }

        builder.Append('\0');
        return builder.ToString();
    }

    private static string? NormalizeDefaultExtension(string? defaultExtension)
    {
        if (string.IsNullOrWhiteSpace(defaultExtension))
        {
            return null;
        }

        return defaultExtension.Trim().TrimStart('.');
    }

    private static nint AllocateFileBuffer(string? initialValue)
    {
        var byteCount = BufferSize * sizeof(char);
        var buffer = Marshal.AllocHGlobal(byteCount);
        Marshal.Copy(new byte[byteCount], 0, buffer, byteCount);

        if (!string.IsNullOrWhiteSpace(initialValue))
        {
            var chars = $"{initialValue}\0".ToCharArray();
            Marshal.Copy(chars, 0, buffer, Math.Min(chars.Length, BufferSize - 1));
        }

        return buffer;
    }

    private static nint AllocateString(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? nint.Zero
            : Marshal.StringToHGlobalUni(value);
    }

    private static void FreeBuffer(nint buffer)
    {
        if (buffer != nint.Zero)
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    internal readonly record struct FileDialogFilter(string Label, string Pattern);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int lStructSize;
        public nint hwndOwner;
        public nint hInstance;
        public nint lpstrFilter;
        public nint lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public nint lpstrFile;
        public int nMaxFile;
        public nint lpstrFileTitle;
        public int nMaxFileTitle;
        public nint lpstrInitialDir;
        public nint lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public nint lpstrDefExt;
        public nint lCustData;
        public nint lpfnHook;
        public nint lpTemplateName;
        public nint pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

}
