// src/WebsiteWhitelistManual.App/Pages/PolicyDetailDialog.xaml.cs
using System.Text;
using System.Windows;
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.App.Pages;

public partial class PolicyDetailDialog : Window
{
    public PolicyDetailDialog(PolicySnapshot snapshot)
    {
        InitializeComponent();
        ContentTextBox.Text = Format(snapshot);
    }

    private static string Format(PolicySnapshot snapshot)
    {
        var builder = new StringBuilder();
        foreach (var browser in snapshot.Browsers)
        {
            builder.AppendLine($"=== {browser.BrowserId} ===");
            builder.AppendLine($"政策機碼存在: {browser.PolicyKeyExists}");
            builder.AppendLine($"URLBlocklist: {string.Join(", ", browser.BlockedUrls)}");
            builder.AppendLine($"URLAllowlist: {string.Join(", ", browser.AllowedUrls)}");
            builder.AppendLine($"停用無痕模式: {browser.IncognitoDisabled}");
            builder.AppendLine($"停用帳號切換 (BrowserSignin=0): {browser.BrowserSigninDisabled}");
            builder.AppendLine($"停用開發人員工具: {browser.DeveloperToolsDisabled}");
            builder.AppendLine();
        }
        return builder.ToString();
    }
}
