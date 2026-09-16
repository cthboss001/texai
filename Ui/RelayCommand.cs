using System.Windows.Input;

namespace texAi.Ui;

/// <summary>
/// The whole of texAi's command infrastructure. A dashboard with four buttons
/// does not justify an MVVM framework, and this is the only piece of one it
/// actually needs.
/// </summary>
internal sealed class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => execute(parameter);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
