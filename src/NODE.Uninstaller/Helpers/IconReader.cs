using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.RegularExpressions;

namespace NODE.Uninstaller.Helpers;

internal static class IconReader
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiSmallIcon = 0x000000001;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public IntPtr Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string path, uint fileAttributes, out ShellFileInfo info, uint infoSize, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr iconHandle);

    public static ImageSource? TryLoad(string? rawIconPath)
    {
        if (string.IsNullOrWhiteSpace(rawIconPath)) return null;

        try
        {
            var path = Environment.ExpandEnvironmentVariables(rawIconPath.Trim());
            path = Regex.Replace(path, @",\s*-?\d+\s*$", "").Trim().Trim('"');
            if (!File.Exists(path)) return null;

            var status = SHGetFileInfo(path, 0, out var info,
                (uint)Marshal.SizeOf<ShellFileInfo>(), ShgfiIcon | ShgfiSmallIcon);
            if (status == IntPtr.Zero || info.Icon == IntPtr.Zero) return null;

            try
            {
                BitmapSource bmp = Imaging.CreateBitmapSourceFromHIcon(
                    info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bmp.Freeze();
                return bmp;
            }
            finally
            {
                DestroyIcon(info.Icon);
            }
        }
        catch (Exception)
        {
            return null;
        }
    }
}
