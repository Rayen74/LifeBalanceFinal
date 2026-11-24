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

namespace View.Dialogs;

public partial class ManageGoalTasksDialog : Window
{
    private readonly GoalService _goalService = new();
    private readonly PersonalGoal? _goal;
    private readonly Guid _userId;
    private readonly HashSet<Guid> _linkedTaskIds = new();

    public bool HasChanges { get; private set; }

    public ManageGoalTasksDialog() : this(null, Guid.Empty)
    {
    }

    public ManageGoalTasksDialog(PersonalGoal? goal, Guid userId)
    {
        InitializeComponent();
        _goal = goal;
        _userId = userId;

        if (_goal != null)
        {
            GoalTitleHeader.Text = $"Linked Tasks: {_goal.Name}";
            foreach (var tg in _goal.TaskGoals)
            {
                _linkedTaskIds.Add(tg.TaskId);
            }
        }

        AttachedToVisualTree += async (_, _) => await LoadTasksAsync();
    }

    private async Task LoadTasksAsync()
    {
        if (_goal == null || _userId == Guid.Empty) return;

        var tasks = await _goalService.GetUserTasksAsync(_userId);
        TasksContainer.Children.Clear();

        if (tasks.Count == 0)
        {
            EmptyNotice.IsVisible = true;
            return;
        }

        EmptyNotice.IsVisible = false;

        foreach (var task in tasks)
        {
            var row = CreateTaskRow(task);
            TasksContainer.Children.Add(row);
        }
    }

    private Border CreateTaskRow(TaskItem task)
    {
        bool isLinked = _linkedTaskIds.Contains(task.Id);
        bool isCompleted = task.Column?.IsCompleted == true;

        var rowBorder = new Border
        {
            Background = ColorBrush("#F8FAFC"),
            BorderBrush = ColorBrush(isLinked ? "#BFDBFE" : "#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10)
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*, Auto")
        };

        // Left info stack
        var infoStack = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        var titleBlock = new TextBlock
        {
            Text = task.Title,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = ColorBrush(isCompleted ? "#7B879B" : "#17243B"),
            VerticalAlignment = VerticalAlignment.Center
        };
        if (isCompleted)
        {
            titleBlock.TextDecorations = TextDecorations.Strikethrough;
        }
        titleRow.Children.Add(titleBlock);

        // Status badge
        var statusBadge = new Border
        {
            Background = ColorBrush(isCompleted ? "#DCFCE7" : "#F1F5F9"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        var statusText = new TextBlock
        {
            Text = isCompleted ? "Completed" : (task.Column?.Name ?? "Active"),
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            Foreground = ColorBrush(isCompleted ? "#16A34A" : "#64748B")
        };
        statusBadge.Child = statusText;
        titleRow.Children.Add(statusBadge);

        infoStack.Children.Add(titleRow);

        // Subtitle: Board name & Column
        var boardName = task.Column?.Board?.Name ?? "Board";
        var colName = task.Column?.Name ?? "Column";
        var subText = new TextBlock
        {
            Text = $"Board: {boardName}  ›  Column: {colName}",
            FontSize = 11,
            Foreground = ColorBrush("#7B879B")
        };
        infoStack.Children.Add(subText);
        Grid.SetColumn(infoStack, 0);

        // Right button
        var actionBtn = new Button
        {
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(12, 5),
            VerticalAlignment = VerticalAlignment.Center
        };

        void UpdateButtonState()
        {
            bool currentlyLinked = _linkedTaskIds.Contains(task.Id);
            if (currentlyLinked)
            {
                actionBtn.Content = "Unlink";
                actionBtn.Classes.Clear();
                actionBtn.Classes.Add("ghost");
                actionBtn.Foreground = ColorBrush("#DC2626");
                rowBorder.BorderBrush = ColorBrush("#BFDBFE");
                rowBorder.Background = ColorBrush("#F0F9FF");
            }
            else
            {
                actionBtn.Content = "+ Link";
                actionBtn.Classes.Clear();
                actionBtn.Classes.Add("primary");
                actionBtn.Foreground = ColorBrush("#FFFFFF");
                rowBorder.BorderBrush = ColorBrush("#E2E8F0");
                rowBorder.Background = ColorBrush("#F8FAFC");
            }
        }

        UpdateButtonState();

        actionBtn.Click += async (_, _) =>
        {
            if (_goal == null) return;
            actionBtn.IsEnabled = false;

            if (_linkedTaskIds.Contains(task.Id))
            {
                var result = await _goalService.UnlinkTaskFromGoalAsync(_userId, task.Id, _goal.Id);
                if (result.Success)
                {
                    _linkedTaskIds.Remove(task.Id);
                    HasChanges = true;
                    UpdateButtonState();
                }
            }
            else
            {
                var result = await _goalService.LinkTaskToGoalAsync(_userId, task.Id, _goal.Id);
                if (result.Success)
                {
                    _linkedTaskIds.Add(task.Id);
                    HasChanges = true;
                    UpdateButtonState();
                }
            }

            actionBtn.IsEnabled = true;
        };

        Grid.SetColumn(actionBtn, 1);

        grid.Children.Add(infoStack);
        grid.Children.Add(actionBtn);

        rowBorder.Child = grid;
        return rowBorder;
    }

    private void OnDoneClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private static SolidColorBrush ColorBrush(string hex) => new(Color.Parse(hex));
}
