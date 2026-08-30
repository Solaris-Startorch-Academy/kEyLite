using System.ComponentModel;
using System.Runtime.CompilerServices;
using kEyLite.Models;
using kEyLite.Services;

namespace kEyLite.ViewModels;

/// <summary>概览页单条密钥的显示模型：缓存验证码与倒计时状态。</summary>
public class KeyItem : INotifyPropertyChanged
{
    public TotpKey Key { get; }

    public KeyItem(TotpKey key) => Key = key;

    /// <summary>加粗显示的主标题：优先显示名称（Label），未提供名称时回退到签发者（Issuer）。</summary>
    public string Title
    {
        get
        {
            if (!string.IsNullOrEmpty(Key.Label)) return Key.Label;
            if (!string.IsNullOrEmpty(Key.Issuer)) return Key.Issuer;
            return "（未命名）";
        }
    }

    /// <summary>副标题：当主标题显示名称时展示签发者；主标题是签发者时不再重复显示签发者。附加算法/位数/周期。</summary>
    public string Subtitle
    {
        get
        {
            var parts = new List<string>();
            // 如果 Title 用了 Label 且存在 Issuer，则副栏显示 Issuer
            if (!string.IsNullOrEmpty(Key.Label) && !string.IsNullOrEmpty(Key.Issuer))
                parts.Add(Key.Issuer);
            else if (string.IsNullOrEmpty(Key.Label) && string.IsNullOrEmpty(Key.Issuer))
                parts.Add("未命名密钥");

            parts.Add($"{Key.Algorithm} · {Key.Digits} 位 · {Key.Period}s");
            return string.Join(" · ", parts);
        }
    }

    private string _code = "------";
    public string Code { get => _code; private set { _code = value; OnPropertyChanged(); } }

    /// <summary>不带空格的原始验证码，用于复制。</summary>
    public string RawCode { get; private set; } = "";

    private double _progress = 1;
    public double Progress { get => _progress; private set { _progress = value; OnPropertyChanged(); } }

    private string _remainingText = "";
    public string RemainingText { get => _remainingText; private set { _remainingText = value; OnPropertyChanged(); } }

    public void Update()
    {
        try
        {
            RawCode = Totp.Generate(Key.Secret, Key.Algorithm, Key.Digits, Key.Period);
        }
        catch
        {
            RawCode = "无效密钥";
        }

        Code = RawCode.Length == Key.Digits
            ? RawCode.Insert(RawCode.Length / 2, " ")
            : RawCode;

        int remain = Totp.RemainingSeconds(Key.Period);
        RemainingText = $"{remain} 秒后刷新";
        Progress = (double)remain / Key.Period;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
