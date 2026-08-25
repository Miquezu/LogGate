using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace LogGate.Services
{
    public class AiAnalyzerService
    {
        private readonly ChatClient _chatClient;

        public AiAnalyzerService()
        {
            string? apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY", EnvironmentVariableTarget.User);

            if (string.IsNullOrEmpty(apiKey))
                throw new Exception("Не найден API-ключ. Проверьте переменные среды Windows.");

            var options = new OpenAIClientOptions
            {
                Endpoint = new Uri("https://openrouter.ai/api/v1")
            };

            _chatClient = new ChatClient(
                "openrouter/auto-beta",
                new ApiKeyCredential(apiKey),
                options);
        }

        public async Task<string> AnalyzeDataAsync(string textData, CancellationToken cancellationToken = default)
        {
            var prompt = $"""
            Ты — эксперт по корпоративной безопасности и HR-аналитике.
            Твоя задача — проанализировать выгрузку логов со СКУД (система контроля и управления доступом) предприятия и составить краткий, емкий и полезный управленческий отчет.

            На вход тебе будет подан список событий. Каждая запись содержит: дату, время, направление (вход/выход), ФИО, должность, отдел, температуру тела, результат алкотеста и примечание.

            Выполни анализ данных по следующим ключевым направлениям (используй лучшие корпоративные практики):

            1. БЕЗОПАСНОСТЬ И ОХРАНА ТРУДА (Критический приоритет)
            - Выяви всех сотрудников с повышенной температурой (выше 37.2 °C). Оцени риск массового заражения в конкретном отделе.
            - Проанализируй результаты алкотестирования. Строго выдели тех, у кого результат > 0.0, а также тех, кто уклонился от теста (результат отсутствует), особенно если их должность связана с риском (водители, слесари, инженеры, специалисты по ремонту).

            2. ТРУДОВАЯ ДИСЦИПЛИНА
            - Выяви злостных нарушителей режима рабочего времени (многократные опоздания или ранние уходы).
            - Посчитай примерное количество потерянных рабочих часов из-за опозданий и ранних уходов (если данных достаточно).
            - Обрати внимание на 'Примечания' — есть ли системные или повторяющиеся оправдания?

            3. ПАТТЕРНЫ И АНОМАЛИИ В ОТДЕЛАХ
            - Сравни подразделения между собой. Какой отдел является самым недисциплинированным?
            - Найди странные аномалии (например: массовый вход/выход группы лиц в одно и то же время, проходы в нерабочие часы или выходные дни).

            ФОРМАТ ОТВЕТА:
            - Отвечай строго в формате Markdown.
            - Используй заголовки, списки и выделение жирным для ФИО нарушителей.
            - Делай выводы кратко, без воды. Если аномалий нет — так и скажи, не выдумывай их.

            Данные:
            {textData}
            """;

            try
            {
                // Передаем токен в метод SDK OpenAI
                ChatCompletion completion = await _chatClient.CompleteChatAsync(
                    [new UserChatMessage(prompt)],
                 cancellationToken: cancellationToken
                );

                if (completion != null && completion.Content != null && completion.Content.Count > 0)
                    return completion.Content[0].Text;
                return "⚠️ ИИ вернул пустой ответ.";
            }
            catch (OperationCanceledException)
            {
                return "Анализ был отменен пользователем.";
            }
            catch (Exception ex)
            {
                return $"Ошибка при обращении к ИИ: {ex.Message}";
            }
        }
    }
}