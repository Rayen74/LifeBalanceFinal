using Avalonia.Controls;
using Avalonia.Interactivity;
using Controller;

namespace View;

public partial class MainWindow : Window
{
    private readonly DashboardController _controller = new();

    public MainWindow()
    {
        InitializeComponent();

        AreasList.ItemsSource = _controller.Areas;
        foreach (var area in _controller.Areas)
            area.PropertyChanged += (_, _) => UpdateBalance();
        UpdateBalance();

        UserText.Text = App.Auth.CurrentUser?.Email;

        Opened += async (_, _) =>
        {
            // Safety net: the dashboard never stays open without a signed-in user.
            if (!App.Auth.IsAuthenticated)
            {
                App.ShowLogin();
                return;
            }

            await RefreshDatabaseStatusAsync();
        };
    }

    private void OnSignOutClick(object? sender, RoutedEventArgs e)
    {
        App.Auth.Logout();
        App.ShowLogin();
    }

    private void UpdateBalance()
    {
        BalanceText.Text = _controller.BalanceScore.ToString("0.0");
        BalanceMessage.Text = _controller.BalanceMessage;
    }

    private async Task RefreshDatabaseStatusAsync()
    {
        SetPill("Checking database…", null);
        var ok = await _controller.CanConnectToDatabaseAsync();
        SetPill(ok ? "● Database connected" : "● Database offline", ok);
    }

    private void SetPill(string text, bool? ok)
    {
        DbStatusText.Text = text;
        DbPill.Classes.Remove("ok");
        DbPill.Classes.Remove("bad");
        if (ok == true) DbPill.Classes.Add("ok");
        else if (ok == false) DbPill.Classes.Add("bad");
    }

    private async void OnRetryClick(object? sender, RoutedEventArgs e) =>
        await RefreshDatabaseStatusAsync();

    private void OnIncreaseClick(object? sender, RoutedEventArgs e) => Adjust(sender, +1);
    private void OnDecreaseClick(object? sender, RoutedEventArgs e) => Adjust(sender, -1);

    private static void Adjust(object? sender, int delta)
    {
        if ((sender as Button)?.DataContext is LifeArea area)
            area.Score += delta;
    }
}
