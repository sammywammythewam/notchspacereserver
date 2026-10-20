using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace NotchSpaceReserver;

public partial class Form1 : Form
{
    private const int ReservedHeight = 50;
    private const uint AppBarNew = 0;
    private const uint AppBarRemove = 1;
    private const uint AppBarQueryPosition = 2;
    private const uint AppBarSetPosition = 3;
    private const uint AppBarPositionChanged = 1;
    private const uint AppBarEdgeTop = 1;
    private const int ExtendedStyleToolWindow = 0x00000080;
    private const int ExtendedStyleAppWindow = 0x00040000;
    private const uint SetWindowPosNoActivate = 0x0010;
    private const uint SetWindowPosNoZOrder = 0x0004;

    private readonly uint callbackMessage;
    private bool appBarRegistered;

    public Form1()
    {
        InitializeComponent();
        callbackMessage = RegisterWindowMessage("NotchSpaceReserver.AppBarCallback");
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Text = "Notch Space Reserver";
        BackColor = Color.Fuchsia;
        TransparencyKey = Color.Fuchsia;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle = (parameters.ExStyle | ExtendedStyleToolWindow) & ~ExtendedStyleAppWindow;
            return parameters;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (callbackMessage == 0)
        {
            ShowAppBarError(new Win32Exception(Marshal.GetLastWin32Error()));
            return;
        }

        var data = CreateAppBarData();
        if (SHAppBarMessage(AppBarNew, ref data) == IntPtr.Zero)
        {
            ShowAppBarError(new InvalidOperationException("Windows could not register the top-space reservation."));
            return;
        }

        appBarRegistered = true;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        PositionAppBar();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

        if (appBarRegistered)
        {
            var data = CreateAppBarData();
            SHAppBarMessage(AppBarRemove, ref data);
            appBarRegistered = false;
        }

        base.OnFormClosed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if ((uint)m.Msg == callbackMessage && unchecked((uint)m.WParam.ToInt32()) == AppBarPositionChanged)
        {
            PositionAppBar();
        }

        base.WndProc(ref m);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (appBarRegistered && IsHandleCreated)
        {
            BeginInvoke(PositionAppBar);
        }
    }

    private void PositionAppBar()
    {
        var screen = Screen.PrimaryScreen
            ?? throw new InvalidOperationException("Windows did not report a primary display.");
        var bounds = screen.Bounds;
        var data = CreateAppBarData();
        data.Edge = AppBarEdgeTop;
        data.Rect = new NativeRect
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Right = bounds.Right,
            Bottom = bounds.Top + ReservedHeight
        };

        SHAppBarMessage(AppBarQueryPosition, ref data);
        data.Rect.Bottom = data.Rect.Top + ReservedHeight;

        if (SHAppBarMessage(AppBarSetPosition, ref data) == IntPtr.Zero)
        {
            ShowAppBarError(new InvalidOperationException("Windows could not reserve the top 50 pixels."));
            return;
        }

        if (!SetWindowPos(
                Handle,
                IntPtr.Zero,
                data.Rect.Left,
                data.Rect.Top,
                data.Rect.Right - data.Rect.Left,
                data.Rect.Bottom - data.Rect.Top,
                SetWindowPosNoActivate | SetWindowPosNoZOrder))
        {
            ShowAppBarError(new Win32Exception(Marshal.GetLastWin32Error()));
        }
    }

    private NativeAppBarData CreateAppBarData() => new()
    {
        Size = (uint)Marshal.SizeOf<NativeAppBarData>(),
        Window = Handle,
        CallbackMessage = callbackMessage
    };

    private void ShowAppBarError(Exception exception)
    {
        MessageBox.Show(
            this,
            $"Notch Space Reserver could not reserve the top of the screen.\n\n{exception.Message}",
            "Notch Space Reserver",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        Close();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeAppBarData
    {
        public uint Size;
        public IntPtr Window;
        public uint CallbackMessage;
        public uint Edge;
        public NativeRect Rect;
        public IntPtr Parameter;
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern IntPtr SHAppBarMessage(uint message, ref NativeAppBarData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string message);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
