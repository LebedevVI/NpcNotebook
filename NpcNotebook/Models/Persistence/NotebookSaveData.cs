namespace NpcNotebook.Models.Persistence;

public sealed class NotebookSaveData
{
    public int FormatVersion { get; init; } = NotebookFileFormat.CurrentVersion;

    public List<NpcGroupDto> Groups { get; init; } = [];

    public List<NpcCharacterDto> Characters { get; init; } = [];
}

public sealed class NpcGroupDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public int SortOrder { get; init; }
}

public sealed class NpcCharacterDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string? Race { get; init; }
    public string? Age { get; init; }
    public string? Voice { get; init; }
    public string? DistinguishingFeatures { get; init; }
    public string? Occupation { get; init; }
    public string? Goal { get; init; }
    public string? Fear { get; init; }
    public string? Secret { get; init; }
    public int PartyAttitude { get; init; }
    public string? Personality { get; init; }
    public string? PortraitFileName { get; init; }
    public List<Guid> GroupIds { get; init; } = [];
    public List<NpcRelationshipDto> Relationships { get; init; } = [];
    public List<NpcDialogTabDto> DialogTabs { get; init; } = [];
}

public sealed class NpcRelationshipDto
{
    public Guid TargetNpcId { get; init; }
    public string Description { get; init; } = "";
}

public sealed class NpcDialogTabDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public string Content { get; init; } = "";
    public int SortOrder { get; init; }
}

public static class NotebookFileFormat
{
    public const string Extension = ".npcbook";
    public const int CurrentVersion = 1;
    public const string DataEntryName = "notebook.json";
    public const string PortraitsFolder = "portraits/";
}
