using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace LogGate.Services
{
    public class CalendarService
    {
        private static readonly HttpClient _httpClient = new();

        // Получаем список предпраздничных (сокращенных) дней для РБ
        public async Task<List<DateTime>> GetPreHolidaysAsync(int year)
        {
            var preHolidays = new List<DateTime>();
            string url = $"https://isdayoff.ru/api/getdata?year={year}&pre=1&cc=by";

            try
            {
                string response = await _httpClient.GetStringAsync(url);

                var startDate = new DateTime(year, 1, 1);
                for (int i = 0; i < response.Length; i++)
                {
                    // Сервис возвращает '2' для сокращенных рабочих дней
                    if (response[i] == '2')
                        preHolidays.Add(startDate.AddDays(i));
                }
            }
            catch (Exception)
            {
                // В случае проблем с интернетом или недоступности API,
                // приложение не должно падать. Оно просто будет считать
                // все дни обычными (полными) рабочими днями.
            }

            return preHolidays;
        }
    }
}