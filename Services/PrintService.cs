using LogGate.Interfaces;
using LogGate.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace LogGate.Services
{
    public class PrintService : IPrintService
    {
        public bool PrintEmployeeDossier(
            string employeeName,
            string department,
            string position,
            string employeeNumber,
            string punctualityRate,
            int lateCount,
            int earlyCount,
            TimesheetSummary summary,
            IEnumerable<DailyWorkRecord> records)
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() != true)
                return false;

            var doc = CreateDossierDocument(
                employeeName,
                department,
                position,
                employeeNumber,
                punctualityRate,
                lateCount,
                earlyCount,
                summary,
                records);

            var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
            printDialog.PrintDocument(paginator, $"Досье СКУД - {employeeName}");
            return true;
        }

        private static FlowDocument CreateDossierDocument(
            string employeeName,
            string department,
            string position,
            string employeeNumber,
            string punctualityRate,
            int lateCount,
            int earlyCount,
            TimesheetSummary summary,
            IEnumerable<DailyWorkRecord> records)
        {
            var doc = new FlowDocument
            {
                PageWidth = 793,
                PageHeight = 1122,
                ColumnWidth = 793,
                PagePadding = new Thickness(36),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(33, 33, 33))
            };

            // 1. ШАПКА
            var headerSection = new Section();
            var subHeader = new Paragraph(new Run("СИСТЕМА КОНТРОЛЯ ДОСТУПА И УЧЁТА РАБОЧЕГО ВРЕМЕНИ «LOGGATE»"))
            {
                FontSize = 8.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(117, 117, 117)),
                Margin = new Thickness(0, 0, 0, 2)
            };
            var title = new Paragraph(new Run("ДОСЬЕ СОТРУДНИКА И ТАБЕЛЬ УЧЁТА ВРЕМЕНИ"))
            {
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(94, 53, 177)),
                Margin = new Thickness(0, 0, 0, 4)
            };
            var dateGen = new Paragraph(new Run($"Дата формирования документа: {DateTime.Now:dd.MM.yyyy HH:mm}"))
            {
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                Margin = new Thickness(0, 0, 0, 12)
            };
            headerSection.Blocks.Add(subHeader);
            headerSection.Blocks.Add(title);
            headerSection.Blocks.Add(dateGen);
            doc.Blocks.Add(headerSection);

            // 2. КАРТОЧКА СОТРУДНИКА (Таблица данных профиля)
            var profileTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 10) };
            profileTable.Columns.Add(new TableColumn { Width = new GridLength(360) });
            profileTable.Columns.Add(new TableColumn { Width = new GridLength(360) });

            var profileGroup = new TableRowGroup();
            var pRow1 = new TableRow();
            pRow1.Cells.Add(CreateMetaCell("ФИО сотрудника:", employeeName, true));
            pRow1.Cells.Add(CreateMetaCell("Подразделение:", department, false));
            profileGroup.Rows.Add(pRow1);

            var pRow2 = new TableRow();
            pRow2.Cells.Add(CreateMetaCell("Должность:", position, false));
            pRow2.Cells.Add(CreateMetaCell("Табельный номер:", employeeNumber, false));
            profileGroup.Rows.Add(pRow2);

            profileTable.RowGroups.Add(profileGroup);
            doc.Blocks.Add(profileTable);

            // 3. БЛОК СВОДНЫХ KPI (Таблица 4х2)
            var kpiTable = new Table { CellSpacing = 4, Margin = new Thickness(0, 0, 0, 12) };
            for (int i = 0; i < 4; i++)
                kpiTable.Columns.Add(new TableColumn { Width = new GridLength(177) });

            var kpiGroup = new TableRowGroup();
            var kpiRow1 = new TableRow();
            kpiRow1.Cells.Add(CreateKpiCell("Отработано", summary.TotalWorkedTimeFormatted, Color.FromRgb(94, 53, 177)));
            kpiRow1.Cells.Add(CreateKpiCell("Норма по плану", summary.TotalPlannedTimeFormatted, Color.FromRgb(21, 101, 192)));
            kpiRow1.Cells.Add(CreateKpiCell("Баланс времени", summary.TotalBalanceFormatted, summary.IsTotalUndertime ? Color.FromRgb(198, 40, 40) : Color.FromRgb(46, 125, 50)));
            kpiRow1.Cells.Add(CreateKpiCell("Рабочих дней", $"{summary.TotalWorkDays} дн. (ср. {summary.AverageHoursPerDayFormatted})", Color.FromRgb(230, 81, 0)));
            kpiGroup.Rows.Add(kpiRow1);

            var kpiRow2 = new TableRow();
            kpiRow2.Cells.Add(CreateKpiCell("Пунктуальность", punctualityRate, Color.FromRgb(46, 125, 50)));
            kpiRow2.Cells.Add(CreateKpiCell("Опозданий", lateCount.ToString(), lateCount > 0 ? Color.FromRgb(251, 140, 0) : Color.FromRgb(117, 117, 117)));
            kpiRow2.Cells.Add(CreateKpiCell("Ранних уходов", earlyCount.ToString(), earlyCount > 0 ? Color.FromRgb(229, 57, 53) : Color.FromRgb(117, 117, 117)));
            kpiRow2.Cells.Add(CreateKpiCell("Нарушений алкотеста", summary.TotalAlcotestViolations.ToString(), summary.TotalAlcotestViolations > 0 ? Color.FromRgb(183, 28, 28) : Color.FromRgb(117, 117, 117)));
            kpiGroup.Rows.Add(kpiRow2);

            kpiTable.RowGroups.Add(kpiGroup);
            doc.Blocks.Add(kpiTable);

            // 4. ТАБЛИЦА ТАБЕЛЯ ПО ДНЯМ
            var timesheetTitle = new Paragraph(new Run("ЖУРНАЛ УЧЁТА РАБОЧИХ ДНЕЙ"))
            {
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(66, 66, 66)),
                Margin = new Thickness(0, 6, 0, 4)
            };
            doc.Blocks.Add(timesheetTitle);

            var tsTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 16) };
            tsTable.Columns.Add(new TableColumn { Width = new GridLength(28) });  // №
            tsTable.Columns.Add(new TableColumn { Width = new GridLength(85) });  // Дата
            tsTable.Columns.Add(new TableColumn { Width = new GridLength(85) });  // День недели
            tsTable.Columns.Add(new TableColumn { Width = new GridLength(68) });  // Вход
            tsTable.Columns.Add(new TableColumn { Width = new GridLength(68) });  // Выход
            tsTable.Columns.Add(new TableColumn { Width = new GridLength(58) });  // Проходов
            tsTable.Columns.Add(new TableColumn { Width = new GridLength(85) });  // Отработано
            tsTable.Columns.Add(new TableColumn { Width = new GridLength(75) });  // Норма
            tsTable.Columns.Add(new TableColumn { Width = new GridLength(75) });  // Баланс
            tsTable.Columns.Add(new TableColumn { Width = new GridLength(94) });  // Статус

            var tsGroup = new TableRowGroup();

            // Заголовок таблицы
            var hRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(240, 242, 245)) };
            hRow.Cells.Add(CreateHeaderCell("№"));
            hRow.Cells.Add(CreateHeaderCell("Дата"));
            hRow.Cells.Add(CreateHeaderCell("День"));
            hRow.Cells.Add(CreateHeaderCell("Первый вход"));
            hRow.Cells.Add(CreateHeaderCell("Посл. выход"));
            hRow.Cells.Add(CreateHeaderCell("Проходов"));
            hRow.Cells.Add(CreateHeaderCell("Отработано"));
            hRow.Cells.Add(CreateHeaderCell("Норма"));
            hRow.Cells.Add(CreateHeaderCell("Баланс"));
            hRow.Cells.Add(CreateHeaderCell("Статус"));
            tsGroup.Rows.Add(hRow);

            int n = 1;
            foreach (var r in records)
            {
                var row = new TableRow
                {
                    Background = n % 2 == 0
                        ? new SolidColorBrush(Color.FromRgb(250, 250, 250))
                        : Brushes.White
                };

                row.Cells.Add(CreateDataCell(n.ToString(), false));
                row.Cells.Add(CreateDataCell(r.DateFormatted, false));
                row.Cells.Add(CreateDataCell(r.Date.ToString("ddd", new CultureInfo("ru-RU")), false));
                row.Cells.Add(CreateDataCell(r.FirstEntryFormatted, false));
                row.Cells.Add(CreateDataCell(r.LastExitFormatted, false));
                row.Cells.Add(CreateDataCell(r.PassesCount.ToString(), false));
                row.Cells.Add(CreateDataCell(r.WorkedTimeFormatted, true));
                row.Cells.Add(CreateDataCell(r.PlannedTimeFormatted, false));

                var balanceCell = CreateDataCell(r.BalanceFormatted, true);
                if (r.IsOvertime)
                    balanceCell.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50));
                else if (r.IsUndertime)
                    balanceCell.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
                row.Cells.Add(balanceCell);

                var statusCell = CreateDataCell(r.StatusText, false);
                if (r.HasAlcotestViolation || r.IsLate || r.IsEarlyDeparture)
                    statusCell.Foreground = new SolidColorBrush(Color.FromRgb(211, 47, 47));
                row.Cells.Add(statusCell);

                tsGroup.Rows.Add(row);
                n++;
            }

            tsTable.RowGroups.Add(tsGroup);
            doc.Blocks.Add(tsTable);

            // 5. БЛОК ПОДПИСЕЙ
            var signSection = new Section { Margin = new Thickness(0, 16, 0, 0) };
            var signTable = new Table { CellSpacing = 0 };
            signTable.Columns.Add(new TableColumn { Width = new GridLength(360) });
            signTable.Columns.Add(new TableColumn { Width = new GridLength(360) });

            var signGroup = new TableRowGroup();
            var signRow = new TableRow();

            var bossCell = new TableCell(new Paragraph(new Run("Руководитель:  _________________ / _________________ /\n(подпись, расшифровка)"))
            {
                FontSize = 9.5,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
            });
            var empCell = new TableCell(new Paragraph(new Run("Сотрудник ознакомлен:  _________________ / _________________ /\n(подпись, расшифровка)"))
            {
                FontSize = 9.5,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
            });

            signRow.Cells.Add(bossCell);
            signRow.Cells.Add(empCell);
            signGroup.Rows.Add(signRow);
            signTable.RowGroups.Add(signGroup);
            signSection.Blocks.Add(signTable);
            doc.Blocks.Add(signSection);

            return doc;
        }

        private static TableCell CreateMetaCell(string label, string value, bool isBold)
        {
            var p = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
            p.Inlines.Add(new Run(label + " ") { Foreground = new SolidColorBrush(Color.FromRgb(117, 117, 117)), FontSize = 10 });
            p.Inlines.Add(new Run(value) { FontWeight = isBold ? FontWeights.Bold : FontWeights.SemiBold, FontSize = 10.5 });
            return new TableCell(p);
        }

        private static TableCell CreateKpiCell(string title, string value, Color accentColor)
        {
            var p = new Paragraph { Margin = new Thickness(6, 4, 6, 4) };
            p.Inlines.Add(new Run(title + "\n") { FontSize = 8.5, Foreground = new SolidColorBrush(Color.FromRgb(117, 117, 117)) });
            p.Inlines.Add(new Run(value) { FontSize = 12, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(accentColor) });

            return new TableCell(p)
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1)
            };
        }

        private static TableCell CreateHeaderCell(string text)
        {
            var p = new Paragraph(new Run(text))
            {
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(66, 66, 66)),
                Margin = new Thickness(4, 5, 4, 5)
            };
            return new TableCell(p)
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
        }

        private static TableCell CreateDataCell(string text, bool isBold)
        {
            var p = new Paragraph(new Run(text))
            {
                FontSize = 8.5,
                FontWeight = isBold ? FontWeights.SemiBold : FontWeights.Normal,
                Margin = new Thickness(4, 3, 4, 3)
            };
            return new TableCell(p)
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
                BorderThickness = new Thickness(0, 0, 0, 0.5)
            };
        }
    }
}

