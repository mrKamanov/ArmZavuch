using ClosedXML.Excel;

namespace ArmZavuch.Services.Excel;

/// <summary>Генерация шаблона импорта Excel (ТЗ §7.1).</summary>
public sealed class ExcelTemplateService
{
    public void SaveTemplate(string filePath)
    {
        using var workbook = new XLWorkbook();
        AddSheet(workbook, "Здания", ["Название", "Цвет_HEX"]);
        AddSheet(workbook, "Переходы", ["Здание_от", "Здание_до", "Минуты"]);
        AddSheet(workbook, "Кабинеты", ["Номер", "Здание", "Вместимость", "Специфика", "Закреплённый_учитель"]);
        AddSheet(workbook, "Предметы", ["Название", "Балл_Сивкова"]);
        AddSheet(workbook, "Классы", ["Параллель", "Буква", "Смена", "Учеников", "Коррекционный", "Здание"]);
        AddSheet(workbook, "Учителя", [
            "ФИО", "Тип", "Должность", "Макс_нагрузка", "Основной_профиль",
            "Смежный_профиль", "Классное_руководство", "Телефон", "Контакт_URL", "Контакт_заметка"
        ]);
        AddSheet(workbook, "Нагрузка", ["Класс", "Предмет", "Часов_в_неделю", "Подгруппы", "Неделя"]);
        AddSheet(workbook, "Звонки", ["Шаблон", "Параллель_с", "Параллель_по", "Тип", "Номер_урока", "Начало", "Конец", "Смена"]);
        workbook.SaveAs(filePath);
    }

    private static void AddSheet(XLWorkbook workbook, string name, string[] headers)
    {
        var sheet = workbook.Worksheets.Add(name);
        for (var i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Row(1).Style.Font.Bold = true;
    }
}
