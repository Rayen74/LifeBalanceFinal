using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Controller;

/// <summary>One area of life (Health, Work, ...) scored from 0 to 10.</summary>
public class LifeArea : INotifyPropertyChanged
{
    private int _score;

    public LifeArea(string name, string icon, int score)
    {
        Name = name;
        Icon = icon;
        _score = Math.Clamp(score, 0, 10);
    }

    public string Name { get; }
    public string Icon { get; }

    public int Score
    {
        get => _score;
        set
        {
            var clamped = Math.Clamp(value, 0, 10);
            if (clamped == _score) return;
            _score = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ScoreText));
        }
    }

    public string ScoreText => $"{Score}/10";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
