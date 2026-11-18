using Avalonia.Controls;
using Avalonia.Interactivity;

namespace View;

public partial class RegisterWindow : Window
{
    public RegisterWindow()
    {
        InitializeComponent();
        Opened += (_, _) => EmailBox.Focus();
    }

    private async void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        ErrorText.IsVisible = false;
        SetBusy(true);

        var result = await App.Auth.RegisterAsync(EmailBox.Text, PasswordBox.Text, ConfirmBox.Text);

        if (result.Success)
        {
            App.ShowLogin("Account created. You can sign in now."); // closes this window
            return;
        }

        ErrorText.Text = result.Error;
        ErrorText.IsVisible = true;
        SetBusy(false);
    }

    private void OnBackToLoginClick(object? sender, RoutedEventArgs e) => App.ShowLogin();

    private void OnTogglePasswordClick(object? sender, RoutedEventArgs e)
    {
        var reveal = !PasswordBox.RevealPassword;
        PasswordBox.RevealPassword = reveal;
        ConfirmBox.RevealPassword = reveal;
        ToggleButton.Content = reveal ? "Hide" : "Show";
    }

    private void SetBusy(bool busy)
    {
        BusyBar.IsVisible = busy;
        CreateButton.IsEnabled = !busy;
        BackButton.IsEnabled = !busy;
        EmailBox.IsEnabled = !busy;
        PasswordBox.IsEnabled = !busy;
        ConfirmBox.IsEnabled = !busy;
        CreateButton.Content = busy ? "Creating account…" : "Create account";
    }
}
