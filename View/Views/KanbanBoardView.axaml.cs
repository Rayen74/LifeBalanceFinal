using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Controller;
using Model;
using View.Dialogs;

namespace View.Views;

public partial class KanbanBoardView : UserControl
{
    private readonly ILocalDateProvider _dateProvider = SystemLocalDateProvider.Instance;
    private readonly BoardService _boardService = new();
    private List<TaskBoard> _boards = new();
    private TaskBoard? _currentBoard;

    public KanbanBoardView()
    {
        InitializeComponent();
        AttachedToVisualTree += async (_, _) => await LoadBoardsAsync();
    }

    public async Task LoadBoardsAsync()
    {
        var user = App.Auth.CurrentUser;
        if (user == null) return;

        _boards = await _boardService.GetUserBoardsAsync(user.Id);

        if (_boards.Count == 0)
        {
            var res = await _boardService.CreateBoardAsync(user.Id, "My Tasks", "Default personal task board");
            if (res.Success && res.Data != null)
            {
                _boards = new List<TaskBoard> { res.Data };
            }
        }

        BoardsComboBox.ItemsSource = _boards.Select(b => b.Name).ToList();

        if (_boards.Count > 0)
        {
            BoardsComboBox.SelectedIndex = 0;
        }
        else
        {
            ColumnsContainer.Children.Clear();
            _currentBoard = null;
        }
    }

    private async void OnBoardSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var idx = BoardsComboBox.SelectedIndex;
        if (idx >= 0 && idx < _boards.Count)
        {
            _currentBoard = _boards[idx];
            BoardDescText.Text = _currentBoard.Description ?? string.Empty;
            await RefreshCurrentBoardAsync();
        }
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        await RefreshCurrentBoardAsync();
    }

    private async Task RefreshCurrentBoardAsync()
    {
        if (_currentBoard == null) return;
        var user = App.Auth.CurrentUser;
        if (user == null) return;

        // Fetch refreshed board with columns and tasks
        var loaded = await _boardService.GetBoardDetailsAsync(user.Id, _currentBoard.Id);
        if (loaded == null) return;

        _currentBoard = loaded;

        // Update statistics
        var stats = await _boardService.GetBoardStatisticsAsync(user.Id, _currentBoard.Id);
        StatTotal.Text = stats.TotalTasks.ToString();
        StatInProgress.Text = stats.InProgressTasks.ToString();
        StatCompleted.Text = stats.CompletedTasks.ToString();
        StatPercent.Text = $"({stats.CompletionPercentage}%)";
        StatDueSoon.Text = stats.DueInNext7Days.ToString();
        StatOverdue.Text = stats.OverdueTasks.ToString();

        // Render Columns
        RenderColumns();
    }

    private void RenderColumns()
    {
        ColumnsContainer.Children.Clear();
        if (_currentBoard == null) return;

        var sortedColumns = _currentBoard.Columns.OrderBy(c => c.Position).ToList();

        for (int colIndex = 0; colIndex < sortedColumns.Count; colIndex++)
        {
            var col = sortedColumns[colIndex];
            var colPanel = CreateColumnPanel(col, colIndex, sortedColumns.Count);
            ColumnsContainer.Children.Add(colPanel);
        }
    }

