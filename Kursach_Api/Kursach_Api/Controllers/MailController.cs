using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using MailKit.Security;
using MailKit.Net.Imap;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Xml;
using System.Linq;
using Kursach_Api.Models;
using MailKit.Search;

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
    
    // дешифровка файлов прикреплённых к письму
    private byte[] DecryptFile3DES(byte[] encryptedData, byte[] encryptedKeyAndIV, RSAParameters rsaPrivateKey)
    {
        // Расшифровка ключа и IV с помощью RSA
        byte[] keyAndIV = DecryptRSA(encryptedKeyAndIV, rsaPrivateKey);

        // Извлечение ключа и IV для 3DES
        byte[] key = keyAndIV.Take(24).ToArray(); // Первые 24 байта — ключ
        byte[] iv = keyAndIV.Skip(24).Take(8).ToArray(); // Последние 8 байт — IV

        // Расшифровка данных с использованием 3DES
        using MemoryStream decryptedStream = new();
        using (TripleDESCryptoServiceProvider tripleDES = new())
        {
            tripleDES.Key = key;
            tripleDES.IV = iv;
            tripleDES.Mode = CipherMode.CBC;
            tripleDES.Padding = PaddingMode.PKCS7;

            using CryptoStream cryptoStream = new(decryptedStream, tripleDES.CreateDecryptor(), CryptoStreamMode.Write);
            cryptoStream.Write(encryptedData, 0, encryptedData.Length);
        }

        return decryptedStream.ToArray(); // Возвращаем расшифрованные данные
    }

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

        // Читаем данные до первой строки с '\n'
        string firstLine = combinedDataStringFromMail.Split(['\n'], 2)[0];

        // Преобразование обратно в байты
        byte[] combinedData = Convert.FromBase64String(firstLine);

        // Разделяем данные
        // Длинна подписи
        int signedMessageLength = BitConverter.ToInt32(combinedData, 0); 

        // Подпись
        byte[] signedMessage = combinedData.AsSpan(4, signedMessageLength).ToArray();

        // Длинна публичного ключа DSA
        int publicKeyXmlLength = BitConverter.ToInt32(combinedData, 4 + signedMessageLength);

        // Публичный ключ DSA
        byte[] publicKeyXml = combinedData.AsSpan(8 + signedMessageLength, publicKeyXmlLength).ToArray();

        // Длинна зашифрованного ключа 3DES
        int encryptedTripleDESKeyLength = BitConverter.ToInt32(combinedData, 8 + signedMessageLength + publicKeyXmlLength);

        // Зашифрованный ключ 3DES
        byte[] encryptedTripleDESKey = combinedData.AsSpan(12 + signedMessageLength + publicKeyXmlLength, encryptedTripleDESKeyLength).ToArray();

        // Зашифрованное сообщение (оставшиеся данные)
        byte[] encryptedTripleDES = combinedData.AsSpan(12 + signedMessageLength + publicKeyXmlLength + encryptedTripleDESKeyLength).ToArray();

        // Парсинг открытого ключа DSA
        DSAParameters publicKeyDsa = ParseDsaPublicKeyFromXml(publicKeyXml);

        // Дешифруем ключ TripleDES с помощью RSA
        byte[] decryptedTripleDESKey = DecryptRSA(encryptedTripleDESKey, privateKey);

        // Дешифруем сообщение с помощью TripleDES и расшифрованного ключа
        string decryptedMessage = DecryptTripleDES(encryptedTripleDES, decryptedTripleDESKey);

        // Проверяем подпись
        bool isValidSignature = VerifySignature(decryptedMessage, signedMessage, publicKeyDsa);
        
        // если подпись оказалась не действиетльной кидаем исключение (данные повреждены показать их нельзя)
        if (!isValidSignature) throw new Exception("Подпись недействительна!");

        return decryptedMessage;
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

    // Проверка подписи с использованием DSA
    private bool VerifySignature(string message, byte[] signature, DSAParameters publicKey)
    {
        using DSACryptoServiceProvider dsa = new();

        dsa.ImportParameters(publicKey);

        byte[] data = Encoding.UTF8.GetBytes(message);

        using SHA1 sha1 = SHA1.Create();

        byte[] hash = sha1.ComputeHash(data);

        return dsa.VerifyHash(hash, "SHA1", signature);
    }

    // Парсинг открытого ключа DSA из XML
    private DSAParameters ParseDsaPublicKeyFromXml(byte[] xmlPublicKey)
    {
        string xmlString = Encoding.UTF8.GetString(xmlPublicKey);
        XmlDocument doc = new XmlDocument();
        doc.LoadXml(xmlString);

        DSAParameters parameters = new DSAParameters();
        parameters.P = Convert.FromBase64String(doc.SelectSingleNode("/DSAKeyValue/P").InnerText);
        parameters.Q = Convert.FromBase64String(doc.SelectSingleNode("/DSAKeyValue/Q").InnerText);
        parameters.G = Convert.FromBase64String(doc.SelectSingleNode("/DSAKeyValue/G").InnerText);
        parameters.Y = Convert.FromBase64String(doc.SelectSingleNode("/DSAKeyValue/Y").InnerText);

        return parameters;
    }

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
            folder.Open(FolderAccess.ReadOnly);

            // берём сообщение
            var message = folder.GetMessage(request.LetterId);

            string decriptText = "";
            var decryptedFiles = new List<string>(); // Список для сохранённых файлов

            try
            {
                string mailTo = ExtractQuotedText(message.To.ToString());

                // получаем всех друзей текущего пользователя (читающего письмо)
                List<string> friends = _db.Database.SqlQuery<string>($"exec CheckUserFriends {mailTo}")
                                                   .ToList();

                // достаём сообщение, для проверки его на пустоту,
                // и если сообщение пришло не от дурга или оно пустое, то это же мы и отдаём 
                string mailBody = getMailText(message).Replace("\n", "").Replace("\r", "").Replace("\t", "");

                bool senderIsFriend = friends.Any(f => f.Equals(ExtractQuotedText(message.From.ToString())));

                // проверяем, что письмо пришло от друга
                // и проверяем чтоб оно было не пустым
                decriptText = senderIsFriend && !string.IsNullOrEmpty(mailBody) ?
                     DecriptMailBody(message) : // расшифровываем текст сообщения
                     mailBody;                  // иначе передаём просто текст сообщения

                // Обработка вложений
                var multipart = message.Body as Multipart;
                if (multipart != null && senderIsFriend)
                {
                    string filePath = $"{_usersFolder}/{ExtractQuotedText(message.To.ToString())}/keys/for {ExtractQuotedText(message.From.ToString())}.xml";

                    // находим закрытый ключ получателя для дешифрования данных от конкретного отправителя
                    if (!System.IO.File.Exists(filePath))
                        throw new Exception("Файл закрытого ключа не найден");

                    foreach (var part in multipart)
                    {
                        if (part is MimePart attachment && attachment.ContentType.MimeType == "application/octet-stream")
                        {
                            // Это зашифрованный файл
                            using var memoryStream = new MemoryStream();
                            attachment.Content.DecodeTo(memoryStream);
                            byte[] encryptedFileData = memoryStream.ToArray();

                            // Найти текстовую часть с ключом и IV для этого файла
                            var keyPart = multipart.OfType<TextPart>().FirstOrDefault(p => p.Text.StartsWith(ComputeSHA256Hash(attachment.FileName) + ":")) ?? 
                                throw new Exception("Не удалось найти ключ для файла: " + attachment.FileName);

                            // Извлечь зашифрованный ключ и IV
                            string base64KeyAndIV = keyPart.Text.Split(':')[1];
                            byte[] encryptedKeyAndIV = Convert.FromBase64String(base64KeyAndIV);

                            // получить закрытый ключ
                            RSAParameters privateKeyParams = ParsePrivateKeyFromXml(filePath);
                            
                            // Расшифровка файла
                            byte[] decryptedFileData = DecryptFile3DES(encryptedFileData, encryptedKeyAndIV, privateKeyParams);

                            // Сохранение расшифрованного файла
                            string outputPath = Path.Combine($"DecryptedFiles\\{request.Email}\\{request.LetterId}", attachment.FileName);

                            // Убеждаемся, что папка существует
                            Directory.CreateDirectory($"DecryptedFiles\\{request.Email}\\{request.LetterId}");

                            // сохраняем дешифрованный файл чтоб пользователь мог его скачать
                            System.IO.File.WriteAllBytes(outputPath, decryptedFileData);

                            // Добавляем путь файла в список
                            decryptedFiles.Add(outputPath); 
                        }
                    }
                }
            }
            finally
            {
                folder.Close(); // Закрываем папку в блоке finally
            }

            // Отключение
            client.Disconnect(true);

            return new(new
            {
                Subject = message.Subject,
                From = ExtractQuotedText(message.From.ToString()),
                Date = $"{message.Date:D}, {message.Date:t}",
                TextBody = decriptText,
                Attachments = decryptedFiles // Возвращаем пути к расшифрованным файлам
            });
        }
        catch (Exception ex)
        {
            return new($"Ошибка: {ex.Message}");
        }
    }


    // скачивание файла прикреплённого к письму
    [HttpPost]
    public IActionResult DownloadFile([FromBody] string filePath)
    {
        try
        {
            if (!System.IO.File.Exists(filePath)) return NotFound(); // Файл не найден

            string fileName = Path.GetFileName(filePath);
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

            return File(fileStream, "application/octet-stream", fileName); // Возвращает файл
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Ошибка сервера: {ex.Message}"); // Обработка ошибок
        }

    }

    #endregion

    #region Отправка писем

    #region Шифрование

    // шифирование файлов
    private Tuple<byte[], byte[]> EncryptFile3DES(byte[] fileData, RSAParameters rsaPublicKey)
    {
        // Генерация ключа и IV для 3DES
        byte[] key = GenerateTripleDESKey(); // 24 байта для 3DES
        byte[] iv = GenerateIV(); // 8 байт

        // Шифрование файла с помощью 3DES
        using MemoryStream encryptedStream = new();

        using (TripleDESCryptoServiceProvider tripleDES = new())
        {
            tripleDES.Key = key;
            tripleDES.IV = iv;
            tripleDES.Mode = CipherMode.CBC;
            tripleDES.Padding = PaddingMode.PKCS7;

            using CryptoStream cryptoStream = new(encryptedStream, tripleDES.CreateEncryptor(), CryptoStreamMode.Write);
            cryptoStream.Write(fileData, 0, fileData.Length);
        }

        // Шифрование ключа 3DES и IV с помощью RSA
        byte[] keyAndIV = key.Concat(iv).ToArray();
        byte[] encrypted3DESKeyAndIV = EncryptRSA(keyAndIV, rsaPublicKey);

        return Tuple.Create(encryptedStream.ToArray(), encrypted3DESKeyAndIV);
    }

    // шифрование тела письма
    public string EncriptMailBody(MailSendRequest request)
    {
        // находим открытый ключ для шифрования данных
        var record = _db.MailBoxesKeys.FirstOrDefault(r => r.UserFrom.Equals(request.Email) && 
                                                           r.UserTo.Equals(request.RecipientEmail)) 
            ?? throw new Exception("Ключ для шифрования не найден в базе");

        // Генерируем ключ для TripleDES
        byte[] tripleDESKey = GenerateTripleDESKey();

        // создание пары ключей DSA для подписи
        DSAParameters dsaParams = GenerateDsaKeyPair();

        // Экспорт открытого ключа в XML
        byte[] publicKeyXml = ExportDsaPublicKey(dsaParams); 

        // подписание сообщения
        byte[] signedMessage = SignMessage(request.Body, dsaParams);

        // Шифруем сообщение с помощью TripleDES
        byte[] encryptedTripleDES = EncryptTripleDES(request.Body, tripleDESKey);

        // Парсинг открытого ключа шфирования
        RSAParameters publicKeyParams = ParseRsaPublicKey(record.EncriptOpenKey) ?? throw new Exception("Ошибка парсинга ключа");

        // Шифруем ключ 3des с помощью RSA
        byte[] encryptedTripleDESKey = EncryptRSA(tripleDESKey, publicKeyParams);

        // Подпись
        byte[] signedMessageLength = BitConverter.GetBytes(signedMessage.Length);

        // Открытый ключ подписи
        byte[] publicKeyXmlLength = BitConverter.GetBytes(publicKeyXml.Length);

        // Ключ 3DES
        byte[] encryptedTripleDESKeyLength = BitConverter.GetBytes(encryptedTripleDESKey.Length);

        byte[] combinedData = signedMessageLength.Concat(signedMessage)
            .Concat(publicKeyXmlLength).Concat(publicKeyXml)
            .Concat(encryptedTripleDESKeyLength).Concat(encryptedTripleDESKey)
            .Concat(encryptedTripleDES).ToArray();

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

    // Генерация 8-байтного вектора инициализации (IV)
    private byte[] GenerateIV()
    {
        byte[] iv = new byte[8];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(iv);
        }
        return iv;
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

    //Подпись сообщения с использованием DSA
    private byte[] SignMessage(string message, DSAParameters privateKey)
    {
        using DSACryptoServiceProvider dsa = new();

        dsa.ImportParameters(privateKey);

        byte[] data = Encoding.UTF8.GetBytes(message);

        byte[] hash = SHA1.HashData(data);

        return dsa.SignHash(hash, "SHA1");
    }

    //Генерирует пару ключей DSA
    private DSAParameters GenerateDsaKeyPair()
    {
        // указываем длину ключа (2048 бит)
        using var dsa = new DSACryptoServiceProvider();

        // экспортируем параметры (включая закрытый ключ)
        return dsa.ExportParameters(true);
    }

    // Экспортируем открытый ключ DSA в XML
    private byte[] ExportDsaPublicKey(DSAParameters parameters)
    {
        string xml = $"<DSAKeyValue><P>{Convert.ToBase64String(parameters.P)}</P><Q>{Convert.ToBase64String(parameters.Q)}</Q><G>{Convert.ToBase64String(parameters.G)}</G><Y>{Convert.ToBase64String(parameters.Y)}</Y></DSAKeyValue>";
        return Encoding.UTF8.GetBytes(xml);
    }

    #endregion

    public class MailSendRequest
    {
        public string Email { get; set; }
        public string AppPassword { get; set; }
        public string RecipientEmail { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; } = string.Empty;
        public IFormFile[] Files { get; set; } = Array.Empty<IFormFile>();
    }

    // Отправка письма
    [HttpPost]
    public IActionResult SendMail([FromForm] MailSendRequest request)
    {
        try
        {
            string connect = request.Email.EndsWith("@yandex.ru") || request.Email.EndsWith("@ya.ru") ? "smtp.yandex.ru" : "smtp.mail.ru";

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(request.Email, request.Email));
            message.To.Add(new MailboxAddress(request.RecipientEmail, request.RecipientEmail));
            message.Subject = request.Subject;
            
            // Создаем multipart/mixed сообщение для поддержки текста и вложений
            var multipart = new Multipart("mixed");
            message.Body = multipart;

            // Добавляем текстовую часть (зашифрованная)
            // шифруем только если есть сообщение
            var textPart = new TextPart("plain") { Text = !string.IsNullOrEmpty(request.Body) ? EncriptMailBody(request) : request.Body };
            multipart.Add(textPart);

            // Добавляем файлы в качестве вложений, если они есть
            if (request.Files != null)
            {
                var record = _db.MailBoxesKeys.FirstOrDefault(r => r.UserFrom.Equals(request.Email) && r.UserTo.Equals(request.RecipientEmail)) ?? 
                    throw new Exception("Ключ для шифрования не найден в базе");

                RSAParameters publicKeyParams = ParseRsaPublicKey(record.EncriptOpenKey) ?? throw new Exception("Ошибка парсинга открытого ключа");

                foreach (var file in request.Files)
                {
                    using var stream = file.OpenReadStream();
                    using MemoryStream ms = new();
                    stream.CopyTo(ms);
                    var encryptedFileWithKey = EncryptFile3DES(ms.ToArray(), publicKeyParams);


                    var attachment = new MimePart("application/octet-stream")
                    {
                        Content = new MimeContent(new MemoryStream(encryptedFileWithKey.Item1)),
                        ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) { FileName = file.FileName },
                        ContentTransferEncoding = ContentEncoding.Base64
                    };

                    string fileHash = ComputeSHA256Hash(file.FileName); // используем SHA256

                    var encryptedKeyAttachment = new TextPart("plain")
                    {
                        Text = $"{fileHash}:{Convert.ToBase64String(encryptedFileWithKey.Item2)}" // хэш + ключ + инит вектор
                    };

                    multipart.Add(attachment);
                    multipart.Add(encryptedKeyAttachment);
                }
            }

            // собственно отправка письма
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

            // меняем переменную на imap протокол для синхронизации папки "входящие"
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

    private string ComputeSHA256Hash(string input)
    {
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant(); // строковое представление хеша
    }

    #endregion

    public class DeleteLetterRequest : LettersMailRequest
    {
        public int LetterId { get; set; } // индекс письма в массиве
    }

    // отправка письма в корзину
    [HttpPost]
    public IActionResult MoveToTrash([FromBody] DeleteLetterRequest request)
    {
        try
        {
            using (var client = new ImapClient())
            {

                // Определяем IMAP-сервер
                string connect = request.Email.EndsWith("@yandex.ru") || request.Email.EndsWith("@ya.ru")
                    ? "imap.yandex.ru"
                    : "imap.mail.ru";

                // Подключение к IMAP-серверу
                client.Connect(connect, 993, SecureSocketOptions.SslOnConnect);

                // Аутентификация
                client.Authenticate(request.Email, request.AppPassword);

                // Используем словарь для упрощения доступа к папкам
                var folders = new Dictionary<string, Func<IMailFolder>>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Input",  () => client.Inbox },
                    { "Output", () => connect.Contains("yandex") ? client.GetFolder("Sent") : client.GetFolder("Отправленные") },
                    { "Drafts", () => connect.Contains("yandex") ? client.GetFolder("Drafts") : client.GetFolder("Черновики") },
                    { "Trash",  () => connect.Contains("yandex") ? client.GetFolder("Trash") : client.GetFolder("Корзина") },
                };

                // Проверяем, что папка существует в словаре
                if (!folders.TryGetValue(request.Action, out var getSourceFolder))
                    throw new NotImplementedException($"Action '{request.Action}' not supported.");

                // Получаем исходную папку (из которой будем перемещать письмо)
                var sourceFolder = getSourceFolder();
                sourceFolder.Open(FolderAccess.ReadWrite);

                // Получаем список всех уникальных идентификаторов сообщений
                var query = SearchQuery.All;
                var uids = sourceFolder.Search(query);

                // Проверяем, что индекс сообщения валиден
                if (request.LetterId < 0 || request.LetterId >= uids.Count)
                    throw new ArgumentOutOfRangeException(nameof(request.LetterId), "Индекс сообщения вне диапазона.");

                // Получаем уникальный идентификатор сообщения
                var messageId = uids[request.LetterId];

                // Получаем папку "Корзина"
                var trashFolder = folders["Trash"]();

                try
                {
                    // Перемещаем сообщение в папку "Корзина"
                    sourceFolder.MoveTo(messageId, trashFolder);
                }
                finally
                {
                    // Закрываем папки
                    sourceFolder.Close();
                }

                // Отключение от сервера
                client.Disconnect(true);
            }

            return Ok(new { Message = "Письмо успешно перемещено в корзину." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Ошибка сервера: {ex.Message}");
        }
    }
}

