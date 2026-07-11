using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NpcNotebook.Models.Persistence;

namespace NpcNotebook.Services;

public static class NotebookPersistenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Save(NotebookSession session, string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = filePath + ".tmp";
        if (File.Exists(tempPath))
            File.Delete(tempPath);

        try
        {
            using (var archive = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                WriteJson(archive, session.CreateSaveData());
                WritePortraits(archive, session);
            }

            if (File.Exists(filePath))
                File.Delete(filePath);

            File.Move(tempPath, filePath);
            session.CurrentFilePath = filePath;
            session.IsDirty = false;
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }

    public static void Load(NotebookSession session, string filePath)
    {
        using var archive = ZipFile.OpenRead(filePath);

        var jsonEntry = archive.GetEntry(NotebookFileFormat.DataEntryName)
            ?? throw new InvalidDataException("В файле нет данных блокнота.");

        NotebookSaveData data;
        using (var jsonStream = jsonEntry.Open())
            data = JsonSerializer.Deserialize<NotebookSaveData>(jsonStream, JsonOptions)
                   ?? throw new InvalidDataException("Не удалось прочитать данные блокнота.");

        if (data.FormatVersion > NotebookFileFormat.CurrentVersion)
            throw new InvalidDataException(
                $"Файл создан в более новой версии программы (формат {data.FormatVersion}).");

        session.ImportSaveData(data);

        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.StartsWith(NotebookFileFormat.PortraitsFolder, StringComparison.OrdinalIgnoreCase))
                continue;

            if (entry.FullName.EndsWith('/'))
                continue;

            var fileName = Path.GetFileName(entry.FullName);
            var characterId = Path.GetFileNameWithoutExtension(fileName);
            if (!Guid.TryParse(characterId, out var id))
                continue;

            var character = session.FindCharacter(id);
            if (character is null)
                continue;

            using var stream = entry.Open();
            var extension = Path.GetExtension(fileName);
            session.ImportPortrait(character, stream, extension);
        }

        session.CurrentFilePath = filePath;
        session.IsDirty = false;
    }

    private static void WriteJson(ZipArchive archive, NotebookSaveData data)
    {
        var entry = archive.CreateEntry(NotebookFileFormat.DataEntryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        JsonSerializer.Serialize(stream, data, JsonOptions);
    }

    private static void WritePortraits(ZipArchive archive, NotebookSession session)
    {
        foreach (var (fileName, image) in session.ExportPortraits())
        {
            var entry = archive.CreateEntry(
                NotebookFileFormat.PortraitsFolder + fileName,
                CompressionLevel.Optimal);

            using var stream = entry.Open();
            SaveBitmap(image, stream, Path.GetExtension(fileName));
        }
    }

    private static void SaveBitmap(BitmapSource source, Stream destination, string extension)
    {
        BitmapEncoder encoder = extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => new JpegBitmapEncoder(),
            ".webp" => new PngBitmapEncoder(),
            _ => new PngBitmapEncoder()
        };

        var encodable = ToEncodableBitmap(source);
        encoder.Frames.Add(BitmapFrame.Create(encodable));
        encoder.Save(destination);
    }

    private static BitmapSource ToEncodableBitmap(BitmapSource source)
    {
        if (source.CanFreeze && source.Format == PixelFormats.Pbgra32)
        {
            source.Freeze();
            return source;
        }

        var converted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        converted.Freeze();
        return converted;
    }
}
