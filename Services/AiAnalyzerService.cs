using LogGate.Interfaces;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace LogGate.Services;

public class AiAnalyzerService : IAiAnalyzerService
{
    private readonly ChatClient? _chatClient;

    public AiAnalyzerService(IConfiguration configuration)
    {
        string? apiKey = configuration["OpenRouter:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY", EnvironmentVariableTarget.User)
                  ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY", EnvironmentVariableTarget.Process)
                  ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY", EnvironmentVariableTarget.Machine);
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // Не выбрасываем исключение при старте приложения, чтобы не ломать запуск без настроенного ИИ
            _chatClient = null;
            return;
        }

        string endpoint = configuration["OpenRouter:Endpoint"] ?? "https://openrouter.ai/api/v1";
        string modelName = configuration["OpenRouter:Model"] ?? "openrouter/auto";

        int timeoutMinutes = 3;
        if (int.TryParse(configuration["OpenRouter:TimeoutMinutes"], out int parsedTimeout))
        {
            timeoutMinutes = parsedTimeout;
        }

        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri(endpoint),
            NetworkTimeout = TimeSpan.FromMinutes(timeoutMinutes)
        };

        _chatClient = new ChatClient(modelName, new ApiKeyCredential(apiKey), options);
    }

    public async Task<string> SendMessageAsync(List<ChatMessage> conversationHistory, CancellationToken cancellationToken = default)
    {
        if (_chatClient is null)
        {
            return "API-ключ OpenRouter не найден ни в appsettings.json, ни в переменных среды Windows (OPENROUTER_API_KEY). Пожалуйста, укажите ключ для работы с ИИ-отчетами.";
        }

        try
        {
            ChatCompletion completion = await _chatClient.CompleteChatAsync(
                conversationHistory,
                cancellationToken: cancellationToken
            );

            if (completion?.Content is { Count: > 0 } content)
            {
                return content[0].Text;
            }

            return "Нейросеть вернула пустой ответ.";
        }
        catch (OperationCanceledException)
        {
            return "Запрос к ИИ был отменен пользователем.";
        }
        catch (Exception ex)
        {
            return $"Ошибка обращения к API: {ex.Message}";
        }
    }
}