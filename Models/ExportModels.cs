namespace kEyLite.Models;

/// <summary>备份导出文件的根对象。</summary>
public class ExportData
{
    public string App { get; set; } = "kEyLite";

    public int FormatVersion { get; set; } = 1;

    public DateTimeOffset ExportedAt { get; set; }

    public List<ExportKey> Keys { get; set; } = new();
}

/// <summary>备份文件中的单条密钥信息（仅含密钥数据本身）。</summary>
public class ExportKey
{
    public string Issuer { get; set; } = "";

    public string Label { get; set; } = "";

    public string Secret { get; set; } = "";

    public string Algorithm { get; set; } = "SHA1";

    public int Digits { get; set; } = 6;

    public int Period { get; set; } = 30;
}
