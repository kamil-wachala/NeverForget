using System.ComponentModel;
using System.Runtime.CompilerServices;
using NeverForget.Contracts;

namespace NeverForget.Client;

public sealed class CalendarListItem(CalendarDto calendar) : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public CalendarDto Calendar { get; } = calendar;
    public string Id => Calendar.Id;
    public string Name => Calendar.Name;
    public bool CanWrite => Calendar.CanWrite;
    public string DisplayName => Calendar.CanWrite ? Calendar.Name : $"{Calendar.Name} (read-only)";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
