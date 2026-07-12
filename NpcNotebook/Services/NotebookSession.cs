using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using NpcNotebook.Models;
using NpcNotebook.Models.Persistence;

namespace NpcNotebook.Services;

public sealed partial class NotebookSession : ObservableObject
{
    public static NotebookSession Current { get; } = new();

    private readonly Dictionary<string, BitmapSource> _portraits = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private string? _currentFilePath;

    [ObservableProperty]
    private bool _isDirty;

    public ObservableCollection<NpcGroup> Groups { get; } = [];

    public ObservableCollection<NpcCharacter> Characters { get; } = [];

    public event Action<NpcCharacter>? PortraitChanged;

    private NotebookSession()
    {
        Groups.CollectionChanged += (_, _) => MarkDirty();
        Characters.CollectionChanged += (_, _) => MarkDirty();
    }

    public void MarkDirty() => IsDirty = true;

    public void Clear()
    {
        Groups.Clear();
        Characters.Clear();
        _portraits.Clear();
        CurrentFilePath = null;
        IsDirty = false;
    }

    public NpcGroup CreateGroup(string name)
    {
        var group = new NpcGroup
        {
            Name = name.Trim(),
            SortOrder = Groups.Count
        };
        Groups.Add(group);
        MarkDirty();
        return group;
    }

    public NpcCharacter CreateCharacter()
    {
        var character = new NpcCharacter();
        Characters.Add(character);
        HookCharacter(character);
        MarkDirty();
        return character;
    }

    public void HookCharacter(NpcCharacter character)
    {
        character.PropertyChanged += OnCharacterPropertyChanged;
        character.GroupIds.CollectionChanged += (_, _) => MarkDirty();
        character.Relationships.CollectionChanged += (_, _) => MarkDirty();
        character.DialogTabs.CollectionChanged += (_, _) => MarkDirty();

        foreach (var relationship in character.Relationships)
            relationship.PropertyChanged += OnCharacterPropertyChanged;

        foreach (var tab in character.DialogTabs)
            tab.PropertyChanged += OnCharacterPropertyChanged;
    }

    private void OnCharacterPropertyChanged(object? sender, PropertyChangedEventArgs e) => MarkDirty();

    public void AddCharacterToGroup(NpcCharacter character, NpcGroup group)
    {
        if (!character.GroupIds.Contains(group.Id))
            character.GroupIds.Add(group.Id);
    }

    public void RemoveCharacterFromGroup(NpcCharacter character, NpcGroup group)
    {
        character.GroupIds.Remove(group.Id);
    }

    public void RemoveCharacterFromGroup(NpcCharacter character, Guid groupId)
    {
        character.GroupIds.Remove(groupId);
    }

    public bool IsInGroup(NpcCharacter character, Guid groupId) =>
        character.GroupIds.Contains(groupId);

    public IEnumerable<NpcCharacter> GetCharactersInGroup(NpcGroup? group)
    {
        if (group is null)
            return Characters.Where(c => c.GroupIds.Count == 0);

        return Characters.Where(c => c.GroupIds.Contains(group.Id));
    }

    public NpcCharacter? FindCharacter(Guid id) =>
        Characters.FirstOrDefault(c => c.Id == id);

    public string GetCharacterName(Guid id) =>
        FindCharacter(id)?.Name ?? "Неизвестный";

    public void DeleteCharacter(NpcCharacter character)
    {
        foreach (var other in Characters)
        {
            for (var i = other.Relationships.Count - 1; i >= 0; i--)
            {
                if (other.Relationships[i].TargetNpcId == character.Id)
                    other.Relationships.RemoveAt(i);
            }
        }

        if (character.PortraitFileName is { } fileName)
            _portraits.Remove(fileName);

        Characters.Remove(character);
        MarkDirty();
    }

    public void DeleteGroup(NpcGroup group)
    {
        foreach (var character in Characters)
            character.GroupIds.Remove(group.Id);

        Groups.Remove(group);
        MarkDirty();
    }

    public void RenameGroup(NpcGroup group, string name)
    {
        group.Name = name.Trim();
        MarkDirty();
    }

    public BitmapSource? GetPortrait(NpcCharacter character)
    {
        if (character.PortraitFileName is not { } fileName)
            return null;

        return _portraits.GetValueOrDefault(fileName);
    }

