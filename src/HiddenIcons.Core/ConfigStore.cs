using System.Text.Json;
using System.Text.Json.Serialization;

namespace HiddenIcons.Core;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string FilePath { get; }

    /// <summary>最近一次 Load 是否失败。调用方据此跳过自动保存/自启动清理，避免空配置覆盖真实配置。</summary>
    public bool LastLoadFailed { get; private set; }

    public ConfigStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "HiddenIcons", "config.json");
    }

    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                LastLoadFailed = false;
                return new AppConfig();
            }
            var json = File.ReadAllText(FilePath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, Options) ?? new AppConfig();
            LastLoadFailed = false;
            return config;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // 读取失败绝不能把空配置当成真实状态返回出去用：
            // UI 侧会因此跳过自动保存与 RunKey 清理，服务侧只是短暂看到空列表（无副作用）。
            LastLoadFailed = true;
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        var directory = Path.GetDirectoryName(FilePath)
            ?? throw new InvalidOperationException("Invalid configuration path.");
        Directory.CreateDirectory(directory);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(config, Options));
        File.Move(temporary, FilePath, true);
    }
}
