using Avalonia.Controls;
using Avalonia.Interactivity;

namespace View.Dialogs;

public partial class BoardDialog : Window
{
    public string BoardTitle { get; private set; } = string.Empty;
    public string? BoardDescription { get; private set; }
    public bool IsConfirmed { get; private set; }

    public BoardDialog() : this(null, null)
    {
    }

    public BoardDialog(string? existingTitle = null, string? existingDescription = null)
    {
        InitializeComponent();

        if (!string.IsNullOrWhiteSpace(existingTitle))
        {
            DialogTitle.Text = "Edit Board";
            TitleTextBox.Text = existingTitle;
            DescTextBox.Text = existingDescription ?? string.Empty;
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var title = TitleTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            ErrorText.Text = "Board title is required.";
            ErrorText.IsVisible = true;
            return;
        }

        BoardTitle = title;
        BoardDescription = string.IsNullOrWhiteSpace(DescTextBox.Text) ? null : DescTextBox.Text.Trim();
        IsConfirmed = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        IsConfirmed = false;
        Close();
    }
}
