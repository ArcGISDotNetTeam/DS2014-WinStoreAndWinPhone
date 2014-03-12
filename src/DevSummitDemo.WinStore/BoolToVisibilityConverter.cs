using System;
using System.Windows;
using System.Globalization;
#if WINDOWS_PHONE
using System.Windows.Data;
#elif NETFX_CORE
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml;
#endif

namespace DevSummitDemo
{
    /// <summary>
    /// Converts a bound boolean value to an equivalent Visiblity value - visible if the boolean is true, collapsed if false
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter 
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        { 
            return (bool)value ? Visibility.Visible : Visibility.Collapsed; 
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) 
        {
            Visibility visibility = (Visibility)value; 
            return (visibility == Visibility.Visible); 
        }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return Convert(value, targetType, parameter, string.IsNullOrEmpty(language) ? null : new CultureInfo(language));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return ConvertBack(value, targetType, parameter, string.IsNullOrEmpty(language) ? null : new CultureInfo(language));
        }
    }
}