using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UEPluginCompiler.ViewModels;

namespace UEPluginCompiler.Views;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private void PickColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ColorRow row) return;

        var c = ParseHex(row.Hex);
        var cc = new CHOOSECOLOR
        {
            lStructSize = Marshal.SizeOf<CHOOSECOLOR>(),
            hwndOwner = IntPtr.Zero,
            rgbResult = (uint)(c.R | (c.G << 8) | (c.B << 16)),
            Flags = CC_FULLOPEN | CC_RGBINIT,
            lpCustColors = _customColors
        };

        if (ChooseColor(ref cc))
            row.UpdateHex($"#{(cc.rgbResult & 0xFF):X2}{((cc.rgbResult >> 8) & 0xFF):X2}{((cc.rgbResult >> 16) & 0xFF):X2}");
    }

    private static readonly IntPtr _customColors = Marshal.AllocCoTaskMem(16 * 4);
    private static System.Drawing.Color ParseHex(string hex)
    {
        try { var c = (Color)ColorConverter.ConvertFromString(hex); return System.Drawing.Color.FromArgb(c.R, c.G, c.B); }
        catch { return System.Drawing.Color.Gray; }
    }

    // P/Invoke for Windows native color dialog (full wheel + spectrum)
    private const int CC_FULLOPEN = 0x2, CC_RGBINIT = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct CHOOSECOLOR
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public uint rgbResult;
        public IntPtr lpCustColors;
        public int Flags;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Auto)]
    private static extern bool ChooseColor(ref CHOOSECOLOR lpcc);
}
