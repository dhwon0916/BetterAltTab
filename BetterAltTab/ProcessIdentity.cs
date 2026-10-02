using System.Text;

namespace BetterAltTab;

internal static class ProcessIdentity
{
    public static string Name(nint hwnd)
    {
        Native.GetWindowThreadProcessId(hwnd, out var pid);
        return ForProcess(pid);
    }
    public static string ForProcess(uint pid)
    {
        var handle = Native.OpenProcess(0x1000, false, pid);
        if (handle == 0) return "";
        try
        {
            var text = new StringBuilder(1024); uint size = 1024;
            return Native.QueryFullProcessImageName(handle, 0, text, ref size) ? AppRules.Normalize(text.ToString()) : "";
        }
        finally { Native.CloseHandle(handle); }
    }
}
