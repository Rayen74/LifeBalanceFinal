using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Model;

namespace View.Dialogs;

public partial class GoalDialog : Window
{
    public string GoalName { get; private set; } = string.Empty;
    public string? GoalDescription { get; private set; }
    public GoalStatus Status { get; private set; } = GoalStatus.Active;
    public DateOnly? TargetDate { get; private set; }
    public bool IsConfirmed { get; private set; }

    public GoalDialog() : this(null)
    {
    }

    public GoalDialog(PersonalGoal? existing = null)
    {
        InitializeComponent();

        if (existing != null)
        {
            DialogTitle.Text = "Edit Goal";
            TitleTextBox.Text = existing.Name;
            DescTextBox.Text = existing.Description ?? string.Empty;
            StatusComboBox.SelectedIndex = (int)existing.Status;
            TargetDateTextBox.Text = existing.TargetDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var title = TitleTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            ErrorText.Text = "Goal title is required.";
            ErrorText.IsVisible = true;
            return;
        }

        DateOnly? parsedDate = null;
        var dateText = TargetDateTextBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(dateText))
        {
            if (DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                parsedDate = dt;
            }
            else if (DateOnly.TryParse(dateText, out var dtFallback))
            {
                parsedDate = dtFallback;
            }
            else
            {
                ErrorText.Text = "Invalid target date format. Please use YYYY-MM-DD.";
                ErrorText.IsVisible = true;
                return;
            }
        }

        GoalName = title;
        GoalDescription = string.IsNullOrWhiteSpace(DescTextBox.Text) ? null : DescTextBox.Text.Trim();
        Status = (GoalStatus)(StatusComboBox.SelectedIndex < 0 ? 0 : StatusComboBox.SelectedIndex);
        TargetDate = parsedDate;
        IsConfirmed = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        IsConfirmed = false;
        Close();
    }
}
