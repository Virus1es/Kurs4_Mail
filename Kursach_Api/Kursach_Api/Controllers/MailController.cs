using Microsoft.AspNetCore.Mvc;
using MailKit.Security;
using MailKit.Net.Imap;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Kursach_Api.Models;
using System.Xml;
using Microsoft.EntityFrameworkCore;

namespace Kursach_Api.Controllers;

[Route("[controller]/{action}")]
[ApiController]
public class MailController(MailDbContext context) : Controller
{
    // соединение с БД
    // ссылка на базу данных
    private MailDbContext _db = context;

    // папка с пользователями
    private readonly string _usersFolder = "users";

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

    // Выбор имени ящика
    public static string ExtractQuotedText(string input)
    {
        // Регулярное выражение для поиска текста в двойных кавычках
        Match match = Regex.Match(input, "\"([^\"]*)\"");

        return match.Success ? match.Groups[1].Value : input;
    }

    #region Дешифрование

    // дешифрование тела письма
    public string DecriptMailBody(MimeMessage message)
    {
        string filePath = $"{_usersFolder}/{ExtractQuotedText(message.To.ToString())}/keys/for {ExtractQuotedText(message.From.ToString())}.xml";

        // находим закрытый ключ получателя для дешифрования данных от конкретного отправителя
        if (!System.IO.File.Exists(filePath))
            throw new Exception("Файл закрытого ключа не найден");

        // получить приватный ключ RSA
        RSAParameters privateKey = ParsePrivateKeyFromXml(filePath);

        // получить текст письма
        string combinedDataStringFromMail = getMailText(message);

        // Преобразование обратно в байты
        byte[] combinedDataFromMail = Convert.FromBase64String(combinedDataStringFromMail);

        // Извлекаем длину
        byte[] lengthBytesFromMail = combinedDataFromMail.Take(sizeof(int)).ToArray();
        if (BitConverter.IsLittleEndian)
            Array.Reverse(lengthBytesFromMail); // Если Little Endian,  нужно изменить порядок байт
        int encryptedTripleDESKeyLength = BitConverter.ToInt32(lengthBytesFromMail);

        // Извлекаем encryptedTripleDESKey и encryptedTripleDES
        byte[] encryptedTripleDESKey = combinedDataFromMail.Skip(sizeof(int)).Take(encryptedTripleDESKeyLength).ToArray();
        byte[] encryptedTripleDES = combinedDataFromMail.Skip(sizeof(int) + encryptedTripleDESKeyLength).ToArray();

        // Расшифровываем ключ TripleDES с помощью RSA
        byte[] decryptedTripleDESKey = DecryptRSA(encryptedTripleDESKey, privateKey);

        // Расшифровываем сообщение с помощью TripleDES и расшифрованного ключа
        string decryptedText = DecryptTripleDES(encryptedTripleDES, decryptedTripleDESKey);

        return decryptedText;
    }

    private string getMailText(MimeMessage message)
    {
        string extractedText = "";

        if (message.Body != null)
        {
            if (message.Body is TextPart textPart)
                extractedText = textPart.Text;
            else if (message.Body is Multipart multipart)
                extractedText = ExtractTextFromMultipart(multipart);
            else if (message.Body is MessagePart messagePart)
                //Рекурсивно обрабатываем вложенное сообщение
                extractedText = ExtractTextFromMessagePart(messagePart);
            else
                Console.WriteLine($"Неподдерживаемый тип Body: {message.Body.GetType()}");
        }
        else
            Console.WriteLine("Сообщение не содержит Body");

        return extractedText;
    }

    // Рекурсивная функция для обработки Multipart
    string ExtractTextFromMultipart(Multipart multipart)
    {
        string text = "";
        foreach (var part in multipart)
        {
            if (part is TextPart textPart)
                text += textPart.Text + Environment.NewLine;
            else if (part is Multipart nestedMultipart)
                text += ExtractTextFromMultipart(nestedMultipart);
            else if (part is MessagePart messagePart)
                text += ExtractTextFromMessagePart(messagePart);
            else
                Console.WriteLine($"Неподдерживаемый тип части: {part.ContentType}");
        }
        return text;
    }

    // Рекурсивная функция для обработки вложенных сообщений
    string ExtractTextFromMessagePart(MessagePart messagePart)
    {
        if (messagePart.Message.Body != null)
        {
            if (messagePart.Message.Body is TextPart textPart) 
                return textPart.Text;
            else if (messagePart.Message.Body is Multipart multipart) 
                return ExtractTextFromMultipart(multipart);
            else
            {
                Console.WriteLine($"Неподдерживаемый тип Body во вложенном сообщении: {messagePart.Message.Body.GetType()}");
                return "";
            }
        }
        else
        {
            Console.WriteLine("Вложенное сообщение не содержит Body");
            return "";
        }
    }


