using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Controller;

namespace View;

public partial class App : Application
{
    /// <summary>The one AuthService shared by every screen (it holds who is signed in).</summary>
    public static AuthService Auth { get; } = new();

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The first screen is chosen BEFORE any window exists, so Login never flashes
            // on screen when a valid session is restored.
            desktop.MainWindow = TryRestoreSession() ? new MainWindow() : new LoginWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    // ---------- Navigation: one window is shown at a time ----------

    public static void ShowLogin(string? infoMessage = null) => SwitchTo(new LoginWindow(infoMessage));

    public static void ShowRegister() => SwitchTo(new RegisterWindow());

    /// <summary>The dashboard can only be opened by a signed-in user.</summary>
    public static void ShowDashboard()
    {
        if (!Auth.IsAuthenticated)
        {
            ShowLogin();
            return;
        }

        SwitchTo(new MainWindow());
    }

    private static void SwitchTo(Window next)
    {
        if (Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var previous = desktop.MainWindow;
        desktop.MainWindow = next;
        next.Show();
        previous?.Close();
    }

    /// <summary>Checks the saved session (max ~6 s) so the app never hangs when PostgreSQL is offline.</summary>
    private static bool TryRestoreSession()
    {
        if (!Auth.HasSavedSession)
            return false;

        var restore = Task.Run(Auth.RestoreSessionAsync);
        return restore.Wait(TimeSpan.FromSeconds(6)) && restore.Result;
    }
}
