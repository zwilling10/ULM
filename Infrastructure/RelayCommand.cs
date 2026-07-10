// Infrastructure/RelayCommand.cs
using System;
using System.Windows.Input;

namespace ULM.Infrastructure
{
    /// <summary>Parameterloser ICommand für MVVM-Buttons.</summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly Action       _execute;
        private readonly Func<bool>?  _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute    = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add    => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) =>
            _canExecute is null || _canExecute();

        public void Execute(object? parameter) => _execute();

        /// <summary>Löst eine manuelle CanExecute-Aktualisierung aus.</summary>
        public static void RaiseCanExecuteChanged() =>
            CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>Parametrisierter ICommand für MVVM-Buttons.</summary>
    public sealed class RelayCommand<T> : ICommand
    {
        private readonly Action<T?>       _execute;
        private readonly Func<T?, bool>?  _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute    = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add    => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) =>
            _canExecute is null || _canExecute(parameter is T t ? t : default);

        public void Execute(object? parameter) =>
            _execute(parameter is T t ? t : default);
    }
}
