using System.Configuration;
using System.Data;
using System.Windows;
using KarateTournamentApp.Services;
using KarateTournamentApp.Services.Dialogs;
using KarateTournamentApp.Services.Navigation;
using KarateTournamentApp.Services.Scheduling;
using KarateTournamentApp.ViewModels;
using KarateTournamentApp.Views;

namespace KarateTournamentApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var categoryManager = new CategoryManager();
            var dialogService = new WpfDialogService();
            var windowService = new WpfWindowService();
            var uiScheduler = new WpfUiScheduler();
            RegisterViews(windowService);

            var mainViewModel = new MainViewModel(categoryManager, dialogService, windowService, uiScheduler);

            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            mainWindow.Show();
        }

        private static void RegisterViews(IWindowService windowService)
        {
            windowService.Register<ScoreboardViewModel, ScoreboardView>();
            windowService.Register<BracketPreviewViewModel, BracketPreviewView>();
            windowService.Register<ScoreboardJudgeViewModel, ScoreboardJudge>();
            windowService.Register<IndividualScoreboardViewModel, IndividualScoreboardView>();
            windowService.Register<IndividualJudgeViewModel, IndividualJudgeView>();
            windowService.Register<ResultsViewModel, ResultsView>();
            windowService.Register<DrawResolverViewModel, DrawResolverView>();
            windowService.Register<DrawResolverScoreboardViewModel, DrawResolverScoreboardView>();
        }
    }

}
