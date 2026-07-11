using System.Windows;

namespace NpcNotebook.Views;

public partial class TextInputDialog : Window
{
    public TextInputDialog(string title, string prompt, string defaultValue = "")
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        InputBox.Text = defaultValue;
        InputBox.SelectAll();
        InputBox.Focus();
    }

    public string? Result { get; private set; }

    public static string? Show(string title, string prompt, string defaultValue = "")
    {
        var dialog = new TextInputDialog(title, prompt, defaultValue);
        if (Application.Current.MainWindow is { IsLoaded: true } owner && owner != dialog)
            dialog.Owner = owner;

        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result = InputBox.Text;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