    // Расшифровывает текст, зашифрованный TripleDES
    public static string DecryptTripleDES(byte[] encryptedData, byte[] key)
    {
        using TripleDESCryptoServiceProvider tripleDES = new()
        {
            Key = key,
            Mode = CipherMode.ECB,
            Padding = PaddingMode.PKCS7
        };

        using ICryptoTransform decryptor = tripleDES.CreateDecryptor();

        using MemoryStream ms = new(encryptedData);

        using CryptoStream cs = new(ms, decryptor, CryptoStreamMode.Read);

        using StreamReader sr = new(cs);

        return sr.ReadToEnd();
    }

    // Расшифровывает данные, зашифрованные RSA (закрытый ключ)
    public static byte[] DecryptRSA(byte[] data, RSAParameters privateKey)
    {
        using RSA rsa = RSA.Create();

        rsa.ImportParameters(privateKey);

        return rsa.Decrypt(data, RSAEncryptionPadding.OaepSHA256);
    }

    public static RSAParameters ParsePrivateKeyFromXml(string filePath)
    {
        try
        {
            XmlDocument doc = new();
            doc.Load(filePath);

            RSAParameters parameters = new()
            {
                // Извлечение параметров ключа.  Структура XML должна соответствовать
                Modulus = Convert.FromBase64String(doc.SelectSingleNode("/RSAKeyValue/Modulus").InnerText),
                Exponent = Convert.FromBase64String(doc.SelectSingleNode("/RSAKeyValue/Exponent").InnerText),
                P = Convert.FromBase64String(doc.SelectSingleNode("/RSAKeyValue/P").InnerText),
                Q = Convert.FromBase64String(doc.SelectSingleNode("/RSAKeyValue/Q").InnerText),
                DP = Convert.FromBase64String(doc.SelectSingleNode("/RSAKeyValue/DP").InnerText),
                DQ = Convert.FromBase64String(doc.SelectSingleNode("/RSAKeyValue/DQ").InnerText),
                InverseQ = Convert.FromBase64String(doc.SelectSingleNode("/RSAKeyValue/InverseQ").InnerText),
                D = Convert.FromBase64String(doc.SelectSingleNode("/RSAKeyValue/D").InnerText)
            };

            return parameters;
        }
        catch (FileNotFoundException)
        {
            throw new Exception($"Файл {filePath} не найден.");
        }
        catch (XmlException ex)
        {
            throw new Exception($"Ошибка парсинга XML: {ex.Message}");
        }
        catch (FormatException ex)
        {
            throw new Exception($"Ошибка преобразования Base64: {ex.Message}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Непредвиденная ошибка: {ex.Message}");
        }
    }

    #endregion


    #region Проверка подписи

    #endregion


    public class LettersMailRequest : MailRequest
    {
        public string Action { get; set; }
    }

    // получить письма на аккаунте в папке
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

    public class LetterById : LettersMailRequest
    {
        public int LetterId { get; set; }
    }

    // открыть конкретное письмо
    // _db.Database.ExecuteSqlRaw($"exec CheckUserFriends '{dateStart:yyyy-MM-dd}', '{dateEnd:yyyy-MM-dd}'")
    [HttpPost]
    public JsonResult GetLetter([FromBody] LetterById request)
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

            // берём сообщение
            var message = folder.GetMessage(request.LetterId);

            string decriptText = "";

            try
            {
                // получаем всех друзей текущего пользователя (читающего письмо)
                List<string> friends = _db.Database.SqlQuery<string>($"exec CheckUserFriends '{ExtractQuotedText(message.To.ToString())}'")
                                                   .ToList();

                // проверяем что письмо пришло от друга
                decriptText = friends.Any(f => f.Equals(ExtractQuotedText(message.From.ToString()))) ?
                     DecriptMailBody(message) : // расшифровываем текст сообщения
                     getMailText(message);      // иначе передаём просто текст сообщения
            }
            finally
            {
                folder.Close(); // Закрываем папку в блоке finally
            }

            // Отключение
            client.Disconnect(true);

