using LogGate.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogGate.Services
{
    public class AiReportManager
    {
        private readonly IAiAnalyzerService _aiService;
        private readonly IDataRepository _dataRepository;
        private readonly IDialogService _dialogService;
        private readonly IScheduleService _scheduleService;

        public AiReportManager(
            IDataRepository dataRepository,
            IScheduleService scheduleService,
            IDialogService dialogService,
            IAiAnalyzerService aiService)
        {
            _dataRepository = dataRepository;
            _scheduleService = scheduleService;
            _dialogService = dialogService;
            _aiService = aiService;
        }

        public async Task GenerateReportAsync(string searchText, DateTime? startDate, DateTime? endDate)
        {
            var fullData = await _dataRepository.GetFilteredLogsAsync(searchText, startDate, endDate);

            if (fullData.Count == 0)
            {
                _dialogService.ShowWarning("Нет данных для анализа за этот период.");
                return;
            }

            _scheduleService.EvaluateCompliance(fullData);

            int totalRecords = fullData.Count;
            int totalEmployees = fullData.Where(x => x.FullName != null).Select(x => x.FullName).Distinct().Count();

            var tokenToRealValue = new Dictionary<string, string>();
            var realValueToToken = new Dictionary<string, string>();
            int empCounter = 1;
            int depCounter = 1;
            int posCounter = 1;

            string GetMaskedValue(string? realValue, string prefix, ref int counter)
            {
                if (string.IsNullOrWhiteSpace(realValue)) return "Н/Д";

                if (!realValueToToken.TryGetValue(realValue, out string? token))
                {
                    token = $"{prefix}_{counter:D3}";
                    realValueToToken[realValue] = token;
                    tokenToRealValue[token] = realValue;
                    counter++;
                }
                return token;
            }

            string GetTargetToken(string? targetName, bool isPersonal)
            {
                if (isPersonal) return GetMaskedValue(targetName, "EMP", ref empCounter);
                return GetMaskedValue(targetName, "DEP", ref depCounter);
            }

            var workRules = await _dataRepository.GetAllWorkRulesAsync();

            var sb = new StringBuilder();
            sb.AppendLine("=== ВНУТРЕННИЕ РЕГЛАМЕНТЫ ПРЕДПРИЯТИЯ ===");

            var alcoRequiredTargets = workRules
                .Where(r => r.RequiresAlcotest)
                .Select(r => GetTargetToken(r.TargetName, r.IsPersonal))
                .ToList();

            sb.AppendLine("Отделы и сотрудники, обязанные проходить алкотест:");
            if (alcoRequiredTargets.Count != 0)
                sb.AppendLine(string.Join(", ", alcoRequiredTargets));
            else
                sb.AppendLine("- Обязательное прохождение не назначено.");

            sb.AppendLine();
            sb.AppendLine("Установленные графики работы:");
            if (workRules.Count != 0)
            {
                foreach (var rule in workRules)
                {
                    string targetType = rule.IsPersonal ? "(Индивидуальный)" : "(Отдел)";
                    string maskedTarget = GetTargetToken(rule.TargetName, rule.IsPersonal);
                    sb.AppendLine($"- {maskedTarget} {targetType}: с {rule.StartTime:hh\\:mm} до {rule.EndTime:hh\\:mm}");
                }
            }
            else
            {
                sb.AppendLine("- Используется стандартный график по умолчанию.");
            }
            sb.AppendLine();

            var groupedDays = fullData
                .Where(x => !string.IsNullOrWhiteSpace(x.FullName) && x.EventTime.HasValue)
                .GroupBy(x => new { x.FullName, Date = x.EventTime!.Value.Date })
                .ToList();

            var incidentSummaries = new List<string>();
            var employeeDailySummaries = new Dictionary<string, List<string>>();
            int totalIncidentsCount = 0;

            foreach (var dayGroup in groupedDays)
            {
                var dayEvents = dayGroup.OrderBy(x => x.EventTime).ToList();
                var first = dayEvents.First();

                string empToken = GetMaskedValue(first.FullName, "EMP", ref empCounter);
                string depToken = GetMaskedValue(first.Department, "DEP", ref depCounter);
                string posToken = GetMaskedValue(first.Position, "POS", ref posCounter);

                var incidentList = new List<string>();

                var lateEntry = dayEvents.FirstOrDefault(x => x.IsLate);
                if (lateEntry != null)
                    incidentList.Add($"Опоздание (вход в {lateEntry.EventTime:HH:mm})");

                var earlyExit = dayEvents.FirstOrDefault(x => x.IsEarlyDeparture);
                if (earlyExit != null)
                    incidentList.Add($"Ранний уход (выход в {earlyExit.EventTime:HH:mm})");

                var tempIssues = dayEvents.Where(x => x.Temperature > 37.2).ToList();
                if (tempIssues.Any())
                    incidentList.Add($"Температура > 37.2 ({string.Join(", ", tempIssues.Select(t => $"{t.EventTime:HH:mm}: {t.Temperature}°C"))})");

                var alcoPositive = dayEvents.Where(x => x.AlcotestResult > 0).ToList();
                if (alcoPositive.Any())
                    incidentList.Add($"Положительный алкотест ({string.Join(", ", alcoPositive.Select(a => $"{a.EventTime:HH:mm}: {a.AlcotestResult} мг/л"))})");

                if (_scheduleService.RequiresAlcotest(first) && dayEvents.All(x => x.AlcotestResult == null))
                    incidentList.Add("Не пройден обязательный алкотест");

                var anomalies = dayEvents.Where(x => !string.IsNullOrWhiteSpace(x.SystemNote)).Select(x => x.SystemNote).Distinct();
                foreach (var anom in anomalies)
                    incidentList.Add(anom!);

                string timeline = string.Join(", ", dayEvents.Select(e => $"{e.EventTime:HH:mm} {e.Direction}"));
                string statusText = incidentList.Count > 0 ? $"Инциденты: {string.Join("; ", incidentList)}" : "Норма";

                string daySummaryLine = $"{dayGroup.Key.Date:dd.MM} | {empToken} ({posToken}, {depToken}) | Проходы: [{timeline}] | Статус: {statusText}";

                if (!employeeDailySummaries.ContainsKey(empToken))
                    employeeDailySummaries[empToken] = new List<string>();

                employeeDailySummaries[empToken].Add(daySummaryLine);

                if (incidentList.Count > 0)
                {
                    incidentSummaries.Add(daySummaryLine);
                    totalIncidentsCount += incidentList.Count;
                }
            }

            sb.AppendLine("=== СТАТИСТИКА ЗА ПЕРИОД ===");
            sb.AppendLine($"Всего зафиксировано проходов: {totalRecords}");
            sb.AppendLine($"Всего уникальных сотрудников прошло: {totalEmployees}");
            sb.AppendLine($"Всего инцидентов/нарушений: {totalIncidentsCount}");

            var chatViewModel = new ViewModels.AiChatViewModel(
                sb.ToString(),
                incidentSummaries,
                employeeDailySummaries,
                tokenToRealValue,
                realValueToToken,
                _aiService);

            _dialogService.OpenAiChat(chatViewModel);

            _dialogService.OpenAiChat(chatViewModel);
        }
    }
}