namespace LogGate.Interfaces;

/// <summary>
/// Сервис получения производственного календаря и предпраздничных дней.
/// </summary>
public interface ICalendarService
{
    /// <summary>
    /// Получает список предпраздничных (сокращенных) рабочих дней за указанный год.
    /// </summary>
    Task<List<DateTime>> GetPreHolidaysAsync(int year, CancellationToken ct = default);
}

