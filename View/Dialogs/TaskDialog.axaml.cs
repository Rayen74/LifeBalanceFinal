using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Model;

namespace View.Dialogs;

public partial class TaskDialog : Window
{
    public string TaskTitle { get; private set; } = string.Empty;
    public string? TaskDescription { get; private set; }
    public TaskPriority Priority { get; private set; } = TaskPriority.Medium;
    public DateOnly? DueDate { get; private set; }
    public TimeSpan? EstimatedDuration { get; private set; }
    public bool IsConfirmed { get; private set; }

    public TaskDialog() : this(null)
    {
    }

    public TaskDialog(TaskItem? existing = null)
    {
        InitializeComponent();

        if (existing != null)
        {
            DialogTitle.Text = "Edit Task";
            TitleTextBox.Text = existing.Title;
            DescTextBox.Text = existing.Description ?? string.Empty;
            PriorityComboBox.SelectedIndex = (int)existing.Priority;
            DueDateTextBox.Text = existing.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty;
            DurationTextBox.Text = existing.EstimatedDuration?.TotalMinutes.ToString("0") ?? string.Empty;
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var title = TitleTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            ShowError("Task title is required.");
            return;
        }

        DateOnly? parsedDueDate = null;
        var dueText = DueDateTextBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(dueText))
        {
            if (DateOnly.TryParseExact(dueText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                parsedDueDate = dt;
            }
            else if (DateOnly.TryParse(dueText, out var dtFallback))
            {
                parsedDueDate = dtFallback;
            }
            else
            {
                ShowError("Invalid due date format. Please use YYYY-MM-DD.");
                return;
            }
        }

        TimeSpan? duration = null;
        var durText = DurationTextBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(durText))
        {
            if (int.TryParse(durText, out var minutes) && minutes >= 0)
            {
                duration = TimeSpan.FromMinutes(minutes);
            }
            else
            {
                ShowError("Estimated duration must be a non-negative number of minutes.");
                return;
            }
        }

        TaskTitle = title;
        TaskDescription = string.IsNullOrWhiteSpace(DescTextBox.Text) ? null : DescTextBox.Text.Trim();
        Priority = (TaskPriority)(PriorityComboBox.SelectedIndex < 0 ? 1 : PriorityComboBox.SelectedIndex);
        DueDate = parsedDueDate;
        EstimatedDuration = duration;
        IsConfirmed = true;
        Close();
    }

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.IsVisible = true;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        IsConfirmed = false;
        Close();
    }
}
