using System.Diagnostics;
using System.Runtime.InteropServices;

namespace UEPluginCompiler.Helpers;

/// <summary>
/// WPF-compatible folder browser dialog using COM IFileDialog (Vista+ style).
/// No WinForms dependency.
/// </summary>
public static class FolderBrowser
{
    public static string? ShowDialog(string? title = null, string? initialDirectory = null)
    {
        try
        {
            var dialog = (IFileOpenDialog)new FileOpenDialog();
            dialog.SetOptions(FOS.FOS_PICKFOLDERS | FOS.FOS_FORCEFILESYSTEM);

            if (title != null)
                dialog.SetTitle(title);

            if (initialDirectory != null && Directory.Exists(initialDirectory))
            {
                var hr = SHCreateItemFromParsingName(initialDirectory, IntPtr.Zero,
                    typeof(IShellItem).GUID, out var folderItemObj);
                if (hr >= 0 && folderItemObj is IShellItem folderItem)
                {
                    dialog.SetFolder(folderItem);
                    Marshal.ReleaseComObject(folderItem);
                }
            }

            var hwnd = Process.GetCurrentProcess().MainWindowHandle;

            if (dialog.Show(hwnd) != 0) // 0 = OK
                return null;

            dialog.GetResult(out var shellItem);
            if (shellItem == null)
                return null;

            shellItem.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var path);
            Marshal.ReleaseComObject(shellItem);
            Marshal.ReleaseComObject(dialog);

            return path;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FolderBrowser failed: {ex.Message}");
            return null;
        }
    }

    // ─── COM types ──────────────────────────────────────────

    [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    private class FileOpenDialog { }

    [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(int cFileTypes, [In, MarshalAs(UnmanagedType.LPArray)] IntPtr[]? rgFilterSpec);
        void SetFileTypeIndex(int iFileType);
        void GetFileTypeIndex(out int piFileType);
        void Advise(IntPtr pfde, out int pdwCookie);
        void Unadvise(int dwCookie);
        void SetOptions(FOS fos);
        void GetOptions(out FOS pfos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int alignment);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close(int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr pFilter);
    }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(int sfgaoMask, out int psfgaoAttribs);
        void Compare(IShellItem psi, int hint, out int piOrder);
    }

    private enum FOS : uint
    {
        FOS_PICKFOLDERS = 0x00000020,
        FOS_FORCEFILESYSTEM = 0x00000040,
    }

    private enum SIGDN : uint
    {
        SIGDN_FILESYSPATH = 0x80058000,
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
}
