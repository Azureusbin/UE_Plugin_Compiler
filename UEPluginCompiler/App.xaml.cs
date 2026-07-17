using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using UEPluginCompiler.Helpers;

namespace UEPluginCompiler;

public partial class App : Application
{
    private Mutex? _mutex;
    private EventWaitHandle? _wakeEvent;
    private CancellationTokenSource? _wakeCts;

    private const string MutexName = "UEPluginCompiler_SingleInstance_v2";
    private const string WakeEventName = "UEPluginCompiler_WakeEvent_v2";

    protected override void OnStartup(StartupEventArgs e)
    {
#if DEBUG
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

        _mutex = new Mutex(true, MutexName, out bool createdNew);

        if (!createdNew)
        {
            // ── Second instance: signal existing and exit ──
            Logger.Log("Another instance detected — signaling to wake up");
            _mutex.Dispose();
            _mutex = null;

            try
            {
                using var wake = EventWaitHandle.OpenExisting(WakeEventName);
                wake.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // Rare race: first instance's event isn't ready yet
                Logger.Log("Wake event not found — first instance may still be starting");
            }

            // Small delay so the first instance can process the signal
            Thread.Sleep(50);
            Shutdown();
            return;
        }

        // ── First instance: create wake event and listen ──
        _wakeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, WakeEventName);
        _wakeCts = new CancellationTokenSource();
        StartWakeListener(_wakeCts.Token);

        base.OnStartup(e);
        Logger.Log("App startup");
    }

    // ─── Background listener ────────────────────────────────────

    private void StartWakeListener(CancellationToken ct)
    {
        var wakeEvent = _wakeEvent!;
        var dispatcher = Dispatcher;

        var thread = new Thread(() =>
        {
            var handles = new WaitHandle[] { wakeEvent, ct.WaitHandle };

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    int signaled = WaitHandle.WaitAny(handles);
                    if (signaled == 0) // wakeEvent
                    {
                        dispatcher.BeginInvoke(() => ActivateMainWindow());
                    }
                    else // cancellation
                    {
                        break;
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        })
        {
            IsBackground = true,
            Name = "WakeListener"
        };
        thread.Start();
    }

    // ─── Self-activation (runs on UI thread) ────────────────────

    private void ActivateMainWindow()
    {
        var window = MainWindow;
        if (window == null) return;

        Logger.Log("Activating main window from wake signal");

        // 1. Restore from minimized / hidden state
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;

        window.Show();

        // 2. Topmost toggle — forces window to top of Z-order on Win10/11
        window.Topmost = true;

        // 3. Activate + Focus
        window.Activate();
        window.Focus();

        // 4. Remove Topmost shortly after (async, low priority)
        _ = Task.Run(async () =>
        {
            await Task.Delay(200);
            await Dispatcher.BeginInvoke(() =>
            {
                if (window != null)
                    window.Topmost = false;
            });
        });

        // 5. Flash taskbar icon to catch user's eye
        FlashTaskbar(window);
    }

    // ─── Taskbar flash ──────────────────────────────────────────

    private static void FlashTaskbar(Window window)
    {
        var hWnd = new WindowInteropHelper(window).Handle;
        if (hWnd == IntPtr.Zero) return;

        var info = new FLASHWINFO
        {
            cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
            hwnd = hWnd,
            dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
            uCount = 3,
            dwTimeout = 0
        };
        FlashWindowEx(ref info);
        Logger.Log("Taskbar flash sent");
    }

    // ─── Cleanup ────────────────────────────────────────────────

    protected override void OnExit(ExitEventArgs e)
    {
        _wakeCts?.Cancel();
        _wakeCts?.Dispose();
        _wakeCts = null;

        _wakeEvent?.Dispose();
        _wakeEvent = null;

        _mutex?.Dispose();
        _mutex = null;

        base.OnExit(e);
    }

    // ─── P/Invoke ───────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    private const uint FLASHW_ALL = 3;
    private const uint FLASHW_TIMERNOFG = 12;

    [DllImport("user32.dll")]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);
}
