namespace Pfuma.Core.Configuration;

public class NotificationSettings
{
    public bool EnableLog { get; set; } = false;
    public bool SendLiquidity { get; set; } = false;
    public bool SendCycles { get; set; } = false;
    public bool SendSMT { get; set; } = false;
    public bool SendCISD { get; set; } = false;
    public bool SendOrderBlock { get; set; } = false;
    public bool SendInsideKeyLevel { get; set; } = false;
    public string TelegramChatId { get; set; } = "5631623580";
    public string TelegramToken { get; set; } = "7507336625:AAHM4oYlg_5XIjzzCNFCR_oyLu1Y69qkvns";
}