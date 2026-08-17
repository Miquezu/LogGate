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

            // Небольшая проверка, чтобы приложение не упало, если ключ забыли добавить
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new Exception("Не найден API-ключ. Проверьте переменные среды Windows.");
            }

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
            Ты строгий аналитик службы безопасности предприятия. Проанализируй выгрузку с турникетов.
            В колонке "Статус нарушения" система уже точно определила, кто опоздал, а кто ушел рано, опираясь на сложные графики отделов.
            Доверяй этой колонке на 100%.

            Сделай короткий структурированный вывод в формате Markdown:
            1. **Главные нарушители**: Кто чаще всего опаздывает или уходит раньше времени (назови конкретные ФИО и количество нарушений).
            2. **Проблемные отделы**: В каких отделах хуже всего с дисциплиной.

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
            catch (OperationCanceledException) // Ловим именно отмену
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