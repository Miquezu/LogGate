using LogGate.Interfaces;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LogGate.Services
{
    public class AiAnalyzerService : IAiAnalyzerService
    {
        private readonly ChatClient _chatClient;

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
                throw new InvalidOperationException("API-ключ OpenRouter не найден ни в appsettings.json, ни в переменных среды Windows (OPENROUTER_API_KEY).");
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
            try
            {
                ChatCompletion completion = await _chatClient.CompleteChatAsync(
                    conversationHistory,
                    cancellationToken: cancellationToken
                );

                if (completion?.Content != null && completion.Content.Count > 0)
                {
                    return completion.Content[0].Text;
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
}