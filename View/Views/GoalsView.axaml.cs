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

public partial class GoalsView : UserControl
{
    private readonly GoalService _goalService = new();

    public GoalsView()
    {
        InitializeComponent();
        AttachedToVisualTree += async (_, _) => await LoadGoalsAsync();
    }

    public async Task LoadGoalsAsync()
    {
        var user = App.Auth.CurrentUser;
        if (user == null) return;

        var goals = await _goalService.GetUserGoalsAsync(user.Id);
        GoalsContainer.Children.Clear();

        if (goals.Count == 0)
        {
            EmptyNotice.IsVisible = true;
            return;
        }

        EmptyNotice.IsVisible = false;

        foreach (var goal in goals)
        {
            var card = CreateGoalCard(goal, user.Id);
            GoalsContainer.Children.Add(card);
        }
    }

    private Border CreateGoalCard(PersonalGoal goal, Guid userId)
    {
        var card = new Border
        {
            Background = ColorBrush("#FFFFFF"),
            BorderBrush = ColorBrush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(18)
        };

        var mainStack = new StackPanel { Spacing = 12 };

        // Top Row: Status Pill, Title, Target Date, Actions
        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, *, Auto, Auto")
        };

        var (statusBg, statusFg) = goal.Status switch
        {
            GoalStatus.Completed => ("#DCFCE7", "#16A34A"),
            GoalStatus.Cancelled => ("#F1F5F9", "#7B879B"),
            _ => ("#DBEAFE", "#2563EB")
        };

        var statusBorder = new Border
        {
            Background = ColorBrush(statusBg),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        var statusText = new TextBlock
        {
            Text = goal.Status.ToString(),
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = ColorBrush(statusFg)
        };
        statusBorder.Child = statusText;
        Grid.SetColumn(statusBorder, 0);

        var titleStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var titleText = new TextBlock
        {
            Text = goal.Name,
            FontSize = 15,
            FontWeight = FontWeight.Bold,
            Foreground = ColorBrush("#14264D")
        };
        titleStack.Children.Add(titleText);
        if (!string.IsNullOrWhiteSpace(goal.Description))
        {
            var descText = new TextBlock
            {
                Text = goal.Description,
                FontSize = 12,
                Foreground = ColorBrush("#7B879B")
            };
            titleStack.Children.Add(descText);
        }
        Grid.SetColumn(titleStack, 1);

        if (goal.TargetDate.HasValue)
        {
            var dateText = new TextBlock
            {
                Text = $"Target: {goal.TargetDate.Value:yyyy-MM-dd}",
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = ColorBrush("#7B879B"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0)
            };
            Grid.SetColumn(dateText, 2);
            headerGrid.Children.Add(dateText);
        }

        var actionsStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };

        var manageTasksBtn = new Button
        {
            Content = "Manage Tasks",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(8, 4)
        };
        manageTasksBtn.Classes.Add("primary");
        manageTasksBtn.Click += async (_, _) => await OpenManageTasksAsync(goal, userId);
        actionsStack.Children.Add(manageTasksBtn);

