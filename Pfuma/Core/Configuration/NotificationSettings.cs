namespace Pfuma.Core.Configuration;

public class NotificationSettings
{
    public bool EnableLog { get; set; } = false;
    public bool EnableTelegram { get; set; } = false;
    public bool NotifyLiquiditySweep { get; set; } = false;
    public string TelegramChatId { get; set; } = "5631623580";
    public string TelegramToken { get; set; } = "7507336625:AAHM4oYlg_5XIjzzCNFCR_oyLu1Y69qkvns";
}