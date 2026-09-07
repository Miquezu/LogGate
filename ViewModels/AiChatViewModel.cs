using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogGate.Services;
using OpenAI.Chat;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LogGate.ViewModels
{
    public partial class AiChatViewModel : ObservableObject
    {
        private const int MaxHistoryMessages = 10;
        private readonly AiAnalyzerService _aiService;
        private readonly List<ChatMessage> _apiHistory = new();
        private readonly string _baseSystemPrompt;
        private readonly List<string> _logLines;
        private readonly Dictionary<string, string> _realValueToToken;
        private readonly Dictionary<string, string> _tokenToRealValue;

        [ObservableProperty]
        private string _inputText = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private ObservableCollection<UiMessage> _messages = new();

        public AiChatViewModel(string baseContext, List<string> logLines, Dictionary<string, string> tokenToRealValue, Dictionary<string, string> realValueToToken)
        {
            _aiService = new AiAnalyzerService();
            _logLines = logLines;
            _tokenToRealValue = tokenToRealValue;
            _realValueToToken = realValueToToken;

            string systemRole = @"Ты — эксперт по корпоративной безопасности и HR-аналитике.
Отвечай кратко, емко, используй Markdown. Выделяй жирным идентификаторы сотрудников.
Анализируй нарушения: опоздания, ранние уходы, повышенную температуру (>37.2) и положительные алкотесты (>0.0).

ПРАВИЛА ЧТЕНИЯ СИСТЕМНЫХ СТАТУСОВ (Поле 'Сист:'):
1. Если указано 'Автоисправление' — система уже исправила ошибку направления. Считай это направление абсолютно верным и рассчитывай рабочее время в штатном режиме.
2. Если указано 'Аномалия СКУД: Пропущен проход' — цепочка событий разорвана. КАТЕГОРИЧЕСКИ ЗАПРЕЩАЕТСЯ высчитывать отработанное время или время отсутствия для этого инцидента. Указывай, что точный расчет невозможен из-за нарушения регламента использования СКУД.

ВАЖНО: Никогда не упоминай внутренние токены (EMP_XXX, DEP_XXX). Если информации по сотруднику или отделу нет, отвечай строго: 'В данных о нарушениях за выбранный период этот сотрудник/отдел отсутствует'.";

            _baseSystemPrompt = $"{systemRole}\n\n{baseContext}";

            Messages.Add(new UiMessage { IsUser = false, Text = "Данные загружены. Вы можете искать информацию по конкретным сотрудникам (например: 'Опаздывал ли Иванов?')." });
        }

        [RelayCommand]
        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(InputText) || IsBusy) return;

            string originalUserText = InputText;
            InputText = string.Empty;
            IsBusy = true;

            Messages.Add(new UiMessage { IsUser = true, Text = originalUserText });

            // 1. Умная маскировка вариаций ФИО (Иванов Иван, Иван Иванов, Иванов)
            string maskedUserText = originalUserText;
            var variations = new Dictionary<string, string>();

            foreach (var kvp in _realValueToToken)
            {
                variations[kvp.Key] = kvp.Value;
                if (kvp.Value.StartsWith("EMP_"))
                {
                    var parts = kvp.Key.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        variations[$"{parts[0]} {parts[1]}"] = kvp.Value;
                        variations[$"{parts[1]} {parts[0]}"] = kvp.Value;
                    }
                    if (parts.Length >= 3)
                        variations[parts[0]] = kvp.Value;
                }
            }

            foreach (var kvp in variations.OrderByDescending(x => x.Key.Length))
            {
                maskedUserText = Regex.Replace(maskedUserText, Regex.Escape(kvp.Key), kvp.Value, RegexOptions.IgnoreCase);
            }

            // 2. ДИНАМИЧЕСКИЙ ФИЛЬТР (Mini-RAG)
            var mentionedTokens = _tokenToRealValue.Keys.Where(t => maskedUserText.Contains(t)).ToList();
            string dynamicLogsContext;

            if (mentionedTokens.Any())
            {
                var relevantLogs = _logLines.Where(log => mentionedTokens.Any(t => log.Contains(t))).ToList();
                dynamicLogsContext = relevantLogs.Any()
                    ? "=== ОТФИЛЬТРОВАННЫЕ ИНЦИДЕНТЫ ПО ЗАПРОСУ ===\n" + string.Join("\n", relevantLogs)
                    : "=== ОТФИЛЬТРОВАННЫЕ ИНЦИДЕНТЫ ПО ЗАПРОСУ ===\nЗаписей не найдено.";
            }
            else
            {
                dynamicLogsContext = "=== ВСЕ ИНЦИДЕНТЫ ЗА ПЕРИОД ===\n" + string.Join("\n", _logLines);
            }

            // 3. Формирование запроса на лету (Только для этого сообщения)
            var requestHistory = new List<ChatMessage>
            {
                new SystemChatMessage($"{_baseSystemPrompt}\n\n{dynamicLogsContext}")
            };

            _apiHistory.Add(new UserChatMessage(maskedUserText));

            if (_apiHistory.Count > MaxHistoryMessages)
            {
                _apiHistory.RemoveRange(0, _apiHistory.Count - MaxHistoryMessages);
            }

            // Добавляем к системному промпту только короткую переписку, без старых логов
            requestHistory.AddRange(_apiHistory);

            // 4. Отправка и деанонимизация
            string maskedAiResponse = await _aiService.SendMessageAsync(requestHistory);
            _apiHistory.Add(new AssistantChatMessage(maskedAiResponse));

            string unmaskedAiResponse = maskedAiResponse;
            foreach (var kvp in _tokenToRealValue)
            {
                unmaskedAiResponse = unmaskedAiResponse.Replace(kvp.Key, kvp.Value);
            }

            Messages.Add(new UiMessage { IsUser = false, Text = unmaskedAiResponse });
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