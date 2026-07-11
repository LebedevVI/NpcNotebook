using CommunityToolkit.Mvvm.ComponentModel;

namespace NpcNotebook.Models;

public partial class NpcRelationship : ObservableObject
{
    [ObservableProperty]
    private Guid _targetNpcId;

    [ObservableProperty]
    private string _description = "";
}
