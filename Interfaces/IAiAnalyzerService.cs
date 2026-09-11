using OpenAI.Chat;

namespace LogGate.Interfaces;

public interface IAiAnalyzerService
{
    Task<string> SendMessageAsync(List<ChatMessage> conversationHistory, CancellationToken cancellationToken = default);
}