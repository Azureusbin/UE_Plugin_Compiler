using System.Windows;
using UEPluginCompiler.Helpers;

namespace UEPluginCompiler;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
#if DEBUG
        // Catch unhandled exceptions — debug only
        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Log($"UNHANDLED DISPATCHER: {args.Exception}");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Logger.Log($"UNHANDLED APPDOMAIN: {args.ExceptionObject}");
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Logger.Log($"UNHANDLED TASK: {args.Exception}");
        };
#endif

        base.OnStartup(e);
        Logger.Log("App startup");

        // Single-instance mutex check
        var mutexName = "UEPluginCompiler_SingleInstance";
        var createdNew = false;
        var mutex = new Mutex(true, mutexName, out createdNew);

        if (!createdNew)
        {
            Logger.Log("Another instance, activating existing");
            mutex?.Dispose();
            ActivateExistingWindow();
            Shutdown();
            return;
        }
    }

    private static void ActivateExistingWindow()
    {
        var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        var processes = System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName);

        foreach (var process in processes)
        {
            if (process.Id != currentProcess.Id && process.MainWindowHandle != IntPtr.Zero)
            {
                SetForegroundWindow(process.MainWindowHandle);
                ShowWindow(process.MainWindowHandle, SW_RESTORE);
                break;
            }
        }
    }

    private const int SW_RESTORE = 9;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
