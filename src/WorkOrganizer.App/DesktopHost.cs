using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace WorkOrganizer.App;

public sealed class DesktopHost : IDisposable
{
    private readonly Window window;
    private readonly System.Windows.Forms.NotifyIcon tray;
    private nint parent;
    private nint oldStyle;
    private nint oldExtendedStyle;
    private readonly System.Windows.Threading.DispatcherTimer watch = new() { Interval = TimeSpan.FromSeconds(3) };
    public bool Attached { get; private set; }
    public DesktopHost(Window window)
    {
        this.window = window;
        var menu = new System.Windows.Forms.ContextMenuStrip(); menu.Items.Add("일반 창으로 열기", null, (_, _) => window.Dispatcher.Invoke(() => { Detach(); window.Show(); window.Activate(); })); menu.Items.Add("바탕화면 모드 시험", null, (_, _) => window.Dispatcher.Invoke(() => UI.Guard(Attach))); menu.Items.Add("프로그램 종료", null, (_, _) => window.Dispatcher.Invoke(() => window.Close()));
        tray = new System.Windows.Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Text = "업무 정리", Visible = true, ContextMenuStrip = menu }; tray.DoubleClick += (_, _) => window.Dispatcher.Invoke(() => { Detach(); window.Show(); window.Activate(); });
        watch.Tick += (_, _) => { if (Attached && !IsWindow(parent)) { Attached = false; parent = 0; window.WindowStyle = WindowStyle.SingleBorderWindow; window.ShowInTaskbar = true; window.Show(); MessageBox.Show("바탕화면 연결이 바뀌어 일반 창으로 복귀했습니다. 저장된 업무는 유지됩니다."); } }; watch.Start();
    }
    public void Attach()
    {
        if (Attached) return;
        nint progman = FindWindow("Progman", null); if (progman == 0) throw new IOExceptionProxy("Windows 바탕화면을 찾을 수 없습니다. 일반 창을 유지합니다.");
        SendMessageTimeout(progman, 0x052C, 0xD, 0, 2, 1000, out _); SendMessageTimeout(progman, 0x052C, 0xD, 1, 2, 1000, out _);
        nint worker = 0;
        EnumWindows((h, _) => { if (FindWindowEx(h, 0, "SHELLDLL_DefView", null) != 0) worker = FindWindowEx(0, h, "WorkerW", null); return true; }, 0);
        nint host = worker, after = 0;
        nint shell = FindWindowEx(progman, 0, "SHELLDLL_DefView", null);
        nint raisedBackground = FindWindowEx(progman, 0, "WorkerW", null);
        if (shell != 0 && raisedBackground != 0 && (GetWindowLongPtr(progman, -20).ToInt64() & 0x00200000L) != 0)
        {
            // Newer Windows keeps the icon view and background as children of Progman.
            // Add only our own layered child between those existing siblings.
            host = progman; after = shell;
        }
        if (host == 0 || !IsWindow(host)) throw new IOExceptionProxy("아이콘 아래의 바탕화면 영역을 찾지 못했습니다. 이 Windows 환경에서는 일반 창을 유지합니다.");
        nint handle = new WindowInteropHelper(window).Handle; oldStyle = GetWindowLongPtr(handle, -16); oldExtendedStyle = GetWindowLongPtr(handle, -20);
        window.WindowState = WindowState.Normal; window.WindowStyle = WindowStyle.None; window.ShowInTaskbar = false;
        SetWindowLongPtr(handle, -16, (nint)((GetWindowLongPtr(handle, -16).ToInt64() & ~0x80000000L) | 0x40000000L));
        SetWindowLongPtr(handle, -20, (nint)(GetWindowLongPtr(handle, -20).ToInt64() | 0x00080000L)); SetLayeredWindowAttributes(handle, 0, 240, 2);
        SetLastError(0); SetParent(handle, host); int error = Marshal.GetLastWin32Error();
        if (error != 0 || GetParent(handle) != host) { SetWindowLongPtr(handle, -16, oldStyle); SetWindowLongPtr(handle, -20, oldExtendedStyle); SetParent(handle, 0); window.WindowStyle = WindowStyle.SingleBorderWindow; window.ShowInTaskbar = true; throw new Win32Exception(error, "바탕화면 연결에 실패했습니다. 일반 창을 유지합니다."); }
        parent = host; Attached = true; window.Background = UI.Canvas;
        var dpi = VisualTreeHelper.GetDpi(window); SetWindowPos(handle, after, 100, 65, (int)(window.Width * dpi.DpiScaleX), (int)(window.Height * dpi.DpiScaleY), 0x0010 | 0x0040);
    }
    public void Detach()
    {
        if (!Attached) return; var handle = new WindowInteropHelper(window).Handle;
        SetParent(handle, 0); SetWindowLongPtr(handle, -16, oldStyle); SetWindowLongPtr(handle, -20, oldExtendedStyle); Attached = false; parent = 0; window.WindowStyle = WindowStyle.SingleBorderWindow; window.ShowInTaskbar = true; window.Background = UI.Canvas; window.Left = 80; window.Top = 50; window.Show(); window.Activate();
    }
    public void Dispose() { watch.Stop(); if (Attached) Detach(); tray.Dispose(); }
    private sealed class IOExceptionProxy(string text) : Exception(text);
    private delegate bool EnumProc(nint hwnd, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string className, string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindowEx(nint parent, nint after, string className, string? name);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, nint parameter);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetParent(nint child, nint parent);
    [DllImport("user32.dll")] private static extern nint GetParent(nint handle);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint handle);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SendMessageTimeout(nint hwnd, uint msg, nint wparam, nint lparam, uint flags, uint timeout, out nint result);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetLayeredWindowAttributes(nint hwnd, uint key, byte alpha, uint flags);
    [DllImport("kernel32.dll")] private static extern void SetLastError(uint error);
}
