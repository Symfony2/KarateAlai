using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace KarateTournamentApp.Services.Dialogs
{
    public class WpfDialogService : IDialogService
    {
        public DialogResult ShowMessage(string message, string title, DialogButtons buttons = DialogButtons.Ok, DialogIcon icon = DialogIcon.Information)
        {
            var result = MessageBox.Show(message, title, ToMessageBoxButton(buttons), ToMessageBoxImage(icon));
            return ToDialogResult(result);
        }

        public string? ShowTextInput(string title, string prompt, string defaultValue = "")
        {
            var textBox = new TextBox
            {
                Text = defaultValue,
                Margin = new Thickness(0, 0, 0, 12),
                MinWidth = 260,
                Padding = new Thickness(8)
            };

            var okButton = new Button
            {
                Content = "Сохранить",
                IsDefault = true,
                Width = 90,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var cancelButton = new Button
            {
                Content = "Отмена",
                IsCancel = true,
                Width = 90
            };

            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            buttonsPanel.Children.Add(okButton);
            buttonsPanel.Children.Add(cancelButton);

            var panel = new StackPanel
            {
                Margin = new Thickness(16)
            };
            panel.Children.Add(new TextBlock
            {
                Text = prompt,
                Margin = new Thickness(0, 0, 0, 8),
                FontWeight = FontWeights.SemiBold
            });
            panel.Children.Add(textBox);
            panel.Children.Add(buttonsPanel);

            var dialog = new Window
            {
                Title = title,
                Width = 360,
                Height = 170,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = panel
            };

            okButton.Click += (_, _) => dialog.DialogResult = true;

            return dialog.ShowDialog() == true ? textBox.Text : null;
        }

        public string? ShowOpenFileDialog(string title, string filter)
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter,
                Title = title
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string? ShowSaveFileDialog(string title, string filter, string defaultFileName)
        {
            var dialog = new SaveFileDialog
            {
                Filter = filter,
                Title = title,
                FileName = defaultFileName
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private static MessageBoxButton ToMessageBoxButton(DialogButtons buttons) => buttons switch
        {
            DialogButtons.OkCancel => MessageBoxButton.OKCancel,
            DialogButtons.YesNo => MessageBoxButton.YesNo,
            _ => MessageBoxButton.OK
        };

        private static MessageBoxImage ToMessageBoxImage(DialogIcon icon) => icon switch
        {
            DialogIcon.Information => MessageBoxImage.Information,
            DialogIcon.Warning => MessageBoxImage.Warning,
            DialogIcon.Error => MessageBoxImage.Error,
            DialogIcon.Question => MessageBoxImage.Question,
            _ => MessageBoxImage.None
        };

        private static DialogResult ToDialogResult(MessageBoxResult result) => result switch
        {
            MessageBoxResult.OK => DialogResult.Ok,
            MessageBoxResult.Cancel => DialogResult.Cancel,
            MessageBoxResult.Yes => DialogResult.Yes,
            MessageBoxResult.No => DialogResult.No,
            _ => DialogResult.Cancel
        };
    }
}
