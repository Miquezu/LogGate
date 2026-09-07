using LogGate.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace LogGate.Services
{
    public class AiReportManager
    {
        private readonly IDataRepository _dataRepository;
        private readonly IScheduleService _scheduleService;

        public AiReportManager(IDataRepository dataRepository, IScheduleService scheduleService)
        {
            _dataRepository = dataRepository;
            _scheduleService = scheduleService;
        }

        public async Task GenerateReportAsync(string searchText, DateTime? startDate, DateTime? endDate)
        {
            var query = _dataRepository.GetAllItems();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var lowerText = searchText.ToLower();
                query = query.Where(x =>
                    (x.FullName != null && x.FullName.ToLower().Contains(lowerText)) ||
                    (x.PassNumber != null && x.PassNumber.ToLower().Contains(lowerText)) ||
                    (x.Department != null && x.Department.ToLower().Contains(lowerText))
                );
            }

            if (startDate.HasValue) query = query.Where(x => x.EventTime >= startDate.Value);
            if (endDate.HasValue) query = query.Where(x => x.EventTime <= endDate.Value.AddDays(1).AddTicks(-1));

            var fullData = await query.ToListAsync();

            if (fullData.Count == 0)
            {
                MessageBox.Show("Нет данных для анализа за этот период.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int totalRecords = fullData.Count;
            int totalEmployees = fullData.Where(x => x.FullName != null).Select(x => x.FullName).Distinct().Count();

            var violators = fullData.Where(item =>
                (item.Temperature > 37.2) ||
                (item.AlcotestResult > 0) ||
                (_scheduleService.RequiresAlcotest(item) && item.AlcotestResult == null) ||
                _scheduleService.IsLate(item) ||
                _scheduleService.IsEarlyDeparture(item)
            ).ToList();

            if (violators.Count == 0)
            {
                MessageBox.Show("За выбранный период нарушений не найдено.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

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
                sb.AppendLine("- Используется стандартный график по умолчанию.");
            sb.AppendLine();

            sb.AppendLine("=== СТАТИСТИКА ЗА ПЕРИОД ===");
            sb.AppendLine($"Всего зафиксировано проходов: {totalRecords}");
            sb.AppendLine($"Всего уникальных сотрудников прошло: {totalEmployees}");
            sb.AppendLine($"Выявлено нарушений/инцидентов: {violators.Count}");

            // Отдельный список логов для динамического RAG-фильтра
            var logLines = new List<string>();
            foreach (var item in violators)
            {
                string empToken = GetMaskedValue(item.FullName, "EMP", ref empCounter);
                string depToken = GetMaskedValue(item.Department, "DEP", ref depCounter);
                string posToken = GetMaskedValue(item.Position, "POS", ref posCounter);

                logLines.Add($"{item.EventTime:dd.MM HH:mm} {item.Direction} | {empToken} ({posToken}, {depToken}) | Т:{item.Temperature} | Алко:{item.AlcotestResult} | Сист:{item.SystemNote} | Прим: {item.Note}");
            }

            var chatViewModel = new ViewModels.AiChatViewModel(sb.ToString(), logLines, tokenToRealValue, realValueToToken);
            var chatWindow = new Views.AiChatWindow(chatViewModel);

            if (Application.Current.MainWindow != null)
                chatWindow.Owner = Application.Current.MainWindow;

            chatWindow.Show();
        }
    }
}