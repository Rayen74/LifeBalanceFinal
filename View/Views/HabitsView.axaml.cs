using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Controller;
using Model;
using View.Dialogs;

namespace View.Views;

public partial class HabitsView : UserControl
{
    private readonly HabitService _habitService = new();

    public HabitsView()
    {
        InitializeComponent();
        AttachedToVisualTree += async (_, _) => await LoadHabitsAsync();
    }

    public async Task LoadHabitsAsync()
    {
        var user = App.Auth.CurrentUser;
        if (user == null) return;

        var habits = await _habitService.GetUserHabitsAsync(user.Id);
        HabitsContainer.Children.Clear();

        if (habits.Count == 0)
        {
            EmptyNotice.IsVisible = true;
            return;
        }

        EmptyNotice.IsVisible = false;

        foreach (var habit in habits)
        {
            var card = await CreateHabitCardAsync(habit, user.Id);
            HabitsContainer.Children.Add(card);
        }
    }

    private async Task<Border> CreateHabitCardAsync(Habit habit, Guid userId)
    {
        var streak = await _habitService.CalculateCurrentStreakAsync(userId, habit.Id);

        var card = new Border
        {
            Background = ColorBrush("#FFFFFF"),
            BorderBrush = ColorBrush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16)
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto, Auto")
        };

        // Top Row: Habit Title, Target/Description, Streak Badge, Actions
        var topGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*, Auto, Auto")
        };

        var titleStack = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        var nameText = new TextBlock
        {
            Text = habit.Name,
            FontSize = 15,
            FontWeight = FontWeight.Bold,
            Foreground = ColorBrush("#14264D")
        };
        if (!habit.IsActive)
        {
            nameText.Foreground = ColorBrush("#7B879B");
            nameText.Text += " (Paused)";
        }
        titleStack.Children.Add(nameText);

        if (!string.IsNullOrWhiteSpace(habit.Description))
        {
            var descText = new TextBlock
            {
                Text = habit.Description,
                FontSize = 12,
                Foreground = ColorBrush("#7B879B")
            };
            titleStack.Children.Add(descText);
        }
        Grid.SetColumn(titleStack, 0);

        // Streak Pill
        var streakBorder = new Border
        {
            Background = ColorBrush(streak > 0 ? "#DCFCE7" : "#F1F5F9"),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 4),
            Margin = new Thickness(10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var streakText = new TextBlock
        {
            Text = $"{streak} day streak",
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = ColorBrush(streak > 0 ? "#16A34A" : "#7B879B")
        };
        streakBorder.Child = streakText;
        Grid.SetColumn(streakBorder, 1);

        // Actions: Toggle Active, Delete
        var actionsStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var toggleBtn = new Button
        {
            Content = habit.IsActive ? "Pause" : "Resume",
            FontSize = 11,
            Padding = new Thickness(8, 4)
        };
        toggleBtn.Classes.Add("ghost");
        toggleBtn.Click += async (_, _) =>
        {
            await _habitService.SetHabitActiveAsync(userId, habit.Id, !habit.IsActive);
            await LoadHabitsAsync();
        };
        actionsStack.Children.Add(toggleBtn);

        var delBtn = new Button
        {
            Content = "Delete",
            FontSize = 11,
            Padding = new Thickness(8, 4),
            Foreground = ColorBrush("#DC2626")
        };
        delBtn.Classes.Add("ghost");
        delBtn.Click += async (_, _) =>
        {
            await _habitService.DeleteHabitAsync(userId, habit.Id);
            await LoadHabitsAsync();
        };
        actionsStack.Children.Add(delBtn);
        Grid.SetColumn(actionsStack, 2);

        topGrid.Children.Add(titleStack);
        topGrid.Children.Add(streakBorder);
        topGrid.Children.Add(actionsStack);
        Grid.SetRow(topGrid, 0);

        // Bottom Row: Last 7 Days Completion Checkboxes
        var daysStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Margin = new Thickness(0, 14, 0, 0)
        };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var habitsWithCompletions = await _habitService.GetUserHabitsAsync(userId, today.AddDays(-6), today);
        var currentHabit = habitsWithCompletions.FirstOrDefault(h => h.Id == habit.Id);
        var completedDates = currentHabit?.Completions.Select(c => c.CompletedOn).ToHashSet() ?? new HashSet<DateOnly>();

        for (int i = 6; i >= 0; i--)
        {
            var dayDate = today.AddDays(-i);
            var isCompleted = completedDates.Contains(dayDate);
            var isToday = i == 0;

            var dayBorder = new Border
            {
                Background = ColorBrush(isToday ? "#F1F5F9" : "#FAFAFA"),
                BorderBrush = ColorBrush(isToday ? "#2563EB" : "#E2E8F0"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 6)
            };

            var dayItem = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
            var dayLabel = new TextBlock
            {
                Text = dayDate.DayOfWeek.ToString()[..3],
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                Foreground = ColorBrush("#7B879B"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var dateNumber = new TextBlock
            {
                Text = dayDate.Day.ToString(),
                FontSize = 12,
                FontWeight = FontWeight.Bold,
                Foreground = ColorBrush("#17243B"),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var checkBtn = new Button
            {
                Content = isCompleted ? "✓" : "○",
                FontWeight = FontWeight.Bold,
                FontSize = 12,
                Padding = new Thickness(8, 2),
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = ColorBrush(isCompleted ? "#16A34A" : "#7B879B")
            };
            checkBtn.Classes.Add("ghost");
            checkBtn.Click += async (_, _) =>
            {
                if (isCompleted)
                {
                    await _habitService.RemoveCompletionAsync(userId, habit.Id, dayDate);
                }
                else
                {
                    await _habitService.RecordCompletionAsync(userId, habit.Id, dayDate);
                }
                await LoadHabitsAsync();
            };

            dayItem.Children.Add(dayLabel);
            dayItem.Children.Add(dateNumber);
            dayItem.Children.Add(checkBtn);
            dayBorder.Child = dayItem;

            daysStack.Children.Add(dayBorder);
        }

        Grid.SetRow(daysStack, 1);

        grid.Children.Add(topGrid);
        grid.Children.Add(daysStack);

        card.Child = grid;
        return card;
    }

    private async void OnNewHabitClick(object? sender, RoutedEventArgs e)
    {
        var user = App.Auth.CurrentUser;
        if (user == null) return;

        var owner = VisualRoot as Window;
        var dialog = new HabitDialog();
        if (owner != null)
        {
            await dialog.ShowDialog(owner);
        }
        else
        {
            dialog.Show();
        }

        if (dialog.IsConfirmed)
        {
            await _habitService.CreateHabitAsync(
                user.Id,
                dialog.HabitName,
                dialog.HabitDescription,
                HabitFrequency.Daily,
                dialog.StartDate);

            await LoadHabitsAsync();
        }
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        await LoadHabitsAsync();
    }

    private static SolidColorBrush ColorBrush(string hex) => new(Color.Parse(hex));
}