#pragma warning disable CS0618 // Type or member is obsolete in Avalonia 11.3 for future v12 transition
    private Border CreateColumnPanel(TaskColumn col, int colIndex, int totalCols)
    {
        var columnBorder = new Border
        {
            Width = 300,
            Background = ColorBrush("#F8FAFC"),
            BorderBrush = ColorBrush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14)
        };

        // Allow drop on column panel
        DragDrop.SetAllowDrop(columnBorder, true);

        columnBorder.AddHandler(DragDrop.DragEnterEvent, (s, e) =>
        {
            if (e.Data.Contains("LifeBalance.TaskId"))
            {
                columnBorder.BorderBrush = ColorBrush("#2563EB");
                columnBorder.BorderThickness = new Thickness(2);
                e.DragEffects = DragDropEffects.Move;
                e.Handled = true;
            }
        });

        columnBorder.AddHandler(DragDrop.DragLeaveEvent, (s, e) =>
        {
            columnBorder.BorderBrush = ColorBrush("#E2E8F0");
            columnBorder.BorderThickness = new Thickness(1);
            e.Handled = true;
        });

        columnBorder.AddHandler(DragDrop.DragOverEvent, (s, e) =>
        {
            if (e.Data.Contains("LifeBalance.TaskId"))
            {
                e.DragEffects = DragDropEffects.Move;
                e.Handled = true;
            }
        });

        columnBorder.AddHandler(DragDrop.DropEvent, async (s, e) =>
        {
            columnBorder.BorderBrush = ColorBrush("#E2E8F0");
            columnBorder.BorderThickness = new Thickness(1);

            if (!e.Data.Contains("LifeBalance.TaskId")) return;
            e.Handled = true;

            var taskIdStr = e.Data.Get("LifeBalance.TaskId") as string;
            var sourceColIdStr = e.Data.Get("LifeBalance.SourceColumnId") as string;
            var sourcePosStr = e.Data.Get("LifeBalance.SourcePosition") as string;

            if (Guid.TryParse(taskIdStr, out var taskId) &&
                Guid.TryParse(sourceColIdStr, out var sourceColId) &&
                int.TryParse(sourcePosStr, out var sourcePos))
            {
                int targetPos = KanbanPositionHelper.CalculateDropPosition(
                    sourceColId, col.Id, sourcePos, null, false, col.Tasks.Count);

                await HandleDropAsync(taskId, sourceColId, sourcePos, col.Id, targetPos);
            }
        });

        var mainLayout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto, *, Auto")
        };

        // Header: Title, Count, Add Task
        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*, Auto")
        };

        var titleStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        var colTitle = new TextBlock
        {
            Text = col.Name,
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            Foreground = ColorBrush("#14264D"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var badgeBorder = new Border
        {
            Background = ColorBrush("#E2E8F0"),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        var badgeText = new TextBlock
        {
            Text = col.Tasks.Count.ToString(),
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = ColorBrush("#17243B")
        };
        badgeBorder.Child = badgeText;

        titleStack.Children.Add(colTitle);
        titleStack.Children.Add(badgeBorder);
        Grid.SetColumn(titleStack, 0);

        var addTaskBtn = new Button
        {
            Content = "+ Task",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Padding = new Thickness(8, 4)
        };
        addTaskBtn.Classes.Add("primary");
        addTaskBtn.Click += async (_, _) => await AddTaskToColumnAsync(col.Id);
        Grid.SetColumn(addTaskBtn, 1);

        headerGrid.Children.Add(titleStack);
        headerGrid.Children.Add(addTaskBtn);
        Grid.SetRow(headerGrid, 0);

        // Tasks Scroll Container
        var tasksScrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 12, 0, 0)
        };

        var tasksStack = new StackPanel
        {
            Spacing = 10
        };

        var orderedTasks = col.Tasks.OrderBy(t => t.Position).ToList();
        for (int i = 0; i < orderedTasks.Count; i++)
        {
            var task = orderedTasks[i];
            var cardContainer = CreateTaskCard(task, col, colIndex, totalCols);
            tasksStack.Children.Add(cardContainer);
        }

        tasksScrollViewer.Content = tasksStack;
        Grid.SetRow(tasksScrollViewer, 1);

        mainLayout.Children.Add(headerGrid);
        mainLayout.Children.Add(tasksScrollViewer);

        columnBorder.Child = mainLayout;
        return columnBorder;
    }

    private Control CreateTaskCard(TaskItem task, TaskColumn col, int colIndex, int totalCols)
    {
        var cardContainer = new StackPanel { Spacing = 0 };

        var topInsertionLine = new Border
        {
            Height = 3,
            Background = ColorBrush("#2563EB"),
            CornerRadius = new CornerRadius(1.5),
            Margin = new Thickness(0, 0, 0, 4),
            IsVisible = false
        };

        var bottomInsertionLine = new Border
        {
            Height = 3,
            Background = ColorBrush("#2563EB"),
            CornerRadius = new CornerRadius(1.5),
            Margin = new Thickness(0, 4, 0, 0),
            IsVisible = false
        };

        var card = new Border
        {
            Background = ColorBrush("#FFFFFF"),
            BorderBrush = ColorBrush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Blur = 4,
                Color = Color.FromArgb(15, 0, 0, 0),
                OffsetY = 1
            })
        };

        // Enable drag & drop target on the card
        DragDrop.SetAllowDrop(card, true);

        card.AddHandler(DragDrop.DragOverEvent, (s, e) =>
        {
            if (e.Data.Contains("LifeBalance.TaskId"))
            {
                e.DragEffects = DragDropEffects.Move;
                e.Handled = true;
                var pt = e.GetPosition(card);
                bool dropAfter = pt.Y > (card.Bounds.Height / 2.0);
                topInsertionLine.IsVisible = !dropAfter;
                bottomInsertionLine.IsVisible = dropAfter;
            }
        });

        card.AddHandler(DragDrop.DragLeaveEvent, (s, e) =>
        {
            topInsertionLine.IsVisible = false;
            bottomInsertionLine.IsVisible = false;
            e.Handled = true;
        });

        card.AddHandler(DragDrop.DropEvent, async (s, e) =>
        {
            topInsertionLine.IsVisible = false;
            bottomInsertionLine.IsVisible = false;

            if (!e.Data.Contains("LifeBalance.TaskId")) return;
            e.Handled = true;

            var taskIdStr = e.Data.Get("LifeBalance.TaskId") as string;
            var sourceColIdStr = e.Data.Get("LifeBalance.SourceColumnId") as string;
            var sourcePosStr = e.Data.Get("LifeBalance.SourcePosition") as string;

            if (Guid.TryParse(taskIdStr, out var taskId) &&
                Guid.TryParse(sourceColIdStr, out var sourceColId) &&
                int.TryParse(sourcePosStr, out var sourcePos))
            {
                var pt = e.GetPosition(card);
                bool dropAfter = pt.Y > (card.Bounds.Height / 2.0);
                int targetPos = KanbanPositionHelper.CalculateDropPosition(
                    sourceColId, col.Id, sourcePos, task.Position, dropAfter, col.Tasks.Count);

                await HandleDropAsync(taskId, sourceColId, sourcePos, col.Id, targetPos);
            }
        });

        var cardStack = new StackPanel { Spacing = 8 };

        // Top Row: Drag Handle, Priority Pill & Due Date
        var metaGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, Auto, *")
        };

        // Dedicated Drag Handle
        var dragHandle = new Border
        {
            Width = 20,
            Height = 20,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeAll),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };
        ToolTip.SetTip(dragHandle, "Drag to reorder or move across columns");

        var gripText = new TextBlock
        {
            Text = "⋮⋮",
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Foreground = ColorBrush("#94A3B8"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        dragHandle.Child = gripText;

        Point? dragStartPoint = null;
        bool isDragging = false;

        dragHandle.PointerPressed += (s, e) =>
        {
            if (e.GetCurrentPoint(dragHandle).Properties.IsLeftButtonPressed)
            {
                dragStartPoint = e.GetPosition(null);
            }
        };

        dragHandle.PointerMoved += async (s, e) =>
        {
            if (dragStartPoint.HasValue && !isDragging)
            {
                var current = e.GetPosition(null);
                var diff = current - dragStartPoint.Value;
                if (Math.Sqrt(diff.X * diff.X + diff.Y * diff.Y) > 4)
                {
                    isDragging = true;
                    card.Opacity = 0.4;
                    var data = new DataObject();
                    data.Set("LifeBalance.TaskId", task.Id.ToString());
                    data.Set("LifeBalance.SourceColumnId", col.Id.ToString());
                    data.Set("LifeBalance.SourcePosition", task.Position.ToString());
                    try
                    {
                        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
                    }
                    finally
                    {
                        card.Opacity = 1.0;
                        isDragging = false;
                        dragStartPoint = null;
                    }
                }
            }
        };

        dragHandle.PointerReleased += (s, e) =>
        {
            dragStartPoint = null;
            isDragging = false;
        };

        Grid.SetColumn(dragHandle, 0);
        metaGrid.Children.Add(dragHandle);

        var (pillBg, pillFg) = task.Priority switch
        {
            TaskPriority.High => ("#FEE2E2", "#DC2626"),
            TaskPriority.Medium => ("#FEF3C7", "#D97706"),
            _ => ("#DCFCE7", "#16A34A")
        };

        var priorityPill = new Border
        {
            Background = ColorBrush(pillBg),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2)
        };
        var priorityText = new TextBlock
        {
            Text = task.Priority.ToString(),
            FontSize = 10,
            FontWeight = FontWeight.Bold,
            Foreground = ColorBrush(pillFg)
        };
        priorityPill.Child = priorityText;
        Grid.SetColumn(priorityPill, 1);
        metaGrid.Children.Add(priorityPill);

        if (task.DueDate.HasValue)
        {
            var today = _dateProvider.GetToday();
            var isOverdue = !col.IsCompleted && task.DueDate.Value < today;
            var dueBlock = new TextBlock
            {
                Text = task.DueDate.Value.ToString("MMM dd"),
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = ColorBrush(isOverdue ? "#DC2626" : "#7B879B"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(dueBlock, 2);
            metaGrid.Children.Add(dueBlock);
        }

        cardStack.Children.Add(metaGrid);

        // Title
        var titleText = new TextBlock
        {
            Text = task.Title,
            FontWeight = FontWeight.SemiBold,
            FontSize = 13,
            Foreground = ColorBrush("#17243B"),
            TextWrapping = TextWrapping.Wrap
        };
        if (col.IsCompleted)
        {
            titleText.TextDecorations = TextDecorations.Strikethrough;
            titleText.Foreground = ColorBrush("#7B879B");
        }
        cardStack.Children.Add(titleText);

        // Description if present
        if (!string.IsNullOrWhiteSpace(task.Description))
        {
            var descText = new TextBlock
            {
                Text = task.Description,
                FontSize = 11,
                Foreground = ColorBrush("#7B879B"),
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            cardStack.Children.Add(descText);
        }

        // Action Buttons Row: Left, Right (Accessible Movement), Edit, Delete
        var actionsGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, Auto, *, Auto")
        };

        var moveLeftBtn = new Button
        {
            Content = "◀",
            FontSize = 10,
            Padding = new Thickness(6, 2),
            IsEnabled = colIndex > 0
        };
        ToolTip.SetTip(moveLeftBtn, "Move to previous column");
        moveLeftBtn.Classes.Add("ghost");
        moveLeftBtn.Click += async (_, _) => await MoveTaskDirectionAsync(task.Id, -1);
        Grid.SetColumn(moveLeftBtn, 0);
        actionsGrid.Children.Add(moveLeftBtn);

        var moveRightBtn = new Button
        {
            Content = "▶",
            FontSize = 10,
            Padding = new Thickness(6, 2),
            Margin = new Thickness(4, 0, 0, 0),
            IsEnabled = colIndex < totalCols - 1
        };
        ToolTip.SetTip(moveRightBtn, "Move to next column");
        moveRightBtn.Classes.Add("ghost");
        moveRightBtn.Click += async (_, _) => await MoveTaskDirectionAsync(task.Id, 1);
        Grid.SetColumn(moveRightBtn, 1);
        actionsGrid.Children.Add(moveRightBtn);

        var editBtn = new Button
        {
            Content = "Edit",
            FontSize = 10,
            Padding = new Thickness(6, 2),
            Margin = new Thickness(4, 0)
        };
        editBtn.Classes.Add("ghost");
        editBtn.Click += async (_, _) => await EditTaskAsync(task);
        Grid.SetColumn(editBtn, 2);
        actionsGrid.Children.Add(editBtn);

        var delBtn = new Button
        {
            Content = "✕",
            FontSize = 10,
            Padding = new Thickness(6, 2),
            Foreground = ColorBrush("#DC2626")
        };
        delBtn.Classes.Add("ghost");
        delBtn.Click += async (_, _) => await DeleteTaskAsync(task.Id);
        Grid.SetColumn(delBtn, 3);
        actionsGrid.Children.Add(delBtn);

        cardStack.Children.Add(actionsGrid);

        card.Child = cardStack;

        cardContainer.Children.Add(topInsertionLine);
        cardContainer.Children.Add(card);
        cardContainer.Children.Add(bottomInsertionLine);

        return cardContainer;
    }

    private async Task HandleDropAsync(Guid taskId, Guid sourceColId, int sourcePos, Guid targetColId, int targetPos)
    {
        var user = App.Auth.CurrentUser;
        if (user == null || _currentBoard == null) return;

        // Dropping into exact same position in same column -> no change
        if (sourceColId == targetColId && sourcePos == targetPos)
        {
            return;
        }

        var result = await _boardService.MoveTaskAsync(user.Id, taskId, targetColId, targetPos);
        if (!result.Success)
        {
            // Reset to persisted state
            await RefreshCurrentBoardAsync();
            return;
        }

        await RefreshCurrentBoardAsync();
    }
#pragma warning restore CS0618

    private async Task AddTaskToColumnAsync(Guid columnId)
    {
        var user = App.Auth.CurrentUser;
        if (user == null) return;

        var owner = VisualRoot as Window;
        var dialog = new TaskDialog();
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
            await _boardService.CreateTaskAsync(
                user.Id,
                columnId,
                dialog.TaskTitle,
                dialog.TaskDescription,
                dialog.Priority,
                dialog.DueDate,
                dialog.EstimatedDuration);

            await RefreshCurrentBoardAsync();
        }
    }

    private async Task EditTaskAsync(TaskItem task)
    {
        var user = App.Auth.CurrentUser;
        if (user == null) return;

        var owner = VisualRoot as Window;
        var dialog = new TaskDialog(task);
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
            await _boardService.UpdateTaskAsync(
                user.Id,
                task.Id,
                dialog.TaskTitle,
                dialog.TaskDescription,
                dialog.Priority,
                dialog.DueDate,
                dialog.EstimatedDuration);

            await RefreshCurrentBoardAsync();
        }
    }

    private async Task MoveTaskDirectionAsync(Guid taskId, int direction)
    {
        if (_currentBoard == null) return;
        var user = App.Auth.CurrentUser;
        if (user == null) return;

        var sortedCols = _currentBoard.Columns.OrderBy(c => c.Position).ToList();
        var currentColIndex = sortedCols.FindIndex(c => c.Tasks.Any(t => t.Id == taskId));
        if (currentColIndex < 0) return;

        var targetColIndex = currentColIndex + direction;
        if (targetColIndex < 0 || targetColIndex >= sortedCols.Count) return;

        var targetCol = sortedCols[targetColIndex];
        var newPos = targetCol.Tasks.Count; // Append to bottom of target column

        await _boardService.MoveTaskAsync(user.Id, taskId, targetCol.Id, newPos);
        await RefreshCurrentBoardAsync();
    }

    private async Task DeleteTaskAsync(Guid taskId)
    {
        var user = App.Auth.CurrentUser;
        if (user == null) return;

        await _boardService.DeleteTaskAsync(user.Id, taskId);
        await RefreshCurrentBoardAsync();
    }

    private async void OnNewBoardClick(object? sender, RoutedEventArgs e)
    {
        var user = App.Auth.CurrentUser;
        if (user == null) return;

        var owner = VisualRoot as Window;
        var dialog = new BoardDialog();
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
            var res = await _boardService.CreateBoardAsync(user.Id, dialog.BoardTitle, dialog.BoardDescription);
            if (res.Success)
            {
                await LoadBoardsAsync();
                var idx = _boards.FindIndex(b => b.Name == dialog.BoardTitle);
                if (idx >= 0) BoardsComboBox.SelectedIndex = idx;
            }
        }
    }

    private async void OnDeleteBoardClick(object? sender, RoutedEventArgs e)
    {
        if (_currentBoard == null) return;
        var user = App.Auth.CurrentUser;
        if (user == null) return;

        if (_boards.Count <= 1)
        {
            return;
        }

        await _boardService.DeleteBoardAsync(user.Id, _currentBoard.Id);
        await LoadBoardsAsync();
    }

    private static SolidColorBrush ColorBrush(string hex) => new(Color.Parse(hex));
}
