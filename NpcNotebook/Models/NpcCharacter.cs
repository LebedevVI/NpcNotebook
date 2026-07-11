using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NpcNotebook.Models;

public partial class NpcCharacter : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();

    [ObservableProperty]
    private string _name = "Без имени";

    [ObservableProperty]
    private string? _race;

    [ObservableProperty]
    private string? _age;

    [ObservableProperty]
    private string? _occupation;

    [ObservableProperty]
    private string? _personality;

    [ObservableProperty]
    private string? _portraitFileName;

    public ObservableCollection<Guid> GroupIds { get; } = [];

    public ObservableCollection<NpcRelationship> Relationships { get; } = [];

    public ObservableCollection<NpcDialogTab> DialogTabs { get; } = [];
}
