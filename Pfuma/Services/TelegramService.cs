using System;
using System.Net;
using Telegram.Bot;

namespace Pfuma.Services;

public class TelegramService
{
    public TelegramService()
    {
        ServicePointManager.Expect100Continue = true;
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
    }

    public string SendTelegram(string chatId, string token, string telegramMessage)
    {
        string reply = string.Empty;
        long id = Convert.ToInt64(chatId);

        try
        {
            var bot = new TelegramBotClient(token);
            bot.SendTextMessageAsync(id, telegramMessage);
            reply = "SUCCESS";
        }
        catch (Exception ex)
        {
            reply = "ERROR: " + ex.Message;
        }

        return reply;
    }
}