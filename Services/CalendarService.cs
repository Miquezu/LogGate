using System.Net.Http;

namespace LogGate.Services;

/// <summary>
/// Реализация сервиса производственного календаря (isdayoff.ru) для Республики Беларусь.
/// </summary>
public class CalendarService(ILogger<CalendarService>? logger = null) : ICalendarService
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    public async Task<List<DateTime>> GetPreHolidaysAsync(int year, CancellationToken ct = default)
    {
        List<DateTime> preHolidays = [];
        string url = $"https://isdayoff.ru/api/getdata?year={year}&pre=1&cc=by";

        try
        {
            logger?.LogInformation("Запрос предпраздничных дней на {Year} год из внешнего календаря...", year);
            string response = await _httpClient.GetStringAsync(url, ct);

            var startDate = new DateTime(year, 1, 1);
            for (int i = 0; i < response.Length; i++)
            {
                // '2' — сокращенный предпраздничный рабочий день
                if (response[i] == '2')
                {
                    preHolidays.Add(startDate.AddDays(i));
                }
            }

            logger?.LogInformation("Успешно получено {Count} предпраздничных дней на {Year} год.", preHolidays.Count, year);
        }
        catch (Exception ex)
        {
            // При отсутствии интернета или недоступности API приложение продолжает работу
            logger?.LogWarning(ex, "Не удалось загрузить производственный календарь на {Year} год. Будет использован стандартный график.", year);
        }

        return preHolidays;
    }
}
}