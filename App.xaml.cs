using Prism.Unity;
using Prism.Ioc;
using RealTimeVideoAnalysis.Views;
using System.Windows;

namespace RealTimeVideoAnalysis
{
    /// <summary>
    /// App.xaml의 상호 작용 로직
    /// </summary>
    public partial class App : PrismApplication
    {
        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 향후 서비스 및 뷰-뷰모델 매핑을 이곳에 등록합니다.
        }
    }
}

