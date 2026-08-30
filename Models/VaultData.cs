using System.Collections.ObjectModel;

namespace kEyLite.Models;

/// <summary>保险库：所有需要持久化的信息，序列化为 JSON 后整体加密存储。</summary>
public class VaultData
{
    public int Version { get; set; } = 1;

    public AppSettings Settings { get; set; } = new();

    public ObservableCollection<TotpKey> Keys { get; set; } = new();
}
