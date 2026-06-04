using Swyft.ServoProgrammer.Infrastructure;
using Swyft.ServoProgrammer.Models;

namespace Swyft.ServoProgrammer.ViewModels;

/// <summary>
/// View-model for a single numeric servo parameter: drives a labelled slider + numeric entry,
/// and reports range validation back to the parent view-model.
/// </summary>
public sealed class ParameterViewModel : ObservableObject
{
    private int _value;

    public ParameterViewModel(ParameterKey key, string name, string unit, string description, int minimum, int maximum, int defaultValue)
    {
        Key = key;
        Name = name;
        Unit = unit;
        Description = description;
        Minimum = minimum;
        Maximum = maximum;
        _value = defaultValue;
    }

    public ParameterKey Key { get; }
    public string Name { get; }
    public string Unit { get; }
    public string Description { get; }
    public int Minimum { get; }
    public int Maximum { get; }

    public string Range => $"{Minimum}\u2013{Maximum}{(string.IsNullOrEmpty(Unit) ? "" : " " + Unit)}";

    public int Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                OnPropertyChanged(nameof(IsValid));
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool IsValid => _value >= Minimum && _value <= Maximum;

    public bool HasError => !IsValid;
}
