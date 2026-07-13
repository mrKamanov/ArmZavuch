using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ArmZavuch.Models;

/// <summary>Галочка выбора строки в списках (только UI, не сохраняется в БД).</summary>
public abstract class SelectableEntity : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isDuplicateHighlight;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Подсветка строки, совпадающей с вводом в форме (дубль).</summary>
    public bool IsDuplicateHighlight
    {
        get => _isDuplicateHighlight;
        set
        {
            if (_isDuplicateHighlight == value)
                return;
            _isDuplicateHighlight = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
