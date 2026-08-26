using LogGate.Interfaces;
using LogGate.Views;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Windows;

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

            // Асинхронно выгружаем данные
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
                MessageBox.Show("За выбранный период нарушений не найдено. Все сотрудники соблюдали правила!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Асинхронно получаем правила из БД
            var workRules = await _dataRepository.GetAllWorkRulesAsync();

            var sb = new StringBuilder();
            sb.AppendLine("=== ВНУТРЕННИЕ РЕГЛАМЕНТЫ ПРЕДПРИЯТИЯ ===");

            var alcoRequiredTargets = workRules
                .Where(r => r.RequiresAlcotest)
                .Select(r => r.TargetName)
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
                    sb.AppendLine($"- {rule.TargetName} {targetType}: с {rule.StartTime:hh\\:mm} до {rule.EndTime:hh\\:mm}");
                }
            }
            else
                sb.AppendLine("- Используется стандартный график по умолчанию.");
            sb.AppendLine();

            sb.AppendLine("=== СТАТИСТИКА ЗА ПЕРИОД ===");
            sb.AppendLine($"Всего зафиксировано проходов: {totalRecords}");
            sb.AppendLine($"Всего уникальных сотрудников прошло: {totalEmployees}");
            sb.AppendLine($"Выявлено нарушений/инцидентов: {violators.Count}");
            sb.AppendLine();

            sb.AppendLine("=== ДЕТАЛИЗАЦИЯ ИНЦИДЕНТОВ (Только нарушения) ===");

            foreach (var item in violators)
                sb.AppendLine($"{item.EventTime:dd.MM HH:mm} {item.Direction} | {item.FullName} ({item.Position}, {item.Department}) | Т:{item.Temperature} | Алко:{item.AlcotestResult} | Прим: {item.Note}");

            var reportWindow = new ReportWindow();
            if (Application.Current.MainWindow != null)
                reportWindow.Owner = Application.Current.MainWindow;

            var cts = new CancellationTokenSource();
            EventHandler onWindowClosed = (s, e) => { try { cts.Cancel(); } catch { } };
            reportWindow.Closed += onWindowClosed;
            reportWindow.Show();

            try
            {
                var aiService = new AiAnalyzerService();
                string report = await aiService.AnalyzeDataAsync(sb.ToString(), cts.Token);

                if (!cts.IsCancellationRequested)
                    reportWindow.DisplayReport(report);
            }
            catch (Exception ex)
            {
                if (!cts.IsCancellationRequested)
                {
                    reportWindow.Close();
                    MessageBox.Show($"Ошибка при обращении к ИИ:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                reportWindow.Closed -= onWindowClosed;
                cts.Dispose();
            }
        }
    }
}