using static ArmZavuch.Services.Help.InstructionArticleBuilder;

namespace ArmZavuch.Services.Help;

/// <summary>Разделы верхнего уровня и быстрые маршруты справки.</summary>
internal static class InstructionArticlesRoutes
{
    public static IReadOnlyList<InstructionQuickRoute> QuickRoutes { get; } =
    [
        new() { Title = "Первый запуск", Hint = "С нуля, по шагам", ArticleId = "start.roadmap" },
        new() { Title = "Учитель заболел", Hint = "Замены утром", ArticleId = "disp.quick" },
        new() { Title = "Правка на сегодня", Hint = "Не трогая всю неделю", ArticleId = "day.when" },
        new() { Title = "Собрать расписание", Hint = "Сетка и проверка", ArticleId = "con.grid" },
        new() { Title = "Данные с прошлого года", Hint = "Архив .armzavuch", ArticleId = "dir.settings-transfer" },
        new() { Title = "Что-то не работает", Hint = "Частые проблемы", ArticleId = "help.common" }
    ];

    public static IReadOnlyList<InstructionArticle> Groups { get; } =
    [
        Group("start", "С чего начать", 10),
        Group("glossary", "Словарик", 20),
        Group("dir", "Справочники", 30),
        Group("con", "Конструктор", 40),
        Group("disp", "Диспетчерская", 50),
        Group("day", "Конструктор дня", 60),
        Group("overview", "Сводка", 70),
        Group("rooms", "Кабинеты", 80),
        Group("help", "Если что-то не так", 90),
        Group("extra", "Ещё полезное", 100)
    ];

    public static IReadOnlyList<InstructionArticle> All => [];
}
