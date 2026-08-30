using System.Security.Cryptography;
using System.Text;
using kEyLite.Models;

namespace kEyLite.Services;

/// <summary>RFC 4648 Base32 解码。</summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static byte[] Decode(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new FormatException("密钥为空。");

        var values = new List<int>();
        foreach (char c in input)
        {
            if (c is ' ' or '-' or '\t') continue;
            int index = Alphabet.IndexOf(char.ToUpperInvariant(c));
            if (index < 0)
                throw new FormatException($"密钥包含无效的 Base32 字符：“{c}”。");
            values.Add(index);
        }

        if (values.Count < 2)
            throw new FormatException("密钥过短。");

        int byteCount = values.Count * 5 / 8;
        var result = new byte[byteCount];
        int bitPos = 0;
        for (int i = 0; i < byteCount; i++)
        {
            int value = 0;
            for (int b = 0; b < 8; b++)
            {
                int bit = (values[bitPos / 5] >> (4 - bitPos % 5)) & 1;
                value |= bit << (7 - b);
                bitPos++;
            }
            result[i] = (byte)value;
        }
        return result;
    }

    public static bool IsValid(string input)
    {
        try { Decode(input); return true; }
        catch { return false; }
    }
}

/// <summary>RFC 6238 TOTP 生成。</summary>
public static class Totp
{
    public static string Generate(string base32Secret, string algorithm, int digits, int period, DateTimeOffset? at = null)
    {
        byte[] key = Base32.Decode(base32Secret);
        long counter = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / period;

        using HMAC hmac = algorithm.ToUpperInvariant() switch
        {
            "SHA256" => new HMACSHA256(key),
            "SHA512" => new HMACSHA512(key),
            _ => new HMACSHA1(key),
        };

        byte[] data = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(data);
        byte[] hash = hmac.ComputeHash(data);

        int offset = hash[^1] & 0x0F;
        int binary = ((hash[offset] & 0x7F) << 24)
                   | (hash[offset + 1] << 16)
                   | (hash[offset + 2] << 8)
                   | hash[offset + 3];

        int mod = (int)Math.Pow(10, digits);
        return (binary % mod).ToString(new string('0', digits));
    }

    public static int RemainingSeconds(int period)
        => period - (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % period);
}