            return new(new {
                Subject = message.Subject,
                From = ExtractQuotedText(message.From.ToString()),
                Date = $"{message.Date:D}, {message.Date:t}",
                TextBody = decriptText
            });
        }
        catch (Exception ex)
        {
            return new(ex.Message);
        }
    }

    #endregion

    #region Отправка писем

    #region Шифрование

    // шифрование тела письма
    public string EncriptMailBody(MailSendRequest request)
    {
        // находим открытый ключ для шифрования данных
        var record = _db.MailBoxesKeys.FirstOrDefault(r => r.UserFrom.Equals(request.Email) && 
                                                           r.UserTo.Equals(request.RecipientEmail)) 
            ?? throw new Exception("Ключ для шифрования не найден в базе");

        // Генерируем ключ для TripleDES
        byte[] tripleDESKey = GenerateTripleDESKey();

        // Шифруем сообщение с помощью TripleDES
        byte[] encryptedTripleDES = EncryptTripleDES(request.Body, tripleDESKey);

        // Парсинг открытого ключа шфирования
        RSAParameters publicKeyParams = ParseRsaPublicKey(record.EncriptOpenKey) ?? throw new Exception("Ошибка парсинга ключа");

        // Шифруем ключ 3des с помощью RSA
        byte[] encryptedTripleDESKey = EncryptRSA(tripleDESKey, publicKeyParams);

        // Добавляем длину encryptedTripleDESKey в начало
        byte[] lengthBytes = BitConverter.GetBytes(encryptedTripleDESKey.Length);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(lengthBytes); // Если Little Endian,  нужно изменить порядок байт

        // Объединяем массивы
        byte[] combinedData = lengthBytes.Concat(encryptedTripleDESKey).Concat(encryptedTripleDES).ToArray();

        // Преобразование в строку для отправки
        return Convert.ToBase64String(combinedData);
    }

    // Парсинг XML открытого ключа в RSAParameters
    public static RSAParameters? ParseRsaPublicKey(string xmlPublicKey)
    {
        try
        {
            XmlDocument xmlDoc = new();
            xmlDoc.LoadXml(xmlPublicKey);

            RSAParameters rsaParams = new()
            {
                Modulus = Convert.FromBase64String(xmlDoc.DocumentElement.SelectSingleNode("Modulus").InnerText),
                Exponent = Convert.FromBase64String(xmlDoc.DocumentElement.SelectSingleNode("Exponent").InnerText)
            };

            return rsaParams;
        }
        catch (Exception)
        {
            return null;
        }
    }


    // Генерирует случайный ключ для TripleDES
    public static byte[] GenerateTripleDESKey()
    {
        using TripleDESCryptoServiceProvider tripleDES = new();
        return tripleDES.Key;
    }

    // Шифрует текст с помощью TripleDES
    public static byte[] EncryptTripleDES(string text, byte[] key)
    {
        using TripleDESCryptoServiceProvider tripleDES = new() 
        { 
            Key = key, 
            Mode = CipherMode.ECB, 
            Padding = PaddingMode.PKCS7 
        };

        using ICryptoTransform encryptor = tripleDES.CreateEncryptor();

        using MemoryStream ms = new();

        using (CryptoStream cs = new(ms, encryptor, CryptoStreamMode.Write))
        {
            using StreamWriter sw = new(cs);
            sw.Write(text);
        }

        return ms.ToArray();
    }

    // Шифрует данные с помощью RSA (открытый ключ)
    public static byte[] EncryptRSA(byte[] data, RSAParameters publicKey)
    {
        using RSA rsa = RSA.Create();
        rsa.ImportParameters(publicKey);
        return rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
    }

    #endregion

    #region Подпись

    #endregion

    public class MailSendRequest
    {
        public string Email { get; set; }
        public string AppPassword { get; set; }
        public string RecipientEmail { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
    }

    // Отправка письма
    [HttpPost]
    public IActionResult SendMail([FromBody] MailSendRequest request)
    {
        try
        {
            string connect = request.Email.EndsWith("@yandex.ru") || request.Email.EndsWith("@ya.ru") ? "smtp.yandex.ru" : "smtp.mail.ru";

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(request.Email, request.Email));
            message.To.Add(new MailboxAddress(request.RecipientEmail, request.RecipientEmail));
            message.Subject = request.Subject;

            // шифрование сообщения
            message.Body = new TextPart("plain")
            {
                Text = EncriptMailBody(request)
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

            connect = request.Email.EndsWith("@yandex.ru") || request.Email.EndsWith("@ya.ru") ? "imap.yandex.ru" : "imap.mail.ru";

            // Подключение к IMAP (попытка синхронизации папки отправленные)
            using (var imapClient = new ImapClient())
            {
                // Подключение к IMAP-серверу
                imapClient.Connect(connect, 993, true);

                // Аутентификация
                imapClient.Authenticate(request.Email, request.AppPassword);

                // Открытие папки "Отправленные"
                var sentFolder = connect.Contains("yandex") ? imapClient.GetFolder("Sent") : imapClient.GetFolder("Отправленные");

                // открываем папку на редактирование
                sentFolder.Open(FolderAccess.ReadWrite);

                // Сохранение сообщения
                sentFolder.Append(message, MessageFlags.Seen);

                imapClient.Disconnect(true);
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
