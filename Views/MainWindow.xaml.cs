using System; // IDisposable을 위해
using System.ComponentModel; // CancelEventArgs를 위해
using System.Windows;
// using RealTimeVideoAnalysis.ViewModels; // ViewModel 타입에 직접 접근할 필요 없이 IDisposable로 충분

namespace RealTimeVideoAnalysis.Views
{
    /// <summary>
    /// MainWindow.xaml의 상호 작용 로직
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Closing 이벤트 핸들러 등록
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            // ViewModel이 IDisposable을 구현하면 Dispose 호출
            if (this.DataContext is IDisposable viewModel)
            {
                viewModel.Dispose();
            }
        }
    }
} 