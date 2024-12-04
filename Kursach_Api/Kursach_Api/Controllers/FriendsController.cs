using Kursach_Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Kursach_Api.Controllers;

[Route("[controller]/{action}")]
[ApiController]
public class FriendsController(MailDbContext context) : Controller
{
    // соединение с БД
    // ссылка на базу данных
    private MailDbContext _db = context;

    // папка с пользователями
    private readonly string _usersFolder = "users";

    // попробовать найти запросы на дружбу для пользователя
    [HttpPost]
    public async Task<JsonResult> FindRequestForUser([FromBody] string user)
    {
        // проверяем есть ли для пользователя запросы
        if (!_db.FriendRequests.Any(r => r.UserTo.Equals(user)))
            return Json("Запросов нет");

        // берём первый запрос на дружбу
        var request = await _db.FriendRequests.FirstAsync(r => r.UserTo.Equals(user));

        return Json(request);
    }

    public class AnswerRequest
    {
        public int RequestId { get; set; }
        public string EmailFrom { get; set; }
        public string EmailTo { get; set; }
        public bool Answer { get; set; }
    }

    // обработать ответ на запрос дружбы
    [HttpDelete]
    public async Task<IActionResult> AnswerFriendRequest([FromBody] AnswerRequest answer)
    {
        try
        {
            // если пользователь принял запрос дружбы начинаем обмен ключами
            if (answer.Answer) await CreateRsaForUsers(answer.EmailFrom, answer.EmailTo);

            // найти нужную книгу
            var friendRequest = _db.FriendRequests.First(b => b.Id == answer.RequestId);

            // если нашли удаляем
            if (friendRequest != null) _db.FriendRequests.Remove(friendRequest);

            // сохраняем изменения
            await _db.SaveChangesAsync();

            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    private async Task CreateRsaForUsers(string emailFrom, string emailTo)
    {
        // сохранить ключи для 1 пользователя
        using (RSACryptoServiceProvider rsa = new())
        {
            // создание записи в бд (сохранение публичного ключа)
            // ключ сохраняет UserTo (UserFrom будет брать этот ключ чтоб шифровать ключ 3des)
            MailBoxesKey keyRecord = new()
            {
                UserFrom = emailFrom,
                UserTo = emailTo,
                EncriptOpenKey = rsa.ToXmlString(false)
            };

            // добавление новой записи в БД
            _db.MailBoxesKeys.Add(keyRecord);

            // если до этого у пользователя не было папки с ключами создаём её
            if (!Directory.Exists($"{_usersFolder}/{emailTo}")) Directory.CreateDirectory($"{_usersFolder}/{emailTo}");

            if (!Directory.Exists($"{_usersFolder}/{emailTo}/keys")) Directory.CreateDirectory($"{_usersFolder}/{emailTo}/keys");

            // сохранение приватного ключа
            // UserTo будет брать этот ключ чтоб расшифрованть переданное сообщение
            System.IO.File.WriteAllText($"{_usersFolder}/{emailTo}/keys/for {emailFrom}.xml", rsa.ToXmlString(true));
        }


        // сохранить ключ для 2 пользователя
        using (RSACryptoServiceProvider rsa = new())
        {
            // создание записи в бд (сохранение публичного ключа)
            // ключ сохраняет UserTo (UserFrom будет брать этот ключ чтоб шифровать ключ 3des)
            MailBoxesKey keyRecord = new()
            {
                UserFrom = emailTo,
                UserTo = emailFrom,
                EncriptOpenKey = rsa.ToXmlString(false)
            };

            // добавление новой записи в БД
            _db.MailBoxesKeys.Add(keyRecord);

            // если до этого у пользователя не было папки с ключами создаём её
            if (!Directory.Exists($"{_usersFolder}/{emailFrom}")) Directory.CreateDirectory($"{_usersFolder}/{emailFrom}");

            if (!Directory.Exists($"{_usersFolder}/{emailFrom}/keys")) Directory.CreateDirectory($"{_usersFolder}/{emailFrom}/keys");

            // сохранение приватного ключа
            // UserTo будет брать этот ключ чтоб расшифрованть переданное сообщение
            System.IO.File.WriteAllText($"{_usersFolder}/{emailFrom}/keys/for {emailTo}.xml", rsa.ToXmlString(true));
        }

        await _db.SaveChangesAsync();
    }

    public class MailFriendRequest
    {
        public string EmailFrom { get; set; }
        public string EmailTo { get; set; }
    }

    [HttpPut]
    public async Task<IActionResult> SendFriendRequest([FromBody] MailFriendRequest request)
    {
        if (request == null)
            return BadRequest("Invalid data.");

        try
        {
            // проверки на повторы
            if (request.EmailTo.Equals(request.EmailFrom))
                return BadRequest("Вы не можете отправить запрос себе");

            if (_db.MailBoxesKeys.Any(c => c.UserFrom.Equals(request.EmailFrom) && c.UserTo.Equals(request.EmailTo)))
                return BadRequest("Вы уже дружите с этим пользователем");

            if (_db.FriendRequests.Any(f => f.UserTo.Equals(request.EmailTo) && f.UserFrom.Equals(request.EmailFrom)))
                return BadRequest("Запрос уже отослан");

            // создаём запрос 
            FriendRequest friendRequest = new()
            {
                UserFrom = request.EmailFrom,
                UserTo = request.EmailTo
            };

            // сохраняем изменения
            _db.FriendRequests.Add(friendRequest);

            await _db.SaveChangesAsync();

            return Ok("Запрос отправлен");
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
}
