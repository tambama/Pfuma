namespace Pfuma.Core.Configuration;

public class NotificationSettings
{
    public bool EnableLog { get; set; } = false;
    public bool EnableTelegram { get; set; } = false;
    public string TelegramChatId { get; set; } = "";
    public string TelegramToken { get; set; } = "";
}