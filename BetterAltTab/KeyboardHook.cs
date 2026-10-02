using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace BetterAltTab;

internal enum SwitchAction { Forward, Backward, Up, Down, Commit, Cancel, Backspace }

internal sealed class KeyboardHook : IDisposable
{
    private readonly Native.HookProc callback;
    private readonly Action<SwitchAction> post;
    internal const uint TestInputTag = 0x42415442;
    private readonly bool acceptTestInput;
    public Action<string>? TextEntered { get; set; }
    public volatile string[] ExcludedApps = [];
    private readonly HashSet<uint> swallowed = new();
    private readonly byte[] keyboardState = new byte[256];
    private readonly StringBuilder translatedText = new(8);
    private nint hook;
    private volatile bool switching;
    private bool nativeGesture;
    public volatile bool Enabled = true;
    public volatile Keys CycleKey = Keys.Tab;
    private readonly Thread thread;
    private Control? hookDispatcher;
    private int disposed;
    internal bool IsRunning => thread.IsAlive;
    public KeyboardHook(Action<SwitchAction> post, bool acceptTestInput = false, bool enabled = true)
    {
        this.post = post;
        this.acceptTestInput = acceptTestInput;
        Enabled = enabled;
        callback = OnKey;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        thread = new Thread(() =>
        {
            try
            {
                using var control = new Control();
                _ = control.Handle;
                hookDispatcher = control;
                hook = Native.SetWindowsHookEx(13, callback, Native.GetModuleHandle(null), 0);
                if (hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                ready.TrySetResult();
                Application.Run();
            }
            catch (Exception ex)
            {
                ready.TrySetException(ex);
                Diagnostics.Write("Keyboard hook thread stopped: " + ex.GetType().Name);
            }
            finally { if (hook != 0) Native.UnhookWindowsHookEx(hook); hook = 0; }
        })
        { IsBackground = true, Name = "BetterAltTab keyboard hook" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try { ready.Task.GetAwaiter().GetResult(); }
        catch (Exception ex) { thread.Join(2000); throw new InvalidOperationException("Could not install keyboard hook.", ex); }
    }
    private static bool Down(Keys key) => Native.GetAsyncKeyState((int)key) < 0;
    private nint OnKey(int code, nint message, nint data)
    {
        try { return ProcessKey(code, message, data); }
        catch (Exception ex)
        {
            // Managed exceptions must never cross the unmanaged hook boundary.
            switching = false;
            Diagnostics.Write("Keyboard callback failed: " + ex.GetType().Name);
            try { post(SwitchAction.Cancel); } catch (Exception) { }
            return Native.CallNextHookEx(hook, code, message, data);
        }
    }
    private nint ProcessKey(int code, nint message, nint data)
    {
        if (code < 0) return Native.CallNextHookEx(hook, code, message, data);
        var key = Marshal.PtrToStructure<Native.Keyboard>(data);
        if ((key.Flags & 0x10) != 0 && !(acceptTestInput && key.Extra == TestInputTag)) return Native.CallNextHookEx(hook, code, message, data);
        var up = message == 0x101 || message == 0x105;
        if (up && swallowed.Remove(key.Key)) return 1;
        if (up && key.Key is 0xA4 or 0xA5 or 0x12)
        {
            var otherAlt = key.Key == 0xA4 ? Keys.RMenu : Keys.LMenu;
            if (!Down(otherAlt)) nativeGesture = false;
            if (switching && !Down(otherAlt)) { switching = false; post(SwitchAction.Commit); }
        }
        if (!up && switching && key.Key == (uint)Keys.Escape)
        {
            switching = false; swallowed.Add(key.Key); post(SwitchAction.Cancel); return 1;
        }
        if (!up && switching && key.Key == (uint)Keys.Enter)
        {
            switching = false; swallowed.Add(key.Key); post(SwitchAction.Commit); return 1;
        }
        if (!up && switching && key.Key != (uint)CycleKey && !Down(Keys.ControlKey) && !Down(Keys.LWin) && !Down(Keys.RWin))
        {
            SwitchAction? action = (Keys)key.Key switch
            {
                Keys.Left => SwitchAction.Backward,
                Keys.Right => SwitchAction.Forward,
                Keys.Up => SwitchAction.Up,
                Keys.Down => SwitchAction.Down,
                Keys.Back => SwitchAction.Backspace,
                _ => null
            };
            if (action != null) { swallowed.Add(key.Key); post(action.Value); return 1; }
            // Decode with the foreground keyboard layout but without Alt, which is
            // the switcher modifier. Flag 4 leaves the keyboard dead-key state intact.
            if (key.Key is >= 0x20 and <= 0xFE && key.Key is not (>= 0x70 and <= 0xA5))
            {
                Array.Clear(keyboardState);
                if (Down(Keys.ShiftKey)) keyboardState[0x10] = 0x80;
                keyboardState[0x14] = (byte)(Native.GetKeyState(0x14) & 1);
                var foregroundThread = Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out _);
                translatedText.Clear();
                int length = Native.ToUnicodeEx(key.Key, key.Scan, keyboardState, translatedText, translatedText.Capacity, 4, Native.GetKeyboardLayout(foregroundThread));
                if (length > 0 && translatedText.Length > 0 && !char.IsControl(translatedText[0]))
                { swallowed.Add(key.Key); TextEntered?.Invoke(translatedText.ToString()); return 1; }
            }
        }
        // Ctrl excludes AltGr; Win combinations belong to the shell.
        if (!up && Enabled && key.Key == (uint)CycleKey && (key.Flags & 0x20) != 0 && !Down(Keys.ControlKey) && !Down(Keys.LWin) && !Down(Keys.RWin))
        {
            if (nativeGesture) return Native.CallNextHookEx(hook, code, message, data);
            if (!switching && ExcludedApps.Length > 0)
            {
                var name = ProcessIdentity.Name(Native.GetForegroundWindow());
                if (name.Length == 0 || AppRules.Matches(name, ExcludedApps))
                {
                    nativeGesture = true;
                    Diagnostics.Write($"Native shortcut bypass for {name}");
                    return Native.CallNextHookEx(hook, code, message, data);
                }
            }
            switching = true; swallowed.Add(key.Key);
            post(Down(Keys.ShiftKey) ? SwitchAction.Backward : SwitchAction.Forward);
            return 1;
        }
        return Native.CallNextHookEx(hook, code, message, data);
    }
    public void Cancel() { switching = false; }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        Enabled = false;
        try { if (hookDispatcher is { IsDisposed: false }) hookDispatcher.BeginInvoke(Application.ExitThread); }
        catch (InvalidOperationException) { /* The hook thread already shut down. */ }
        thread.Join(2000);
    }
}
