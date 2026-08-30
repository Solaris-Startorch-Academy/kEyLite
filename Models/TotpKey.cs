namespace kEyLite.Models;

/// <summary>一条 TOTP 密钥记录。</summary>
public class TotpKey
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>签发者（如 GitHub）。</summary>
    public string Issuer { get; set; } = "";

    /// <summary>显示名称 / 账户名。</summary>
    public string Label { get; set; } = "";

    /// <summary>Base32 编码的密钥。</summary>
    public string Secret { get; set; } = "";

    /// <summary>HMAC 算法：SHA1 / SHA256 / SHA512。</summary>
    public string Algorithm { get; set; } = "SHA1";

    /// <summary>验证码位数（6-8）。</summary>
    public int Digits { get; set; } = 6;

    /// <summary>刷新周期（秒）。</summary>
    public int Period { get; set; } = 30;
}
