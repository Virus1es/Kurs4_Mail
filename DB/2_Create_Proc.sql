create or alter proc CheckUserFriends
	@selectUser nvarchar(255)
as
	select UserTo
	from MailBoxesKeys
	where UserFrom = @selectUser
	group by UserTo;
go

exec CheckUserFriends 'V1ru5es@yandex.ru';