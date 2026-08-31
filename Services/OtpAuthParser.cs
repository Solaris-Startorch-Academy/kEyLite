using kEyLite.Models;

namespace kEyLite.Services;

/// <summary>解析 otpauth:// URI 并规范化密钥参数。</summary>
public static class OtpAuthParser
{
    public static TotpKey Parse(string text)
    {
        text = text.Trim();
        if (!text.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
            throw new FormatException("链接必须以 otpauth:// 开头。");

        Uri uri;
        try { uri = new Uri(text); }
        catch (UriFormatException) { throw new FormatException("无法解析链接格式。"); }

        string type = uri.Host.ToLowerInvariant();
        if (type != "totp")
            throw new FormatException($"暂不支持“{type}”类型的链接（仅支持 totp）。");

        var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string qs = uri.Query.StartsWith('?') ? uri.Query[1..] : uri.Query;
        foreach (var part in qs.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0) continue;
            string name = Uri.UnescapeDataString(part[..eq]).Trim();
            string value = Uri.UnescapeDataString(part[(eq + 1)..].Replace('+', ' '));
            query[name] = value;
        }

        string secret = query.TryGetValue("secret", out var s) ? s.Trim() : "";
        if (string.IsNullOrEmpty(secret))
            throw new FormatException("链接中缺少 secret 参数。");

        string rawLabel = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        string issuer = query.TryGetValue("issuer", out var iss) ? iss.Trim() : "";
        string label = rawLabel;
        int colon = rawLabel.IndexOf(':');
        if (colon >= 0)
        {
            string prefix = rawLabel[..colon].Trim();
            label = rawLabel[(colon + 1)..].Trim();
            if (string.IsNullOrEmpty(issuer)) issuer = prefix;
        }

        var key = new TotpKey
        {
            Issuer = issuer,
            Label = label,
            Secret = secret,
            Algorithm = query.TryGetValue("algorithm", out var alg) ? alg : "SHA1",
            Digits = int.TryParse(query.TryGetValue("digits", out var d) ? d : null, out int digits) ? digits : 6,
            Period = int.TryParse(query.TryGetValue("period", out var p) ? p : null, out int period) ? period : 30,
        };
        Normalize(key);
        return key;
    }

    /// <summary>规范化并校验密钥参数（Base32、算法、位数、周期）。</summary>
    public static void Normalize(TotpKey key)
    {
        key.Secret = key.Secret.Replace(" ", "").Replace("-", "").ToUpperInvariant();
        if (!Base32.IsValid(key.Secret))
            throw new FormatException("密钥不是有效的 Base32 字符串。");

        key.Algorithm = key.Algorithm?.ToUpperInvariant() switch
        {
            "SHA256" => "SHA256",
            "SHA512" => "SHA512",
            _ => "SHA1",
        };

        if (key.Digits is < 6 or > 8) key.Digits = 6;
        if (key.Period <= 0) key.Period = 30;
    }
}
