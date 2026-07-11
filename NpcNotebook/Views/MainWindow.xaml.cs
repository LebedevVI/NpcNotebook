using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using NpcNotebook.Models;
using NpcNotebook.Models.Persistence;
using NpcNotebook.Services;
using NpcNotebook.ViewModels;

namespace NpcNotebook.Views;

public partial class MainWindow : Window
{
    private const string NpcDragFormat = "NpcNotebook/NpcId";

    private readonly MainViewModel _viewModel = new();
    private Point _dragStartPoint;
    private bool _isDragging;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        NotebookSession.Current.PortraitChanged += character =>
            _viewModel.NotifyPortraitChanged(character);

        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!NotebookSession.Current.IsDirty)
            return;

        var result = MessageBox.Show(
            "Сохранить изменения перед выходом?",
            "Блокнот NPC",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Cancel)
        {
            e.Cancel = true;
            return;
        }

        if (result == MessageBoxResult.Yes)
        {
            if (!TrySave(showSaveDialogIfNeeded: true))
                e.Cancel = true;
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (NotebookSession.Current.IsDirty &&
            MessageBox.Show("Открыть другой файл? Несохранённые изменения будут потеряны.",
                    "Открыть", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var dialog = new OpenFileDialog
        {
            Filter = $"Блокнот NPC (*{NotebookFileFormat.Extension})|*{NotebookFileFormat.Extension}",
            Title = "Открыть блокнот"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            NotebookPersistenceService.Load(NotebookSession.Current, dialog.FileName);
            _viewModel.SelectedCharacter = null;
            _viewModel.RefreshSections();
            _viewModel.StatusText = $"Открыт файл {Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка открытия", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e) => TrySave(showSaveDialogIfNeeded: true);

    private void SaveAs_Click(object sender, RoutedEventArgs e) => SaveAs();

    private bool TrySave(bool showSaveDialogIfNeeded)
    {
        var session = NotebookSession.Current;
        if (string.IsNullOrEmpty(session.CurrentFilePath))
        {
            if (!showSaveDialogIfNeeded)
                return false;
            return SaveAs();
        }

        try
        {
            NotebookPersistenceService.Save(session, session.CurrentFilePath);
            _viewModel.StatusText = "Блокнот сохранён.";
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private bool SaveAs()
    {
        var dialog = new SaveFileDialog
        {
            Filter = $"Блокнот NPC (*{NotebookFileFormat.Extension})|*{NotebookFileFormat.Extension}",
            Title = "Сохранить блокнот",
            FileName = string.IsNullOrEmpty(NotebookSession.Current.CurrentFilePath)
                ? "campaign.npcbook"
                : Path.GetFileName(NotebookSession.Current.CurrentFilePath)
        };

        if (dialog.ShowDialog() != true)
            return false;

        try
        {
            NotebookPersistenceService.Save(NotebookSession.Current, dialog.FileName);
            _viewModel.StatusText = $"Сохранено: {Path.GetFileName(dialog.FileName)}.";
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        var help = new HelpWindow { Owner = this };
        help.ShowDialog();
    }

    private void Portrait_Click(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.ChoosePortraitCommand.CanExecute(null))
            _viewModel.ChoosePortraitCommand.Execute(null);
    }

    private void NpcCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { DataContext: NpcCharacter character })
            return;

        _viewModel.SelectedCharacter = character;
    }

    private void NpcCard_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _isDragging)
            return;

        var position = e.GetPosition(null);
        if (Math.Abs(position.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (sender is not Border { DataContext: NpcCharacter character })
            return;

        _isDragging = true;
        var data = new DataObject(NpcDragFormat, character.Id.ToString());
        try
        {
            DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
        }
        finally
        {
            _isDragging = false;
        }
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        _dragStartPoint = e.GetPosition(null);
    }

    private void GroupHeader_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(NpcDragFormat) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void GroupHeader_Drop(object sender, DragEventArgs e)
    {
        if (sender is not Border { Tag: GroupSectionViewModel section })
            return;

        if (!e.Data.GetDataPresent(NpcDragFormat))
            return;

        var idText = e.Data.GetData(NpcDragFormat) as string;
        if (!Guid.TryParse(idText, out var npcId))
            return;

        var character = NotebookSession.Current.FindCharacter(npcId);
        if (character is null)
            return;

        _viewModel.MoveCharacterToGroup(character, section.Group);
        _viewModel.SelectedCharacter = character;
    }

    private void NpcCard_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border ||
            border.DataContext is not NpcCharacter character)
            return;

        var section = FindParentGroupSection(border);
        var menu = new ContextMenu();

        if (section?.Group is not null)
        {
            var group = section.Group;
            menu.Items.Add(new MenuItem
            {
                Header = $"Исключить из «{group.Name}»",
                Tag = (character, group),
                Command = _viewModel.RemoveFromGroupCommand,
                CommandParameter = (character, group)
            });
        }

        menu.Items.Add(new MenuItem
        {
            Header = "Удалить персонажа",
            Command = _viewModel.DeleteCharacterCommand,
            CommandParameter = character
        });

        menu.IsOpen = true;
        e.Handled = true;
    }

    private void GroupHeader_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: GroupSectionViewModel section } || section.Group is null)
            return;

        var menu = new ContextMenu
        {
            Items =
            {
                new MenuItem
                {
                    Header = "Переименовать",
                    Command = _viewModel.RenameGroupCommand,
                    CommandParameter = section.Group
                },
                new MenuItem
                {
                    Header = "Удалить группу",
                    Command = _viewModel.DeleteGroupCommand,
                    CommandParameter = section.Group
                }
            }
        };

        menu.IsOpen = true;
        e.Handled = true;
    }

    private static GroupSectionViewModel? FindParentGroupSection(DependencyObject child)
    {
        while (child is not null)
        {
            if (child is FrameworkElement { DataContext: GroupSectionViewModel section })
                return section;

            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private void DialogTabHeader_Click(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not TextBlock { DataContext: NpcDialogTab tab })
            return;

        if (_viewModel.RenameDialogTabCommand.CanExecute(tab))
            _viewModel.RenameDialogTabCommand.Execute(tab);
    }
}
