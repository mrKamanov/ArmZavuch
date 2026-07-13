using System.Globalization;
using System.Text;
using ArmZavuch.Services.Excel;
using ExcelDataReader;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var path = @"c:\golpom\АРМ\РАСПИСАНИЕ 2025.xls";
using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
using var reader = ExcelReaderFactory.CreateReader(stream);
var profiles = new Dictionary<string, string>();
for (var si = 0; si < 6 && reader.Read(); si++) { }
do
{
    var rows = new List<string?[]>();
    while (reader.Read())
    {
        var row = new string?[reader.FieldCount];
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var value = reader.GetValue(i);
            row[i] = value switch
            {
                null => null,
                DateTime dt => dt.ToString("HH:mm"),
                double d when d == Math.Floor(d) && d < 10000 => ((int)d).ToString(CultureInfo.InvariantCulture),
                _ => value.ToString()?.Trim()
            };
        }
        rows.Add(row);
    }

    if (rows.Count < 2) continue;
    var senior = rows[1].ElementAtOrDefault(1)?.Contains("кабинет", StringComparison.OrdinalIgnoreCase) == true
                 && rows[1].ElementAtOrDefault(2)?.Contains("предмет", StringComparison.OrdinalIgnoreCase) == true;
    if (LegacyBellExtractor.ExtractMondaySchedule(rows, senior) is not { } s) continue;
    profiles.TryAdd(s.Signature, s.TemplateName + " | entries=" + s.Entries.Count);
} while (reader.NextResult());

foreach (var p in profiles.Values)
    Console.WriteLine(p);
