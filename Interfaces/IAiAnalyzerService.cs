using OpenAI.Chat;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LogGate.Interfaces
{
    public interface IAiAnalyzerService
    {
        Task<string> SendMessageAsync(List<ChatMessage> conversationHistory, CancellationToken cancellationToken = default);
    }
}