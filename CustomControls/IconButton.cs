using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RealTimeVideoAnalysis.CustomControls
{
    public class IconButton : Button
    {
        static IconButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(IconButton), 
                new FrameworkPropertyMetadata(typeof(IconButton)));
        }

        #region Icon DependencyProperty
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(
                "Icon",
                typeof(string),
                typeof(IconButton),
                new PropertyMetadata(string.Empty));

        public string Icon
        {
            get { return (string)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }
        #endregion

        #region IconSize DependencyProperty
        public static readonly DependencyProperty IconSizeProperty =
            DependencyProperty.Register(
                "IconSize",
                typeof(double),
                typeof(IconButton),
                new PropertyMetadata(16.0));

        public double IconSize
        {
            get { return (double)GetValue(IconSizeProperty); }
            set { SetValue(IconSizeProperty, value); }
        }
        #endregion

        #region IconMargin DependencyProperty
        public static readonly DependencyProperty IconMarginProperty =
            DependencyProperty.Register(
                "IconMargin",
                typeof(Thickness),
                typeof(IconButton),
                new PropertyMetadata(new Thickness(0, 0, 8, 0)));

        public Thickness IconMargin
        {
            get { return (Thickness)GetValue(IconMarginProperty); }
            set { SetValue(IconMarginProperty, value); }
        }
        #endregion

        #region ShowIcon DependencyProperty
        public static readonly DependencyProperty ShowIconProperty =
            DependencyProperty.Register(
                "ShowIcon",
                typeof(bool),
                typeof(IconButton),
                new PropertyMetadata(true));

        public bool ShowIcon
        {
            get { return (bool)GetValue(ShowIconProperty); }
            set { SetValue(ShowIconProperty, value); }
        }
        #endregion

        #region ButtonType DependencyProperty
        public static readonly DependencyProperty ButtonTypeProperty =
            DependencyProperty.Register(
                "ButtonType",
                typeof(ButtonType),
                typeof(IconButton),
                new PropertyMetadata(ButtonType.Default));

        public ButtonType ButtonType
        {
            get { return (ButtonType)GetValue(ButtonTypeProperty); }
            set { SetValue(ButtonTypeProperty, value); }
        }
        #endregion

        #region CornerRadius DependencyProperty
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                "CornerRadius",
                typeof(CornerRadius),
                typeof(IconButton),
                new PropertyMetadata(new CornerRadius(6)));

        public CornerRadius CornerRadius
        {
            get { return (CornerRadius)GetValue(CornerRadiusProperty); }
            set { SetValue(CornerRadiusProperty, value); }
        }
        #endregion
    }

    public enum ButtonType
    {
        Default,
        Primary,
        Success,
        Warning,
        Danger,
        Info
    }
}