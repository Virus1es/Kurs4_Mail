using Microsoft.AspNetCore.Mvc;
using MailKit.Security;
using MailKit.Net.Imap;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit;
using System.Text.RegularExpressions;

namespace Kursach_Api.Controllers;

[Route("[controller]/{action}")]
[ApiController]
public class MailController : Controller
{
    public class MailRequest
    {
        public string Email { get; set; }
        public string AppPassword { get; set; }
    }

    // подключение клиента (проверка аккаунта на правильность ввода данных)
    [HttpPost]
    public IActionResult TestConnect([FromBody] MailRequest request)
    {

        using ImapClient imapClient = new();
        try
        {
            string connect = request.Email.EndsWith("@yandex.ru") || request.Email.EndsWith("@ya.ru") ? "imap.yandex.ru" : "imap.mail.ru";

            imapClient.Connect(connect, 993, SecureSocketOptions.SslOnConnect);
            imapClient.Authenticate(request.Email, request.AppPassword);

            imapClient.Disconnect(true);

            return Ok("Вход произведён успешно");
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }


    #region Чтение писем

    public class LettersMailRequest : MailRequest
    {
        public string Action { get; set; }
    }

    // Выбор имени отправителя
    public static string ExtractQuotedText(string input)
    {
        // Регулярное выражение для поиска текста в двойных кавычках
        Match match = Regex.Match(input, "\"([^\"]*)\"");

        return match.Success ? match.Groups[1].Value : input;
    }

    // получить письма на аккаунте
    [HttpPost]
    public IActionResult GetMails([FromBody] LettersMailRequest request)
    {
        try
        {
            using var client = new ImapClient();

            // строка подключения к нужному сервису
            string connect = request.Email.EndsWith("@yandex.ru") || request.Email.EndsWith("@ya.ru") ? "imap.yandex.ru" : "imap.mail.ru";

            // Подключение к IMAP-серверу
            client.Connect(connect, 993, SecureSocketOptions.SslOnConnect);

            // Аутентификация
            client.Authenticate(request.Email, request.AppPassword);

            // Используем словарь для более эффективного поиска папок
            var folders = new Dictionary<string, Func<IMailFolder>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Input",  () => client.Inbox },
                { "Output", () => connect.Contains("yandex") ? client.GetFolder("Sent") : client.GetFolder("Отправленные") },
                { "Drafts", () => connect.Contains("yandex") ? client.GetFolder("Drafts") : client.GetFolder("Черновики") },
                { "Trash",  () => connect.Contains("yandex") ? client.GetFolder("Trash") : client.GetFolder("Корзина")},
            };

            // проверяем что такая папка есть в словаре
            if (!folders.TryGetValue(request.Action, out var getFolder))
                throw new NotImplementedException($"Action '{request.Action}' not supported.");

            // получаем искомую папку
            var folder = getFolder();

            // создаём переменную для всех сообщений
            var messages = new List<object>();

            // Читаем последние письма
            folder.Open(FolderAccess.ReadOnly);

            // количество читаемых писем
            int messageCount = 10;

            // выбираем сколько писем конкретно читать
            // 10 или до конца
            int startIndex = Math.Max(0, folder.Count - messageCount);
            
            try
            {
                // получаем, допустим, первые 10 сообщений в папке
                for (int i = folder.Count - 1; i >= startIndex; i--)
                {
                    // берём сообщение
                    var message = folder.GetMessage(i);

                    // конвертируем его в удобный для нас формат
                    messages.Add(new
                    {
                        Id = i,
                        Subject = message.Subject,
                        From = ExtractQuotedText(message.From.ToString()),
                        Date = $"{message.Date:D}"
                    });
                }
            }
            finally
            {
                folder.Close(); // Закрываем папку в блоке finally
            }

            // Отключение
            client.Disconnect(true);

            return Ok(messages);
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    #endregion

    #region Отправка писем

    public class MailSendRequest
    {
        public string Email { get; set; }
        public string AppPassword { get; set; }
        public string RecipientEmail { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
    }

    // Отправка письма
    [HttpGet]
    public IActionResult SendMail([FromBody] MailSendRequest request)
    {
        try
        {
            string connect = request.Email.EndsWith("@yandex.ru") || request.Email.EndsWith("@ya.ru") ? "imap.yandex.ru" : "imap.mail.ru";


            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(request.Email, request.Email));
            message.To.Add(new MailboxAddress(request.RecipientEmail, request.RecipientEmail));
            message.Subject = request.Subject;
            message.Body = new TextPart("plain")
            {
                Text = request.Body
            };

            using (var client = new SmtpClient())
            {
                // Подключение к SMTP-серверу
                client.Connect(connect, 465, true);

                // Аутентификация
                client.Authenticate(request.Email, request.AppPassword);

                // Отправка
                client.Send(message);
                client.Disconnect(true);
            }

            return Ok(new { Status = "Email sent successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    #endregion
}
