using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RealTimeVideoAnalysis.Converters
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = false;
            if (value is bool)
            {
                boolValue = (bool)value;
            }

            // "Invert" 파라미터 처리 (선택 사항, 필요에 따라 사용)
            if (parameter is string stringParameter && stringParameter.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            {
                boolValue = !boolValue;
            }
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 일반적으로 OneWay 바인딩에 사용되므로 ConvertBack은 구현하지 않아도 무방함
            throw new NotImplementedException();
        }
    }
} 