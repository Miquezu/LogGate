using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System;

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
                Endpoint = new Uri("https://openrouter.ai/api/v1"),
                NetworkTimeout = TimeSpan.FromMinutes(5)
            };

            _chatClient = new ChatClient("openrouter/auto", new ApiKeyCredential(apiKey), options);
        }

        public async Task<string> SendMessageAsync(List<ChatMessage> conversationHistory, CancellationToken cancellationToken = default)
        {
            try
            {
                ChatCompletion completion = await _chatClient.CompleteChatAsync(
                    conversationHistory,
                    cancellationToken: cancellationToken
                );

                if (completion != null && completion.Content != null && completion.Content.Count > 0)
                    return completion.Content[0].Text;

                return "?? ИИ вернул пустой ответ.";
            }
            catch (OperationCanceledException)
            {
                return "Анализ отменен.";
            }
            catch (Exception ex)
            {
                return $"Ошибка генерации: {ex.Message}";
            }
        }
    }
}