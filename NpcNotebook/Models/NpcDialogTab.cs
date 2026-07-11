using CommunityToolkit.Mvvm.ComponentModel;

namespace NpcNotebook.Models;

public partial class NpcDialogTab : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();

    [ObservableProperty]
    private string _title = "Новый диалог";

    [ObservableProperty]
    private string _content = "";

    [ObservableProperty]
    private int _sortOrder;
}
