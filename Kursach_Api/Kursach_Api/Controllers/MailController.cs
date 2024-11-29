using Microsoft.AspNetCore.Mvc;
using MailKit.Security;
using MailKit.Net.Imap;
using MimeKit;
using MailKit.Net.Smtp;

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

    // Текущий клиент
    private static ImapClient _imapClient;

    // подключение клиента
    [HttpPost]
    public IActionResult TestConnect([FromBody] MailRequest request) { 
    
        using ImapClient imapClient = new();
        try
        {
            string connect = request.Email.Contains("@yandex.ru") ? "imap.yandex.ru" : "imap.mail.ru";

            _imapClient = new ImapClient();
            _imapClient.Connect(connect, 993, SecureSocketOptions.SslOnConnect);
            _imapClient.Authenticate(request.Email, request.AppPassword);

            _imapClient.Disconnect(true);

            return Ok("Вход произведён успешно");
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    // получить письма на аккаунте
    [HttpPost]
    public IActionResult GetMails([FromBody] MailRequest request)
    {
        try
        {
            using (var client = new ImapClient())
            {
                // Подключение к IMAP-серверу
                client.Connect("imap.yandex.ru", 993, SecureSocketOptions.SslOnConnect);

                // Аутентификация
                client.Authenticate(request.Email, request.AppPassword);

                // Открываем папку "Входящие"
                client.Inbox.Open(MailKit.FolderAccess.ReadOnly);

                // Читаем последние письма
                var messages = new List<object>();
                for (int i = 0; i < client.Inbox.Count && i < 10; i++)
                {
                    var message = client.Inbox.GetMessage(i);
                    messages.Add(new
                    {
                        Subject = message.Subject,
                        From = message.From.ToString(),
                        Date = message.Date
                    });
                }

                // Отключение
                client.Disconnect(true);

                return Ok(messages);
            }
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

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
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Your App", request.Email));
            message.To.Add(new MailboxAddress("Recipient", request.RecipientEmail));
            message.Subject = request.Subject;
            message.Body = new TextPart("plain")
            {
                Text = request.Body
            };

            using (var client = new SmtpClient())
            {
                // Подключение к SMTP-серверу
                client.Connect("smtp.yandex.ru", 465, true);

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
}
