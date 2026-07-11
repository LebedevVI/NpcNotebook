using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NpcNotebook.Models;
using NpcNotebook.Services;

namespace NpcNotebook.ViewModels;

public partial class GroupSectionViewModel : ObservableObject
{
    public GroupSectionViewModel(NpcGroup? group, ObservableCollection<NpcCharacter> characters)
    {
        Group = group;
        Characters = characters;
        Title = group?.Name ?? "Без группы";
    }

    public NpcGroup? Group { get; }

    public ObservableCollection<NpcCharacter> Characters { get; }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _isExpanded = true;

    public Guid? GroupId => Group?.Id;
}

public partial class MainViewModel : ObservableObject
{
    private readonly NotebookSession _session = NotebookSession.Current;
    private NpcCharacter? _dialogTabsSubscriptionCharacter;

    [ObservableProperty]
    private NpcCharacter? _selectedCharacter;

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private NpcDialogTab? _selectedDialogTab;

    [ObservableProperty]
    private string _statusText = "Создайте группу или персонажа — или откройте сохранённый блокнот (.npcbook).";

    public ObservableCollection<GroupSectionViewModel> GroupSections { get; } = [];

    public ObservableCollection<NpcCharacter> RelationshipTargets { get; } = [];

    public bool HasDialogTabs => SelectedCharacter is { DialogTabs.Count: > 0 };

    public string WindowTitle
    {
        get
        {
            var name = string.IsNullOrEmpty(_session.CurrentFilePath)
                ? "Новый блокнот"
                : Path.GetFileName(_session.CurrentFilePath);
            return _session.IsDirty ? $"{name} * — Блокнот NPC" : $"{name} — Блокнот NPC";
        }
    }

