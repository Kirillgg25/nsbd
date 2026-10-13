using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NSBD.Desktop.Views;

public enum CloseChoice
{
    Cancel,
    Minimize,
    Exit
}

public partial class CloseChoiceWindow : Window
{
    public bool RememberChoice => RememberCheckBox.IsChecked == true;

    public CloseChoiceWindow()
    {
        InitializeComponent();
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(CloseChoice.Cancel);

    private void Minimize_OnClick(object? sender, RoutedEventArgs e) => Close(CloseChoice.Minimize);

    private void Exit_OnClick(object? sender, RoutedEventArgs e) => Close(CloseChoice.Exit);
}
