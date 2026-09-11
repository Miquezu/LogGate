using OpenAI.Chat;
using System.Text.RegularExpressions;

namespace LogGate.ViewModels;

/// <summary>
/// ViewModel диалогового окна ИИ-аналитики и расследования инцидентов.
/// </summary>
public partial class AiChatViewModel : ObservableObject
{
    private const int MaxHistoryMessages = 10;
    private readonly IAiAnalyzerService _aiService;
    private readonly List<ChatMessage> _apiHistory = [];
    private readonly string _baseSystemPrompt;
    private readonly Dictionary<string, List<string>> _employeeDailySummaries;
    private readonly List<string> _incidentSummaries;
    private readonly Dictionary<string, string> _realValueToToken;
    private readonly Dictionary<string, string> _tokenToRealValue;

    private CancellationTokenSource? _generationCts;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private ObservableCollection<UiMessage> _messages = [];

    public AiChatViewModel(
        string baseContext,
        List<string> incidentSummaries,
        Dictionary<string, List<string>> employeeDailySummaries,
        Dictionary<string, string> tokenToRealValue,
        Dictionary<string, string> realValueToToken,
        IAiAnalyzerService aiService)
    {
        _aiService = aiService;
        _incidentSummaries = incidentSummaries;
        _employeeDailySummaries = employeeDailySummaries;
        _tokenToRealValue = tokenToRealValue;
        _realValueToToken = realValueToToken;

        string systemRole = """
            Ты — эксперт по корпоративной безопасности и HR-аналитике.
            Отвечай кратко, емко, используй Markdown. Выделяй жирным идентификаторы сотрудников.

            ПРАВИЛА ИНТЕРПРЕТАЦИИ ХРОНОЛОГИИ ПРОХОДОВ ЗА ДЕНЬ:
            1. В поле 'Проходы' перечислена полная хронология событий сотрудника за сутки.
            2. Опозданием на работу является ТОЛЬКО первый утренний вход за день, если он произошел позже времени начала смены.
            3. Любые повторные входы в течение дня (возвращение с обеда, служебные выходы) НЕ ЯВЛЯЮТСЯ опозданиями, если сотрудник пришел на работу вовремя.
            4. Ранним уходом со смены является ТОЛЬКО последний выход за день до времени окончания смены.
            5. Если в статусе дня указано 'Аномалия СКУД: Пропущен проход' — цепочка событий разорвана, точный расчет времени не производится.

            ВАЖНО: Никогда не упоминай внутренние токены (EMP_XXX, DEP_XXX) в ответе. Если данных о нарушениях сотрудника нет, отвечай: 'За выбранный период нарушений у сотрудника не зафиксировано'.
            """;

        _baseSystemPrompt = $"{systemRole}\n\n{baseContext}";

        Messages.Add(new UiMessage
        {
            IsUser = false,
            Text = "Данные загружены. Выберите готовую подсказку ниже или задайте свой вопрос (например: 'Опаздывал ли Городецкий?')."
        });
    }

    public List<string> Suggestions { get; } =
    [
        "Кто чаще всех опаздывал за период?",
        "Были ли сотрудники с температурой > 37.2°C?",
        "Покажи нарушения по алкотестеру",
        "Сводка нарушений по отделам",
        "Кто уходил раньше окончания смены?"
    ];

    [RelayCommand]
    private void ApplySuggestion(string? suggestion)
    {
        if (string.IsNullOrWhiteSpace(suggestion) || IsBusy) return;
        InputText = suggestion;
        _ = SendMessageAsync();
    }

    [RelayCommand]
    private void CancelGeneration()
    {
        if (IsBusy && _generationCts is { IsCancellationRequested: false })
        {
            _generationCts.Cancel();
        }
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText) || IsBusy) return;

        string originalUserText = InputText;
        InputText = string.Empty;
        IsBusy = true;

        Messages.Add(new UiMessage { IsUser = true, Text = originalUserText });

        // 1. Маскирование персональных данных
        string maskedUserText = originalUserText;
        Dictionary<string, string> searchPatterns = [];

        foreach (var kvp in _realValueToToken)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key)) continue;

            searchPatterns[Regex.Escape(kvp.Key)] = kvp.Value;

            if (kvp.Value.StartsWith("EMP_") && !kvp.Key.StartsWith('{'))
            {
                var parts = kvp.Key.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && parts[0].All(char.IsLetter))
                {
                    string lastName = parts[0];
                    string stem = lastName.Length > 4 ? lastName[..^2] : lastName;
                    searchPatterns[$@"\b{Regex.Escape(stem)}\w*"] = kvp.Value;
                }
            }
        }

        foreach (var pattern in searchPatterns.OrderByDescending(x => x.Key.Length))
        {
            maskedUserText = Regex.Replace(maskedUserText, pattern.Key, pattern.Value, RegexOptions.IgnoreCase);
        }

        // 2. Локализация контекста
        var mentionedTokens = _tokenToRealValue.Keys.Where(t => maskedUserText.Contains(t)).ToList();
        string dynamicLogsContext;

        if (mentionedTokens.Count > 0)
        {
            List<string> personalSummaries = [];
            foreach (var token in mentionedTokens)
            {
                if (_employeeDailySummaries.TryGetValue(token, out var summaries))
                    personalSummaries.AddRange(summaries);
            }

            dynamicLogsContext = personalSummaries.Count > 0
                ? "=== ПОЛНАЯ ХРОНОЛОГИЯ ПО ЗАПРОШЕННЫМ СОТРУДНИКАМ ЗА ВЕСЬ ПЕРИОД ===\n" + string.Join("\n", personalSummaries)
                : "=== ДАННЫЕ ПО ЗАПРОСУ ===\nЗаписей не найдено.";
        }
        else
        {
            dynamicLogsContext = _incidentSummaries.Count > 0
                ? "=== ВСЕ ИНЦИДЕНТЫ ЗА ПЕРИОД ===\n" + string.Join("\n", _incidentSummaries)
                : "=== ВСЕ ИНЦИДЕНТЫ ЗА ПЕРИОД ===\nНарушений не зафиксировано.";
        }

        // 3. Формирование истории
        List<ChatMessage> requestHistory =
        [
            new SystemChatMessage($"{_baseSystemPrompt}\n\n{dynamicLogsContext}")
        ];

        _apiHistory.Add(new UserChatMessage(maskedUserText));

        if (_apiHistory.Count > MaxHistoryMessages)
        {
            _apiHistory.RemoveRange(0, _apiHistory.Count - MaxHistoryMessages);
        }

        requestHistory.AddRange(_apiHistory);

        // 4. Отправка с токеном отмены
        _generationCts = new CancellationTokenSource();

        try
        {
            string maskedAiResponse = await _aiService.SendMessageAsync(requestHistory, _generationCts.Token);
            _apiHistory.Add(new AssistantChatMessage(maskedAiResponse));

            string unmaskedAiResponse = maskedAiResponse;
            foreach (var kvp in _tokenToRealValue)
            {
                unmaskedAiResponse = unmaskedAiResponse.Replace(kvp.Key, kvp.Value);
            }

            Messages.Add(new UiMessage { IsUser = false, Text = unmaskedAiResponse });
        }
        finally
        {
            _generationCts.Dispose();
            _generationCts = null;
            IsBusy = false;
        }
    }

    public class UiMessage
    {
        public bool IsUser { get; set; }
        public string SenderName => IsUser ? "Вы" : "ИИ-Аналитик";
        public string Text { get; set; } = string.Empty;
    }
}
}