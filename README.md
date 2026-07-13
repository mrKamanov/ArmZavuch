# Расписание.Про

Локальное Windows-приложение для завуча: расписание, замены, логистика между зданиями.

## Требования

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Запуск

```powershell
dotnet restore
dotnet build
dotnet run --project src\ArmZavuch\ArmZavuch.csproj
```

## Структура проекта

```
src/ArmZavuch/
  Data/           — SQLite, миграции
  Services/       — сохранение, recovery, (далее: скоринг, экспорт)
  ViewModels/     — MVVM
  Views/          — экраны модулей
  Resources/      — тема UI
```

## База данных

Файл: `%LocalAppData%\ArmZavuch\school.db`  
Черновик восстановления: `%LocalAppData%\ArmZavuch\recovery.db`