        var editBtn = new Button
        {
            Content = "Edit",
            FontSize = 11,
            Padding = new Thickness(8, 4)
        };
        editBtn.Classes.Add("ghost");
        editBtn.Click += async (_, _) => await EditGoalAsync(goal, userId);
        actionsStack.Children.Add(editBtn);

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
            await _goalService.DeleteGoalAsync(userId, goal.Id);
            await LoadGoalsAsync();
        };
        actionsStack.Children.Add(delBtn);
        Grid.SetColumn(actionsStack, 3);

        headerGrid.Children.Add(statusBorder);
        headerGrid.Children.Add(titleStack);
        headerGrid.Children.Add(actionsStack);
        mainStack.Children.Add(headerGrid);

        // Derived Progress Section
        var progress = GoalService.CalculateGoalProgress(goal);
        if (!progress.HasTasks)
        {
            var noProgressText = new TextBlock
            {
                Text = "No tasks linked yet",
                FontSize = 12,
                FontStyle = FontStyle.Italic,
                Foreground = ColorBrush("#7B879B"),
                Margin = new Thickness(0, 2, 0, 2)
            };
            mainStack.Children.Add(noProgressText);
        }
        else
        {
            var progressContainer = new StackPanel { Spacing = 5, Margin = new Thickness(0, 2, 0, 2) };

            var progressLabel = new TextBlock
            {
                Text = $"{progress.CompletedTasks} of {progress.TotalTasks} tasks completed — {progress.Percentage}%",
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = ColorBrush("#14264D")
            };
            progressContainer.Children.Add(progressLabel);

            var progressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = progress.Percentage,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = ColorBrush("#E2E8F0"),
                Foreground = ColorBrush(progress.Percentage >= 100 ? "#16A34A" : "#2563EB")
            };
            progressContainer.Children.Add(progressBar);

            mainStack.Children.Add(progressContainer);
        }

        // Linked Tasks Section
        var tasksCount = goal.TaskGoals.Count;
        var tasksHeader = new TextBlock
        {
            Text = $"Linked Tasks ({tasksCount})",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = ColorBrush("#17243B"),
            Margin = new Thickness(0, 4, 0, 0)
        };
        mainStack.Children.Add(tasksHeader);

        if (tasksCount > 0)
        {
            var tasksWrap = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            foreach (var tg in goal.TaskGoals)
            {
                bool isTaskCompleted = tg.Task?.Column?.IsCompleted == true;
                var taskTag = new Border
                {
                    Background = ColorBrush(isTaskCompleted ? "#DCFCE7" : "#F1F5F9"),
                    BorderBrush = ColorBrush(isTaskCompleted ? "#86EFAC" : "#E2E8F0"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8, 4),
                    Margin = new Thickness(0, 0, 8, 6)
                };

                var tagStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                if (isTaskCompleted)
                {
                    tagStack.Children.Add(new TextBlock
                    {
                        Text = "✓",
                        FontSize = 10,
                        FontWeight = FontWeight.Bold,
                        Foreground = ColorBrush("#16A34A")
                    });
                }

                var taskText = new TextBlock
                {
                    Text = tg.Task?.Title ?? "Task",
                    FontSize = 11,
                    Foreground = ColorBrush(isTaskCompleted ? "#166534" : "#17243B")
                };
                if (isTaskCompleted)
                {
                    taskText.TextDecorations = TextDecorations.Strikethrough;
                }

                tagStack.Children.Add(taskText);
                taskTag.Child = tagStack;
                tasksWrap.Children.Add(taskTag);
            }
            mainStack.Children.Add(tasksWrap);
        }
        else
        {
            var noTasksText = new TextBlock
            {
                Text = "No tasks currently linked to this goal.",
                FontSize = 11,
                Foreground = ColorBrush("#7B879B"),
                FontStyle = FontStyle.Italic
            };
            mainStack.Children.Add(noTasksText);
        }

        card.Child = mainStack;
        return card;
    }

    private async Task OpenManageTasksAsync(PersonalGoal goal, Guid userId)
    {
        var owner = VisualRoot as Window;
        var dialog = new ManageGoalTasksDialog(goal, userId);
        if (owner != null)
        {
            await dialog.ShowDialog(owner);
        }
        else
        {
            dialog.Show();
        }

        if (dialog.HasChanges)
        {
            await LoadGoalsAsync();
        }
    }

    private async Task EditGoalAsync(PersonalGoal goal, Guid userId)
    {
        var owner = VisualRoot as Window;
        var dialog = new GoalDialog(goal);
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
            await _goalService.UpdateGoalAsync(
                userId,
                goal.Id,
                dialog.GoalName,
                dialog.GoalDescription,
                dialog.TargetDate,
                dialog.Status);

            await LoadGoalsAsync();
        }
    }

    private async void OnNewGoalClick(object? sender, RoutedEventArgs e)
    {
        var user = App.Auth.CurrentUser;
        if (user == null) return;

        var owner = VisualRoot as Window;
        var dialog = new GoalDialog();
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
            await _goalService.CreateGoalAsync(
                user.Id,
                dialog.GoalName,
                dialog.GoalDescription,
                dialog.TargetDate);

            await LoadGoalsAsync();
        }
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        await LoadGoalsAsync();
    }

    private static SolidColorBrush ColorBrush(string hex) => new(Color.Parse(hex));
}
