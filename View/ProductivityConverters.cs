using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Model;

namespace View;

public class PriorityToColorConverter : IValueConverter
{
    public static readonly PriorityToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TaskPriority priority)
        {
            return priority switch
            {
                TaskPriority.High => Color.Parse("#DC2626"),   // Red
                TaskPriority.Medium => Color.Parse("#D97706"), // Amber/Orange
                TaskPriority.Low => Color.Parse("#16A34A"),    // Green
                _ => Color.Parse("#7B879B")
            };
        }
        return Color.Parse("#7B879B");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class PriorityToBgConverter : IValueConverter
{
    public static readonly PriorityToBgConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TaskPriority priority)
        {
            return priority switch
            {
                TaskPriority.High => Color.Parse("#FEE2E2"),   // Light red
                TaskPriority.Medium => Color.Parse("#FEF3C7"), // Light amber
                TaskPriority.Low => Color.Parse("#DCFCE7"),    // Light green
                _ => Color.Parse("#F1F5F9")
            };
        }
        return Color.Parse("#F1F5F9");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
