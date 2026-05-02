using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System;
using System.Windows.Media;
using System.Windows.Controls;
using PersonelTakip.Monitoring.ViewModels;

namespace PersonelTakip.Monitoring.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainViewModel();
        DataContext = viewModel;
        Loaded += async (s, e) =>
        {
            await viewModel.LoadAllDataAsync();
        };
        SourceInitialized += MainWindow_SourceInitialized;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        nint handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
    }

    private nint WindowProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024;
        if (msg == WM_GETMINMAXINFO)
        {
            MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO))!;
            nint monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                MONITORINFO monitorInfo = new MONITORINFO();
                GetMonitorInfo(monitor, monitorInfo);
                RECT rcWorkArea = monitorInfo.rcWork;
                RECT rcMonitorArea = monitorInfo.rcMonitor;
                mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.left - rcMonitorArea.left);
                mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.top - rcMonitorArea.top);
                mmi.ptMaxSize.x = Math.Abs(rcWorkArea.right - rcWorkArea.left);
                mmi.ptMaxSize.y = Math.Abs(rcWorkArea.bottom - rcWorkArea.top);

                // Minimum boyut sınırlarını belirle (DPI uyumlu)
                var dpi = VisualTreeHelper.GetDpi(this);
                mmi.ptMinTrackSize.x = (int)(this.MinWidth * dpi.DpiScaleX);
                mmi.ptMinTrackSize.y = (int)(this.MinHeight * dpi.DpiScaleY);
            }
            Marshal.StructureToPtr(mmi, lParam, true);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint handle, int flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(nint hMonitor, MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
        public POINT(int x, int y) { this.x = x; this.y = y; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class MONITORINFO
    {
        public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
        public RECT rcMonitor = new RECT();
        public RECT rcWork = new RECT();
        public int dwFlags = 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left, top, right, bottom;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        
        // Eğer açık olan başka pencereler yoksa (veya bu ana pencereyse) uygulamayı kapat
        if (Application.Current != null && Application.Current.Windows.Count == 0)
        {
            Application.Current.Shutdown();
        }
    }

    private bool _isClosingWorkDone = false;
    protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isClosingWorkDone && DataContext is MainViewModel vm && vm.CurrentUser != null)
        {
            e.Cancel = true; // Kapanmayı geçici olarak durdur
            try 
            {
                var db = new Services.DatabaseService();
                await db.LogoutAsync(vm.CurrentUser.Id);
                await db.BildirimEkleAsync($"{vm.CurrentUser.KullaniciAdi} oturumu kapattı.", vm.CurrentUser.Id);
            } 
            catch { }
            
            _isClosingWorkDone = true;
            this.Close(); // İşlem bitince tekrar kapat
            return;
        }
        base.OnClosing(e);
    }

    private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu && contextMenu.PlacementTarget is TextBlock textBlock)
        {
            Clipboard.SetText(textBlock.Text);
        }
    }
}
