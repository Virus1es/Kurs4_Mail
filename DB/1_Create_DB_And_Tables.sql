set noexec off
go

use master 
go

-- при отсутствии БД создать ее
if db_id('Mail_DB') is null
begin
	create database Mail_DB on (
	    name = Mail_DB, 
		filename='C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA\Mail_DB.mdf'
	)
	log on (
		name = Mail_DB_log, 
		filename='C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA\Mail_DB_log.ldf'
	);
end
else begin
print 'БД уже есть на сервере'
end;
go

use Mail_DB;
go

-- удаление существующих вариантов таблиц
begin transaction;
print '';
print 'Удаление существующих таблиц:';
print '';

-- удалить старый вариант таблицы MailBoxesKeys(Ключи для почтовых ящиков)
if OBJECT_ID('MailBoxesKeys') is not null begin
	drop table MailBoxesKeys;
	print 'Удалена таблица MailBoxesKeys';
end;

-- удалить старый вариант таблицы FriendRequests(Запросы дружбы)
if OBJECT_ID('FriendRequests') is not null begin
	drop table FriendRequests;
	print 'Удалена таблица FriendRequests';
end;


-- Если осталась хотя бы одна таблица, откатить
-- транзакцию
if object_id('MailBoxesKeys') is not null or
   object_id('FriendRequests') is not null
begin  
   rollback transaction
   
   print ''
   print 'Не все таблицы удалены'
   print ''

   set noexec on;
end else begin
   commit transaction;
   
   print ''
   print 'Удаление таблиц завершено'
   print ''
end;


begin transaction


-- создание таблицы Ключи для почтовых ящиков
create table MailBoxesKeys ( 
     Id             int           not null identity(1, 1) constraint MailBoxes_PK primary key (Id), -- для автоинкремента
	 UserFrom       nvarchar(255) not null,                 -- отправитель (берёт открытый ключ для шифрования ключа 3des, чтоб получатель расшифровал его своим закрытым)
	 UserTo         nvarchar(255) not null,                 -- получатель (пишет свой открытый ключ в следующую колонку)
	 EncriptOpenKey nvarchar(255) not null,                 -- открытый ключ шифрования данных
);
go

-- создание таблицы Не просмотренные запросы дружбы
create table FriendRequests ( 
     Id             int           not null identity(1, 1) constraint FriendRequests_PK primary key (Id), -- для автоинкремента
	 UserFrom       nvarchar(255) not null,                 -- отправитель
	 UserTo         nvarchar(255) not null,                 -- получатель 
);
go


-- Если не создана хотя бы одна таблица, откатить
-- транзакцию
if object_id('MailBoxesKeys') is null or
   object_id('FriendRequests') is null
begin  
   rollback transaction
   print '';
   print 'Не все таблицы созданы';
   print '';
   set noexec on;
end else
    commit transaction;

	
print '';
print 'Создание базы данных и таблиц выполнено';
print '';