    public void SetPortraitFromFile(NpcCharacter character, string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp"))
            throw new InvalidOperationException("Поддерживаются изображения PNG, JPG, WEBP и BMP.");

        var fileName = $"{character.Id}{extension}";
        var image = LoadImage(sourcePath);
        _portraits[fileName] = image;
        character.PortraitFileName = fileName;
        PortraitChanged?.Invoke(character);
        MarkDirty();
    }

    public void ImportPortrait(NpcCharacter character, Stream stream, string extension)
    {
        var normalized = extension.StartsWith('.') ? extension : $".{extension}";
        var fileName = $"{character.Id}{normalized.ToLowerInvariant()}";
        var image = DecodeImage(stream);
        _portraits[fileName] = image;
        character.PortraitFileName = fileName;
        PortraitChanged?.Invoke(character);
    }

    public IEnumerable<(string FileName, BitmapSource Image)> ExportPortraits()
    {
        foreach (var pair in _portraits)
            yield return (pair.Key, pair.Value);
    }

    private static BitmapSource LoadImage(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static BitmapSource DecodeImage(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;

        var decoder = BitmapDecoder.Create(
            buffer,
            BitmapCreateOptions.None,
            BitmapCacheOption.OnLoad);

        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }

    public NotebookSaveData CreateSaveData()
    {
        return new NotebookSaveData
        {
            Groups = Groups.Select(g => new NpcGroupDto
            {
                Id = g.Id,
                Name = g.Name,
                SortOrder = g.SortOrder
            }).ToList(),
            Characters = Characters.Select(c => new NpcCharacterDto
            {
                Id = c.Id,
                Name = c.Name,
                Race = c.Race,
                Age = c.Age,
                Voice = c.Voice,
                DistinguishingFeatures = c.DistinguishingFeatures,
                Occupation = c.Occupation,
                Goal = c.Goal,
                Fear = c.Fear,
                Secret = c.Secret,
                PartyAttitude = c.PartyAttitude,
                Personality = c.Personality,
                PortraitFileName = c.PortraitFileName,
                GroupIds = c.GroupIds.ToList(),
                Relationships = c.Relationships.Select(r => new NpcRelationshipDto
                {
                    TargetNpcId = r.TargetNpcId,
                    Description = r.Description
                }).ToList(),
                DialogTabs = c.DialogTabs.Select(t => new NpcDialogTabDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Content = t.Content,
                    SortOrder = t.SortOrder
                }).ToList()
            }).ToList()
        };
    }

    public void ImportSaveData(NotebookSaveData data)
    {
        Clear();

        foreach (var groupDto in data.Groups.OrderBy(g => g.SortOrder))
        {
            Groups.Add(new NpcGroup
            {
                Id = groupDto.Id,
                Name = groupDto.Name,
                SortOrder = groupDto.SortOrder
            });
        }

        foreach (var characterDto in data.Characters)
        {
            var character = new NpcCharacter
            {
                Id = characterDto.Id,
                Name = characterDto.Name,
                Race = characterDto.Race,
                Age = characterDto.Age,
                Voice = characterDto.Voice,
                DistinguishingFeatures = characterDto.DistinguishingFeatures,
                Occupation = characterDto.Occupation,
                Goal = characterDto.Goal,
                Fear = characterDto.Fear,
                Secret = characterDto.Secret,
                PartyAttitude = Math.Clamp(characterDto.PartyAttitude, -100, 100),
                Personality = characterDto.Personality,
                PortraitFileName = characterDto.PortraitFileName
            };

            foreach (var groupId in characterDto.GroupIds)
                character.GroupIds.Add(groupId);

            foreach (var relationshipDto in characterDto.Relationships)
            {
                character.Relationships.Add(new NpcRelationship
                {
                    TargetNpcId = relationshipDto.TargetNpcId,
                    Description = relationshipDto.Description
                });
            }

            foreach (var tabDto in characterDto.DialogTabs.OrderBy(t => t.SortOrder))
            {
                character.DialogTabs.Add(new NpcDialogTab
                {
                    Id = tabDto.Id,
                    Title = tabDto.Title,
                    Content = tabDto.Content,
                    SortOrder = tabDto.SortOrder
                });
            }

            Characters.Add(character);
            HookCharacter(character);
        }

        IsDirty = false;
    }
}
