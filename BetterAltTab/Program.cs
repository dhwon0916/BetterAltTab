namespace BetterAltTab;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--activation-test") || args.Contains("--test-window") || args.Contains("--feature-test") || args.Contains("--ui-test") || args.Contains("--lifecycle-test"))
        {
            ApplicationConfiguration.Initialize();
            if (args.Contains("--lifecycle-test")) LifecycleCheck.Run(); else if (args.Contains("--ui-test")) UiCheck.Run(); else if (args.Contains("--feature-test")) FeatureCheck.Run(); else ActivationCheck.Run(args);
            return;
        }
        using var mutex = new Mutex(true, @"Local\BetterAltTab", out var first);
        if (!first) { Environment.ExitCode = 2; return; }
        ApplicationConfiguration.Initialize();
        try
        {
            using var app = new TrayApplication(args.Contains("--smoke-test"));
            Application.Run(app);
        }
        catch (Exception ex)
        {
            Environment.ExitCode = 1;
            if (args.Contains("--smoke-test")) File.WriteAllText("smoke-error.txt", ex.ToString());
            else MessageBox.Show(ex.Message, "BetterAltTab could not start", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
