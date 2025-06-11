using Prism.Mvvm;
using System.Windows; // Rect를 위해

namespace RealTimeVideoAnalysis.Models
{
    public class RoiModel : BindableBase
    {
        private double _x;
        public double X
        {
            get => _x;
            set
            {
                if (SetProperty(ref _x, value))
                {
                    RaisePropertyChanged(nameof(Rect));
                    RaisePropertyChanged(nameof(CvRect));
                }
            }
        }

        private double _y;
        public double Y
        {
            get => _y;
            set
            {
                if (SetProperty(ref _y, value))
                {
                    RaisePropertyChanged(nameof(Rect));
                    RaisePropertyChanged(nameof(CvRect));
                }
            }
        }

        private double _width;
        public double Width
        {
            get => _width;
            set
            {
                if (SetProperty(ref _width, value))
                {
                    RaisePropertyChanged(nameof(IsDefined));
                    RaisePropertyChanged(nameof(Rect));
                    RaisePropertyChanged(nameof(CvRect));
                }
            }
        }

        private double _height;
        public double Height
        {
            get => _height;
            set
            {
                if (SetProperty(ref _height, value))
                {
                    RaisePropertyChanged(nameof(IsDefined));
                    RaisePropertyChanged(nameof(Rect));
                    RaisePropertyChanged(nameof(CvRect));
                }
            }
        }

        public bool IsDefined => Width > 0 && Height > 0;

        // UI 또는 그리기에 편리한 WPF Rect
        public Rect Rect => new Rect(X, Y, Width, Height);

        // 처리에 편리한 OpenCV Rect
        public OpenCvSharp.Rect CvRect => new OpenCvSharp.Rect((int)X, (int)Y, (int)Width, (int)Height);


        public RoiModel(double x = 0, double y = 0, double width = 0, double height = 0)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public void Reset()
        {
            X = 0;
            Y = 0;
            Width = 0;
            Height = 0;
        }
    }
}