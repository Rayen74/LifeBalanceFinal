using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
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

    // ================= NAVIGATION SWITCHING =================

    private void OnNavDashboardClick(object? sender, PointerPressedEventArgs e)
    {
        SwitchView(0);
    }

    private async void OnNavKanbanClick(object? sender, PointerPressedEventArgs e)
    {
        SwitchView(1);
        await KanbanViewControl.LoadBoardsAsync();
    }

    private async void OnNavHabitsClick(object? sender, PointerPressedEventArgs e)
    {
        SwitchView(2);
        await HabitsViewControl.LoadHabitsAsync();
    }

    private async void OnNavGoalsClick(object? sender, PointerPressedEventArgs e)
    {
        SwitchView(3);
        await GoalsViewControl.LoadGoalsAsync();
    }

    private void SwitchView(int viewIndex)
    {
        // Reset Nav styles
        ResetNavStyle(NavDashboard, IconDashboard, TextDashboard);
        ResetNavStyle(NavKanban, IconKanban, TextKanban);
        ResetNavStyle(NavHabits, IconHabits, TextHabits);
        ResetNavStyle(NavGoals, IconGoals, TextGoals);

        // Hide all views
        DashboardContainer.IsVisible = false;
        KanbanViewControl.IsVisible = false;
        HabitsViewControl.IsVisible = false;
        GoalsViewControl.IsVisible = false;

        switch (viewIndex)
        {
            case 0:
                SetActiveNav(NavDashboard, IconDashboard, TextDashboard);
                DashboardContainer.IsVisible = true;
                HeaderTitle.Text = "Life Balance Overview";
                HeaderSubtitle.Text = "Assess and maintain fulfillment across your core life dimensions.";
                break;
            case 1:
                SetActiveNav(NavKanban, IconKanban, TextKanban);
                KanbanViewControl.IsVisible = true;
                HeaderTitle.Text = "Tasks & Kanban Boards";
                HeaderSubtitle.Text = "Organize tasks, track progress across columns, and maintain productivity.";
                break;
            case 2:
                SetActiveNav(NavHabits, IconHabits, TextHabits);
                HabitsViewControl.IsVisible = true;
                HeaderTitle.Text = "Daily Habits & Streaks";
                HeaderSubtitle.Text = "Build consistent routines, mark daily completions, and build momentum.";
                break;
            case 3:
                SetActiveNav(NavGoals, IconGoals, TextGoals);
                GoalsViewControl.IsVisible = true;
                HeaderTitle.Text = "Personal Goals";
                HeaderSubtitle.Text = "Define measurable milestones, track target dates, and link actionable tasks.";
                break;
        }
    }

    private void SetActiveNav(Border item, Avalonia.Controls.Shapes.Path icon, TextBlock text)
    {
        item.Classes.Add("active");
        icon.Fill = ColorBrush("#2563EB");
        text.Foreground = ColorBrush("#2563EB");
        text.FontWeight = FontWeight.SemiBold;
    }

    private void ResetNavStyle(Border item, Avalonia.Controls.Shapes.Path icon, TextBlock text)
    {
        item.Classes.Remove("active");
        icon.Fill = ColorBrush("#7B879B");
        text.Foreground = ColorBrush("#17243B");
        text.FontWeight = FontWeight.Normal;
    }

    private static SolidColorBrush ColorBrush(string hex) => new(Color.Parse(hex));
}
