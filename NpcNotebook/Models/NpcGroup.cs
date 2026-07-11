using CommunityToolkit.Mvvm.ComponentModel;

namespace NpcNotebook.Models;

public partial class NpcGroup : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private int _sortOrder;

    [ObservableProperty]
    private bool _isExpanded = true;
}
