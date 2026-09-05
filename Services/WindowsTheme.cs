using System.Runtime.InteropServices;

namespace RecipeItemCreator.Services;

internal static class WindowsTheme
{
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;

    public static void SetDarkTitleBar(nint windowHandle, bool enabled)
    {
        if (!OperatingSystem.IsWindows() || windowHandle == nint.Zero)
            return;

        int value = enabled ? 1 : 0;

        int result = NativeMethods.DwmSetWindowAttribute(
            windowHandle,
            DwmwaUseImmersiveDarkMode,
            ref value,
            sizeof(int));

        if (result != 0)
        {
            _ = NativeMethods.DwmSetWindowAttribute(
                windowHandle,
                DwmwaUseImmersiveDarkModeBefore20H1,
                ref value,
                sizeof(int));
        }
    }

    public static void EnableDarkTitleBar(nint windowHandle)
    {
        SetDarkTitleBar(windowHandle, true);
    }

    public static void DisableDarkTitleBar(nint windowHandle)
    {
        SetDarkTitleBar(windowHandle, false);
    }

    private static class NativeMethods
    {
#pragma warning disable SYSLIB1054
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("dwmapi.dll", ExactSpelling = true)]
        internal static extern int DwmSetWindowAttribute(
            nint hwnd,
            int attribute,
            ref int attributeValue,
            int attributeSize);
#pragma warning restore SYSLIB1054
    }
}