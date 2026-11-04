using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Voica.UI;

public partial class PreviewOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int NoActivate = 0x08000000;
    private const int ToolWindow = 0x00000080;
    private const int Transparent = 0x00000020;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr window, int index, int style);

    public PreviewOverlayWindow() => InitializeComponent();

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        SetWindowLong(handle, GwlExStyle, GetWindowLong(handle, GwlExStyle) | NoActivate | ToolWindow | Transparent);
    }

    public void SetText(string text) => PreviewText.Text = text;
}
