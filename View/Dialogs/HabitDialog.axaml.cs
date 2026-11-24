using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Controller;
using Model;

namespace View.Dialogs;

public partial class HabitDialog : Window
{
    public string HabitName { get; private set; } = string.Empty;
    public string? HabitDescription { get; private set; }
    public DateOnly StartDate { get; private set; } = SystemLocalDateProvider.Instance.GetToday();
    public bool IsConfirmed { get; private set; }

    public HabitDialog() : this(null)
    {
    }

    public HabitDialog(Habit? existing = null)
    {
        InitializeComponent();

        if (existing != null)
        {
            DialogTitle.Text = "Edit Habit";
            NameTextBox.Text = existing.Name;
            DescTextBox.Text = existing.Description ?? string.Empty;
            StartDateTextBox.Text = existing.StartDate.ToString("yyyy-MM-dd");
        }
        else
        {
            StartDateTextBox.Text = StartDate.ToString("yyyy-MM-dd");
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorText.Text = "Habit name is required.";
            ErrorText.IsVisible = true;
            return;
        }

        var startText = StartDateTextBox.Text?.Trim();
        DateOnly startDate = SystemLocalDateProvider.Instance.GetToday();
        if (!string.IsNullOrWhiteSpace(startText))
        {
            if (DateOnly.TryParseExact(startText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                startDate = parsed;
            }
            else if (DateOnly.TryParse(startText, out var fallback))
            {
                startDate = fallback;
            }
            else
            {
                ErrorText.Text = "Invalid start date. Please use YYYY-MM-DD.";
                ErrorText.IsVisible = true;
                return;
            }
        }

        HabitName = name;
        HabitDescription = string.IsNullOrWhiteSpace(DescTextBox.Text) ? null : DescTextBox.Text.Trim();
        StartDate = startDate;
        IsConfirmed = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        IsConfirmed = false;
        Close();
    }
}
