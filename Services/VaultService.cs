using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using kEyLite.Models;

namespace kEyLite.Services;

/// <summary>
/// 负责保险库的加密存取：
/// - 启用访问密码：PBKDF2-SHA256 派生密钥 + AES-256-GCM 认证加密；
/// - 未启用密码：Windows DPAPI（当前用户）加密。
/// 明文内容始终为 JSON。
/// 磁盘数据始终处于加密状态；“解锁”仅指把密钥解密到内存。
/// </summary>
public static class VaultService
{
    private const string PasswordMagic = "KLVT1"; // 密码加密格式头
    private const string PlainMagic = "KLVD1";    // DPAPI 格式头
    private const int Pbkdf2Iterations = 210_000;
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    /// <summary>数据目录：应用目录下的 Data 文件夹（便携模式，数据随程序走）。</summary>
    public static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "Data");

    public static string FilePath => Path.Combine(DataDir, "vault.dat");

    static VaultService()
    {
        // 旧版本数据迁移：AppData\kEyLite → 应用目录\Data
        try
        {
            string legacyDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "kEyLite");
            string legacyFile = Path.Combine(legacyDir, "vault.dat");
            if (File.Exists(legacyFile) && !File.Exists(FilePath))
            {
                Directory.CreateDirectory(DataDir);
                File.Move(legacyFile, FilePath);
            }
        }
        catch
        {
            // 迁移失败不影响启动
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static bool Exists() => File.Exists(FilePath);

    public static byte[] ReadBlob() => File.ReadAllBytes(FilePath);

    /// <summary>根据格式头判断数据文件是否为密码加密（不进行解密）。</summary>
    public static bool IsPasswordProtected(byte[] blob) => StartsWith(blob, PasswordMagic);

    /// <summary>尝试用密码解锁；密码错误或数据损坏返回 null。</summary>
    public static VaultData? TryUnlockWithPassword(byte[] blob, string password)
    {
        try { return DecryptWithPassword(blob, password); }
        catch (CryptographicException) { return null; }
        catch (ArgumentException) { return null; }
    }

    /// <summary>以 DPAPI 解密（无需密码）；数据损坏时抛出异常。</summary>
    public static VaultData DecryptDpapi(byte[] blob) => DecryptDpapiCore(blob);

    public static void Save(VaultData vault, string? password)
    {
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(vault, JsonOpts);
        byte[] blob;

        if (vault.Settings.PasswordEnabled)
        {
            if (string.IsNullOrEmpty(password))
                throw new InvalidOperationException("已启用访问密码，但当前会话没有密码。");

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
            byte[] key = DeriveKey(password, salt);
            using var aes = new AesGcm(key, TagSize);
            byte[] cipher = new byte[plain.Length];
            byte[] tag = new byte[TagSize];
            aes.Encrypt(nonce, plain, cipher, tag);

            using var ms = new MemoryStream();
            ms.Write(Encoding.ASCII.GetBytes(PasswordMagic));
            ms.Write(salt);
            ms.Write(nonce);
            ms.Write(tag);
            ms.Write(cipher);
            blob = ms.ToArray();
        }
        else
        {
            byte[] protectedData = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            using var ms = new MemoryStream();
            ms.Write(Encoding.ASCII.GetBytes(PlainMagic));
            ms.Write(protectedData);
            blob = ms.ToArray();
        }

        File.WriteAllBytes(FilePath, blob);
    }

    private static VaultData DecryptWithPassword(byte[] blob, string password)
    {
        int offset = PasswordMagic.Length;
        byte[] salt = blob[offset..(offset + SaltSize)]; offset += SaltSize;
        byte[] nonce = blob[offset..(offset + NonceSize)]; offset += NonceSize;
        byte[] tag = blob[offset..(offset + TagSize)]; offset += TagSize;
        byte[] cipher = blob[offset..];

        byte[] key = DeriveKey(password, salt);
        using var aes = new AesGcm(key, TagSize);
        byte[] plain = new byte[cipher.Length];
        aes.Decrypt(nonce, cipher, tag, plain);
        return Deserialize(plain);
    }

    private static VaultData DecryptDpapiCore(byte[] blob)
    {
        int offset = PlainMagic.Length;
        byte[] protectedData = blob[offset..];
        byte[] plain = ProtectedData.Unprotect(protectedData, null, DataProtectionScope.CurrentUser);
        return Deserialize(plain);
    }

    private static VaultData Deserialize(byte[] plain)
        => JsonSerializer.Deserialize<VaultData>(plain, JsonOpts)
           ?? throw new FormatException("数据文件为空或已损坏。");

    private static byte[] DeriveKey(string password, byte[] salt)
        => Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, KeySize);

    private static bool StartsWith(byte[] data, string magic)
    {
        var m = Encoding.ASCII.GetBytes(magic);
        return data.Length >= m.Length && data.AsSpan(0, m.Length).SequenceEqual(m);
    }
}
