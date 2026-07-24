using System.Text.Json;
using System.Text.Json.Serialization;
using AvaloniaScheduleEditor.Models;

namespace AvaloniaScheduleEditor.Services;

/// <summary>
/// 组件自带的 JSON 配置存储。保存路径由宿主程序决定。
/// </summary>
public sealed class JsonScheduleStore : IScheduleStore
{
    private readonly JsonSerializerOptions _serializerOptions;

    public JsonScheduleStore(
        string filePath,
        JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        FilePath = Path.GetFullPath(filePath);
        _serializerOptions = serializerOptions is null
            ? CreateDefaultSerializerOptions()
            : new JsonSerializerOptions(serializerOptions);

        if (!_serializerOptions.Converters
                .Any(converter => converter is JsonStringEnumConverter))
        {
            _serializerOptions.Converters.Add(new JsonStringEnumConverter());
        }
    }

    /// <summary>
    /// JSON 配置文件的绝对路径。
    /// </summary>
    public string FilePath { get; }

    public async Task<ScheduleOptions?> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath))
            return null;

        await using var stream = new FileStream(
            FilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        var options = await JsonSerializer.DeserializeAsync<ScheduleOptions>(
            stream,
            _serializerOptions,
            cancellationToken);

        return options?.Normalize();
    }

    public async Task SaveAsync(
        ScheduleOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var normalized = options.Normalize();
        ScheduleOptionsValidator.Validate(normalized);

        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var temporaryPath = $"{FilePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 4096,
                             useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    normalized,
                    _serializerOptions,
                    cancellationToken);

                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static JsonSerializerOptions CreateDefaultSerializerOptions() =>
        new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };
}
