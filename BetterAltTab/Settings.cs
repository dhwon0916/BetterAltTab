using Microsoft.Win32;
using System.Text.Json;

namespace BetterAltTab;

internal sealed record MonitorPreferences(int ScalePercent = 100, bool IncludeMinimized = true);

internal sealed record Settings
{
    public bool FocusedMonitor { get; init; }
    public bool IncludeMinimized { get; init; } = true;
    public bool ShowOverlay { get; init; } = true;
    public int OverlayScalePercent { get; init; } = 100;
    public Keys CycleKey { get; init; } = Keys.Tab;
    public string[] ExcludedApps { get; init; } = [];
    public Dictionary<string, MonitorPreferences> Displays { get; init; } = new();
    public MonitorPreferences ForDisplay(string device) => Displays.TryGetValue(device, out var value) ? value : new(OverlayScalePercent, IncludeMinimized);
    public static readonly Keys[] AllowedKeys = [Keys.Tab, Keys.Oemtilde, Keys.F1, Keys.F2, Keys.F3, Keys.F4, Keys.F6, Keys.F7, Keys.F8, Keys.F9, Keys.F10, Keys.F11, Keys.F12];
    private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BetterAltTab", "settings.json");
    public static Settings Load()
    {
        try
        {
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new();
            return settings with
            {
                CycleKey = AllowedKeys.Contains(settings.CycleKey) ? settings.CycleKey : Keys.Tab,
                OverlayScalePercent = Math.Clamp(settings.OverlayScalePercent, 50, 200),
                ExcludedApps = AppRules.Parse(string.Join(";", settings.ExcludedApps ?? [])),
                Displays = (settings.Displays ?? new()).Where(p => p.Value != null).ToDictionary(p => p.Key, p => p.Value with { ScalePercent = Math.Clamp(p.Value.ScalePercent, 50, 200) })
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(FilePath + ".tmp", FilePath, true);
    }
}

internal static class StartupIntegration
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool Enabled { get { using var key = Registry.CurrentUser.OpenSubKey(Key); return key?.GetValue("BetterAltTab") != null; } }
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        if (enabled) key.SetValue("BetterAltTab", "\"" + Environment.ProcessPath + "\"");
        else key.DeleteValue("BetterAltTab", false);
    }
}
