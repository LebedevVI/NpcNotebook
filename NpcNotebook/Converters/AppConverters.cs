using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using NpcNotebook.Models;

namespace NpcNotebook.Converters;

public sealed class PortraitConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not NpcCharacter character)
            return null;

        return Services.NotebookSession.Current.GetPortrait(character);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class NpcSelectionBorderConverter : IMultiValueConverter
{
    private static readonly SolidColorBrush SelectedBrush = new(Color.FromArgb(0xAA, 0xC9, 0xA8, 0x6C));
    private static readonly SolidColorBrush NormalBrush = new(Color.FromArgb(0x44, 0xE8, 0xD4, 0xA8));

    static NpcSelectionBorderConverter()
    {
        SelectedBrush.Freeze();
        NormalBrush.Freeze();
    }

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not NpcCharacter item)
            return NormalBrush;

        if (values[1] is NpcCharacter selected && item.Id == selected.Id)
            return SelectedBrush;

        return NormalBrush;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class InverseNullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class NpcNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Guid id)
            return Services.NotebookSession.Current.GetCharacterName(id);

        return "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
