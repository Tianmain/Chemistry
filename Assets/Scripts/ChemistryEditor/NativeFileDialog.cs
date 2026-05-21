using UnityEngine;
using System;
using System.Runtime.InteropServices;
using System.IO;

/// <summary>
/// Windows 原生文件对话框封装（仅 Windows 独立平台有效）
/// </summary>
public static class NativeFileDialog
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class OpenFileName
    {
        public int lStructSize = Marshal.SizeOf(typeof(OpenFileName));
        public IntPtr hwndOwner = IntPtr.Zero;
        public IntPtr hInstance = IntPtr.Zero;
        public string lpstrFilter = null;
        public string lpstrCustomFilter = null;
        public int nMaxCustFilter = 0;
        public int nFilterIndex = 1;
        public string lpstrFile = null;
        public int nMaxFile = 0;
        public string lpstrFileTitle = null;
        public int nMaxFileTitle = 0;
        public string lpstrInitialDir = null;
        public string lpstrTitle = null;
        public int Flags = 0;
        public short nFileOffset = 0;
        public short nFileExtension = 0;
        public string lpstrDefExt = null;
        public IntPtr lCustData = IntPtr.Zero;
        public IntPtr lpfnHook = IntPtr.Zero;
        public string lpTemplateName = null;
        public IntPtr pvReserved = IntPtr.Zero;
        public int dwReserved = 0;
        public int FlagsEx = 0;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetOpenFileName(OpenFileName ofn);

    [DllImport("comdlg32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetSaveFileName(OpenFileName ofn);

    private const int OFN_EXPLORER = 0x00080000;
    private const int OFN_FILEMUSTEXIST = 0x00001000;
    private const int OFN_OVERWRITEPROMPT = 0x00000002;

    /// <summary>
    /// 打开文件对话框，返回完整路径；用户取消则返回 null
    /// </summary>
    public static string OpenFilePanel(string title, string directory, string extension)
    {
        string filter = string.IsNullOrEmpty(extension)
            ? "All Files\0*.*\0\0"
            : $"{extension.ToUpper()} Files\0*.{extension}\0All Files\0*.*\0\0";

        var ofn = new OpenFileName
        {
            lpstrFilter = filter,
            lpstrTitle = title,
            lpstrInitialDir = EnsureDir(directory),
            lpstrFile = new string('\0', 512),
            nMaxFile = 512,
            Flags = OFN_EXPLORER | OFN_FILEMUSTEXIST,
        };

        if (GetOpenFileName(ofn))
        {
            return ofn.lpstrFile.TrimEnd('\0');
        }
        return null;
    }

    /// <summary>
    /// 保存文件对话框，返回完整路径；用户取消则返回 null
    /// </summary>
    public static string SaveFilePanel(string title, string directory, string defaultName, string extension)
    {
        string filter = string.IsNullOrEmpty(extension)
            ? "All Files\0*.*\0\0"
            : $"{extension.ToUpper()} Files\0*.{extension}\0All Files\0*.*\0\0";

        string fileName = defaultName ?? "scene.json";
        if (!fileName.EndsWith($".{extension}"))
            fileName = Path.ChangeExtension(fileName, extension);

        var ofn = new OpenFileName
        {
            lpstrFilter = filter,
            lpstrTitle = title,
            lpstrInitialDir = EnsureDir(directory),
            lpstrFile = fileName.PadRight(512, '\0'),
            nMaxFile = 512,
            lpstrDefExt = extension,
            Flags = OFN_EXPLORER | OFN_OVERWRITEPROMPT,
        };

        if (GetSaveFileName(ofn))
        {
            return ofn.lpstrFile.TrimEnd('\0');
        }
        return null;
    }

    private static string EnsureDir(string dir)
    {
        if (string.IsNullOrEmpty(dir)) return null;
        if (Directory.Exists(dir)) return dir;
        try { Directory.CreateDirectory(dir); return dir; }
        catch { return null; }
    }
#else
    /// <summary>
    /// 非 Windows 平台占位，返回 null（可后续扩展 Mac/Linux）
    /// </summary>
    public static string OpenFilePanel(string title, string directory, string extension)
    {
        Debug.LogWarning("[NativeFileDialog] 仅支持 Windows 平台");
        return null;
    }

    public static string SaveFilePanel(string title, string directory, string defaultName, string extension)
    {
        Debug.LogWarning("[NativeFileDialog] 仅支持 Windows 平台");
        return null;
    }
#endif
}
