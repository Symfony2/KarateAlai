using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace KarateTournamentApp.Commands
{
    /// <summary>
    /// Asynchronous implementation of ICommand for handling async operations in MVVM
    /// </summary>
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Action<Exception> _onError;
        private readonly Func<bool>? _canExecute;
        private int _isExecuting;

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public AsyncRelayCommand(
            Func<Task> execute,
            Action<Exception> onError,
            Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _onError = onError ?? throw new ArgumentNullException(nameof(onError));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            if (Volatile.Read(ref _isExecuting) != 0)
                return false;

            return _canExecute?.Invoke() ?? true;
        }

        public void Execute(object? parameter)
        {
            _ = ExecuteAndHandleErrorsAsync();
        }

        public Task ExecuteAsync()
        {
            if (!CanExecute(null))
                return Task.CompletedTask;

            return ExecuteCoreAsync();
        }

        private async Task ExecuteCoreAsync()
        {
            if (Interlocked.CompareExchange(ref _isExecuting, 1, 0) != 0)
                return;

            RaiseCanExecuteChanged();
            try
            {
                await _execute();
            }
            finally
            {
                Volatile.Write(ref _isExecuting, 0);
                RaiseCanExecuteChanged();
            }
        }

        private async Task ExecuteAndHandleErrorsAsync()
        {
            try
            {
                await ExecuteAsync();
            }
            catch (Exception exception)
            {
                _onError(exception);
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// Asynchronous implementation of ICommand with parameter support
    /// </summary>
    public class AsyncRelayCommand<T> : ICommand
    {
        private readonly Func<T, Task> _execute;
        private readonly Action<Exception> _onError;
        private readonly Func<T, bool>? _canExecute;
        private int _isExecuting;

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public AsyncRelayCommand(
            Func<T, Task> execute,
            Action<Exception> onError,
            Func<T, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _onError = onError ?? throw new ArgumentNullException(nameof(onError));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            if (Volatile.Read(ref _isExecuting) != 0
                || !TryGetParameter(parameter, out var typedParameter))
                return false;

            return _canExecute?.Invoke(typedParameter) ?? true;
        }

        public void Execute(object? parameter)
        {
            _ = ExecuteAndHandleErrorsAsync(parameter);
        }

        public Task ExecuteAsync(T parameter)
        {
            if (!CanExecute(parameter))
                return Task.CompletedTask;

            return ExecuteCoreAsync(parameter);
        }

        private async Task ExecuteCoreAsync(T parameter)
        {
            if (Interlocked.CompareExchange(ref _isExecuting, 1, 0) != 0)
                return;

            RaiseCanExecuteChanged();
            try
            {
                await _execute(parameter);
            }
            finally
            {
                Volatile.Write(ref _isExecuting, 0);
                RaiseCanExecuteChanged();
            }
        }

        private async Task ExecuteAndHandleErrorsAsync(object? parameter)
        {
            try
            {
                if (!TryGetParameter(parameter, out var typedParameter))
                {
                    throw new ArgumentException(
                        $"Ожидался параметр команды типа {typeof(T).Name}.",
                        nameof(parameter));
                }

                await ExecuteAsync(typedParameter);
            }
            catch (Exception exception)
            {
                _onError(exception);
            }
        }

        private static bool TryGetParameter(object? parameter, out T typedParameter)
        {
            if (parameter is T value)
            {
                typedParameter = value;
                return true;
            }

            if (parameter == null && default(T) == null)
            {
                typedParameter = default!;
                return true;
            }

            typedParameter = default!;
            return false;
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
