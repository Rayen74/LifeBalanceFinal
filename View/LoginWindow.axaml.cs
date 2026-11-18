using Avalonia.Controls;
using Avalonia.Interactivity;

namespace View;

public partial class LoginWindow : Window
{
    public LoginWindow() : this(null)
    {
    }

    /// <param name="infoMessage">Optional green message, e.g. after a successful registration.</param>
    public LoginWindow(string? infoMessage)
    {
        InitializeComponent();

        if (!string.IsNullOrEmpty(infoMessage))
        {
            InfoText.Text = infoMessage;
            InfoText.IsVisible = true;
            InfoBorder.IsVisible = true;
        }

        Opened += (_, _) => EmailBox.Focus();
    }

    private async void OnSignInClick(object? sender, RoutedEventArgs e)
    {
        ErrorText.IsVisible = false;
        ErrorBorder.IsVisible = false;
        SetBusy(true);

        var result = await App.Auth.LoginAsync(EmailBox.Text, PasswordBox.Text);

        if (result.Success)
        {
            App.ShowDashboard(); // closes this window
            return;
        }

        ErrorText.Text = result.Error;
        ErrorText.IsVisible = true;
        ErrorBorder.IsVisible = true;
        PasswordBox.Text = string.Empty;
        SetBusy(false);
    }

    private void OnCreateAccountClick(object? sender, RoutedEventArgs e) => App.ShowRegister();

    private void OnTogglePasswordClick(object? sender, RoutedEventArgs e)
    {
        PasswordBox.RevealPassword = !PasswordBox.RevealPassword;
        ToggleButton.Content = PasswordBox.RevealPassword ? "Hide" : "Show";
    }

    private void SetBusy(bool busy)
    {
        BusyBar.IsVisible = busy;
        SignInButton.IsEnabled = !busy;
        CreateAccountButton.IsEnabled = !busy;
        EmailBox.IsEnabled = !busy;
        PasswordBox.IsEnabled = !busy;
        SignInButton.Content = busy ? "Signing in…" : "Sign in";
    }
}