    public MainViewModel()
    {
        _session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(NotebookSession.IsDirty) or nameof(NotebookSession.CurrentFilePath))
                OnPropertyChanged(nameof(WindowTitle));
        };

        _session.Groups.CollectionChanged += (_, _) => RefreshSections();
        _session.Characters.CollectionChanged += (_, _) => RefreshSections();
        RefreshSections();
    }

    partial void OnSearchQueryChanged(string value) => RefreshSections();

    partial void OnSelectedCharacterChanged(NpcCharacter? value)
    {
        if (_dialogTabsSubscriptionCharacter is not null)
            _dialogTabsSubscriptionCharacter.DialogTabs.CollectionChanged -= OnDialogTabsChanged;

        _dialogTabsSubscriptionCharacter = value;
        if (value is not null)
            value.DialogTabs.CollectionChanged += OnDialogTabsChanged;

        RefreshRelationshipTargets();
        SelectedDialogTab = value?.DialogTabs.FirstOrDefault();
        OnPropertyChanged(nameof(HasDialogTabs));
        StatusText = value is null
            ? "Выберите персонажа в списке слева."
            : $"Карточка: {value.Name}";
    }

    private void OnDialogTabsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        OnPropertyChanged(nameof(HasDialogTabs));

    public void RefreshSections()
    {
        GroupSections.Clear();
        var query = SearchQuery.Trim();

        foreach (var group in _session.Groups.OrderBy(g => g.SortOrder).ThenBy(g => g.Name))
        {
            var characters = FilterCharacters(_session.GetCharactersInGroup(group));
            if (characters.Count == 0 && !string.IsNullOrEmpty(query))
                continue;

            GroupSections.Add(new GroupSectionViewModel(group, characters)
            {
                IsExpanded = group.IsExpanded
            });
        }

        var ungrouped = FilterCharacters(_session.GetCharactersInGroup(null));
        if (ungrouped.Count > 0 || string.IsNullOrEmpty(query))
        {
            GroupSections.Add(new GroupSectionViewModel(null, ungrouped)
            {
                IsExpanded = true
            });
        }

        if (SelectedCharacter is not null &&
            !GroupSections.SelectMany(s => s.Characters).Any(c => c.Id == SelectedCharacter.Id))
        {
            // Keep selection even if filtered out
        }
    }

    private ObservableCollection<NpcCharacter> FilterCharacters(IEnumerable<NpcCharacter> source)
    {
        var query = SearchQuery.Trim();
        var list = string.IsNullOrEmpty(query)
            ? source.OrderBy(c => c.Name).ToList()
            : source.Where(c =>
                    c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (c.Race?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (c.Occupation?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                .OrderBy(c => c.Name)
                .ToList();

        return new ObservableCollection<NpcCharacter>(list);
    }

    public BitmapSource? GetPortrait(NpcCharacter character) => _session.GetPortrait(character);

    [RelayCommand]
    private void CreateGroup()
    {
        var name = Views.TextInputDialog.Show("Новая группа", "Название группы", "Например: Waterdeep");
        if (string.IsNullOrWhiteSpace(name))
            return;

        _session.CreateGroup(name);
        StatusText = $"Создана группа «{name.Trim()}». Перетащите персонажей на её заголовок.";
    }

    [RelayCommand]
    private void CreateCharacter()
    {
        var character = _session.CreateCharacter();
        SelectedCharacter = character;
        RefreshSections();
        StatusText = "Новый персонаж — заполните карточку справа.";
    }

    [RelayCommand]
    private void AddDialogTab()
    {
        if (SelectedCharacter is null)
            return;

        var tab = new NpcDialogTab
        {
            Title = $"Диалог {SelectedCharacter.DialogTabs.Count + 1}",
            SortOrder = SelectedCharacter.DialogTabs.Count
        };
        SelectedCharacter.DialogTabs.Add(tab);
        tab.PropertyChanged += (_, _) => _session.MarkDirty();
        SelectedDialogTab = tab;
        _session.MarkDirty();
    }

    [RelayCommand]
    private void RemoveDialogTab(NpcDialogTab? tab)
    {
        if (SelectedCharacter is null || tab is null)
            return;

        SelectedCharacter.DialogTabs.Remove(tab);
        SelectedDialogTab = SelectedCharacter.DialogTabs.FirstOrDefault();
        _session.MarkDirty();
    }

    [RelayCommand]
    private void RenameDialogTab(NpcDialogTab? tab)
    {
        if (tab is null)
            return;

        var name = Views.TextInputDialog.Show("Переименовать диалог", "Название вкладки", tab.Title);
        if (string.IsNullOrWhiteSpace(name))
            return;

        tab.Title = name.Trim();
        _session.MarkDirty();
    }

    [RelayCommand]
    private void AddRelationship()
    {
        if (SelectedCharacter is null)
            return;

        var others = _session.Characters.Where(c => c.Id != SelectedCharacter.Id).ToList();
        if (others.Count == 0)
        {
            StatusText = "Добавьте ещё одного персонажа, чтобы указать отношения.";
            return;
        }

        SelectedCharacter.Relationships.Add(new NpcRelationship
        {
            TargetNpcId = others[0].Id,
            Description = ""
        });
        var added = SelectedCharacter.Relationships[^1];
        added.PropertyChanged += (_, _) => _session.MarkDirty();
        _session.MarkDirty();
    }

    [RelayCommand]
    private void RemoveRelationship(NpcRelationship? relationship)
    {
        if (SelectedCharacter is null || relationship is null)
            return;

        SelectedCharacter.Relationships.Remove(relationship);
        _session.MarkDirty();
    }

    [RelayCommand]
    private void ChoosePortrait()
    {
        if (SelectedCharacter is null)
            return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Изображения|*.png;*.jpg;*.jpeg;*.webp;*.bmp",
            Title = "Выберите портрет персонажа"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            _session.SetPortraitFromFile(SelectedCharacter, dialog.FileName);
            OnPropertyChanged(nameof(SelectedCharacter));
            StatusText = "Портрет обновлён.";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    [RelayCommand]
    private void RenameGroup(NpcGroup? group)
    {
        if (group is null)
            return;

        var name = Views.TextInputDialog.Show("Переименовать группу", "Название группы", group.Name);
        if (string.IsNullOrWhiteSpace(name))
            return;

        _session.RenameGroup(group, name);
        RefreshSections();
    }

    [RelayCommand]
    private void DeleteGroup(NpcGroup? group)
    {
        if (group is null)
            return;

        var result = MessageBox.Show(
            $"Удалить группу «{group.Name}»?\nПерсонажи не будут удалены — только исключены из этой группы.",
            "Удалить группу",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        _session.DeleteGroup(group);
        RefreshSections();
        StatusText = "Группа удалена.";
    }

    [RelayCommand]
    private void DeleteCharacter(NpcCharacter? character)
    {
        if (character is null)
            return;

        var result = MessageBox.Show(
            $"Удалить персонажа «{character.Name}»?\nЭто действие нельзя отменить.",
            "Удалить персонажа",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        if (SelectedCharacter?.Id == character.Id)
            SelectedCharacter = null;

        _session.DeleteCharacter(character);
        RefreshSections();
        RefreshRelationshipTargets();
        StatusText = "Персонаж удалён.";
    }

    [RelayCommand]
    private void RemoveFromGroup((NpcCharacter Character, NpcGroup Group) args)
    {
        _session.RemoveCharacterFromGroup(args.Character, args.Group);
        RefreshSections();
        StatusText = $"«{args.Character.Name}» исключён из группы «{args.Group.Name}».";
    }

    public void MoveCharacterToGroup(NpcCharacter character, NpcGroup? group)
    {
        if (group is null)
        {
            StatusText = "Перетащите на группу, чтобы включить персонажа.";
            return;
        }

        _session.AddCharacterToGroup(character, group);
        RefreshSections();
        StatusText = $"«{character.Name}» добавлен в группу «{group.Name}».";
    }

    public void RefreshRelationshipTargets()
    {
        RelationshipTargets.Clear();
        if (SelectedCharacter is null)
            return;

        foreach (var character in _session.Characters.Where(c => c.Id != SelectedCharacter.Id).OrderBy(c => c.Name))
            RelationshipTargets.Add(character);
    }

    public string GetNpcName(Guid id) => _session.GetCharacterName(id);

    public void NotifyPortraitChanged(NpcCharacter character)
    {
        if (SelectedCharacter?.Id == character.Id)
            OnPropertyChanged(nameof(SelectedCharacter));
        RefreshSections();
    }
}
