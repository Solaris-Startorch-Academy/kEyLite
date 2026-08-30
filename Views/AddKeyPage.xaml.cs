using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using kEyLite.Models;
using kEyLite.Services;
using kEyLite.Views.Dialogs;

namespace kEyLite.Views;

public partial class AddKeyPage : UserControl
{
    private TotpKey? _linkPending;
    private TotpKey? _qrPending;

    public AddKeyPage()
    {
        InitializeComponent();
    }

    private static string ComboTag(ComboBox box)
        => ((ComboBoxItem)box.SelectedItem).Tag?.ToString() ?? "SHA1";

    private static int ComboInt(ComboBox box)
        => int.Parse(((ComboBoxItem)box.SelectedItem).Tag?.ToString() ?? "0");

    private static string Describe(TotpKey k)
    {
        string name = string.IsNullOrEmpty(k.Issuer) ? k.Label : k.Issuer;
        return $"{(string.IsNullOrEmpty(name) ? "（未命名）" : name)} · {k.Algorithm} · {k.Digits} 位 · {k.Period}s";
    }

    private Window? OwnerWindow => Window.GetWindow(this);

    private void Commit(TotpKey key)
    {
        AppState.Vault.Keys.Add(key);
        AppState.Save();

        // 清空各表单
        IssuerBox.Text = "";
        LabelBox.Text = "";
        SecretBox.Text = "";
        _linkPending = null;
        LinkAddButton.IsEnabled = false;
        LinkPreview.Visibility = Visibility.Collapsed;
        LinkBox.Text = "";
        _qrPending = null;
        QrAddButton.IsEnabled = false;
        QrResultText.Visibility = Visibility.Collapsed;

        MainWindow.Instance?.ShowPage(0);
        ResultDialog.ShowSuccess(OwnerWindow);
    }

    // ---------- 手动输入 ----------

    private void ManualAdd_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var key = new TotpKey
            {
                Issuer = IssuerBox.Text.Trim(),
                Label = LabelBox.Text.Trim(),
                Secret = SecretBox.Text,
                Algorithm = ComboTag(AlgorithmBox),
                Digits = ComboInt(DigitsBox),
                Period = ComboInt(PeriodBox),
            };

            if (string.IsNullOrEmpty(key.Issuer) && string.IsNullOrEmpty(key.Label))
                throw new FormatException("请至少填写“签发者”或“显示名称”。");

            OtpAuthParser.Normalize(key);
            Commit(key);
        }
        catch (Exception ex)
        {
            ResultDialog.ShowError(OwnerWindow, "添加密钥失败", ex.Message);
        }
    }

    // ---------- otpauth 链接 ----------

    private void ParseLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var key = OtpAuthParser.Parse(LinkBox.Text);
            _linkPending = key;

            LinkIssuerText.Text = $"签发者：{(string.IsNullOrEmpty(key.Issuer) ? "（未提供）" : key.Issuer)}";
            LinkLabelText.Text = $"显示名称：{(string.IsNullOrEmpty(key.Label) ? "（未提供）" : key.Label)}";
            LinkDetailText.Text = $"算法：{key.Algorithm} · 位数：{key.Digits} · 周期：{key.Period}s";
            LinkPreview.Visibility = Visibility.Visible;
            LinkAddButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            _linkPending = null;
            LinkAddButton.IsEnabled = false;
            LinkPreview.Visibility = Visibility.Collapsed;
            ResultDialog.ShowError(OwnerWindow, "解析 otpauth 链接失败", ex.Message);
        }
    }

    private void LinkAdd_Click(object sender, RoutedEventArgs e)
    {
        if (_linkPending is null) return;
        Commit(_linkPending);
    }

    // ---------- QR 码图片 ----------

    private void PickImage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "选择二维码图片",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件|*.*",
        };
        if (dlg.ShowDialog(OwnerWindow) != true) return;

        try
        {
            string? text = QrService.DecodeFile(dlg.FileName);
            if (string.IsNullOrEmpty(text))
                throw new FormatException("未能从图片中识别出二维码。");

            TotpKey key;
            if (text.TrimStart().StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
            {
                key = OtpAuthParser.Parse(text);
            }
            else
            {
                // 有些二维码只包含密钥本身
                var plain = new TotpKey { Secret = text };
                OtpAuthParser.Normalize(plain);
                key = plain;
            }

            _qrPending = key;
            QrResultText.Text = $"识别成功：{Describe(key)}";
            QrResultText.Visibility = Visibility.Visible;
            QrAddButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            _qrPending = null;
            QrAddButton.IsEnabled = false;
            QrResultText.Visibility = Visibility.Collapsed;
            ResultDialog.ShowError(OwnerWindow, "识别二维码失败", ex.Message);
        }
    }

    private void QrAdd_Click(object sender, RoutedEventArgs e)
    {
        if (_qrPending is null) return;
        Commit(_qrPending);
    }
}
