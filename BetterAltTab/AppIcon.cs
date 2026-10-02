namespace BetterAltTab;

internal static class AppIcon
{
    // Shared for the process lifetime by tray and application windows.
    public static Icon Value { get; } = Load();

    private static Icon Load()
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("BetterAltTab.AppIcon.ico")
            ?? throw new InvalidOperationException("The application icon resource is missing.");
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }
}
