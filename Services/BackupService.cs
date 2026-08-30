using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using kEyLite.Models;

namespace kEyLite.Services;

/// <summary>
/// 备份导出/导入。
/// 导出：纯 JSON（UTF-8 无 BOM，带缩进，仅含密钥信息，不含应用设置）。
/// 导入：优先按纯 JSON 解析；为兼容旧版本，失败后再尝试按 Base64 解码后解析 JSON。
/// </summary>
public static class BackupService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string ExportToJson(IEnumerable<TotpKey> keys)
    {
        var data = new ExportData
        {
            ExportedAt = DateTimeOffset.Now,
            Keys = keys.Select(k => new ExportKey
            {
                Issuer = k.Issuer,
                Label = k.Label,
                Secret = k.Secret,
                Algorithm = k.Algorithm,
                Digits = k.Digits,
                Period = k.Period,
            }).ToList(),
        };

        return JsonSerializer.Serialize(data, JsonOpts);
    }

    public static List<TotpKey> Import(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new FormatException("备份文件内容为空。");

        string? json = null;

        // 1. 优先按纯 JSON 解析
        string compact = new string(content.Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (compact.StartsWith('{') || compact.StartsWith('['))
        {
            json = content;
        }
        else
        {
            // 2. 兼容：旧版本 Base64 编码的 JSON
            try
            {
                json = Encoding.UTF8.GetString(Convert.FromBase64String(compact));
            }
            catch (FormatException)
            {
                throw new FormatException("无法识别的备份文件：内容无效。");
            }
        }

        var data = JsonSerializer.Deserialize<ExportData>(json, JsonOpts)
                   ?? throw new FormatException("备份文件内容无法解析。");

        var result = new List<TotpKey>();
        foreach (var k in data.Keys)
        {
            var key = new TotpKey
            {
                Issuer = k.Issuer?.Trim() ?? "",
                Label = k.Label?.Trim() ?? "",
                Secret = k.Secret ?? "",
                Algorithm = k.Algorithm ?? "SHA1",
                Digits = k.Digits,
                Period = k.Period,
            };
            OtpAuthParser.Normalize(key);
            result.Add(key);
        }
        return result;
    }
}
