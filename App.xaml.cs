using LogGate.DataAccess;
using LogGate.Services;
using LogGate.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace LogGate;

public partial class App : Application
{
    public App()
    {
        AppHost = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // 1. Фабрика контекстов базы данных SQLite с оптимизациями WAL и case-insensitive lower()
                services.AddDbContextFactory<AppDBContext>(options =>
                {
                    var connectionString = context.Configuration.GetConnectionString("DefaultConnection");

                    if (string.IsNullOrEmpty(connectionString))
                        connectionString = "Data Source=app_data.db";

                    var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
                    connection.Open();
                    connection.CreateFunction("lower", (string? x) => x?.ToLower());

                    using (var pragmaCmd = connection.CreateCommand())
                    {
                        pragmaCmd.CommandText = """
                            PRAGMA journal_mode = WAL;
                            PRAGMA synchronous = NORMAL;
                            PRAGMA temp_store = MEMORY;
                            """;
                        pragmaCmd.ExecuteNonQuery();
                    }

                    options.UseSqlite(connection);
                });

                // 2. Сервисы уровня ядра и доменной логики
                services.AddSingleton<IFileParser, CsvFileParser>();
                services.AddSingleton<IDataCleaningService, DataCleaningService>();
                services.AddSingleton<IDataImportService, DataImportService>();
                services.AddSingleton<ICalendarService, CalendarService>();
                services.AddSingleton<IDialogService, OpenDialog>();
                services.AddSingleton<IScheduleService, ScheduleService>();
                services.AddSingleton<IDataRepository, DataRepository>();
                services.AddSingleton<AutoImportService>();
                services.AddSingleton<IDashboardService, DashboardService>();
                services.AddSingleton<IAiAnalyzerService, AiAnalyzerService>();
                services.AddTransient<AiReportManager>();
                services.AddSingleton<IExportService, CsvExportService>();
                services.AddSingleton<ITimesheetService, TimesheetService>();
                services.AddSingleton<IPrintService, PrintService>();

                // 3. ViewModels и окна
                services.AddTransient<MainViewModel>();
                services.AddTransient<ScheduleSettingsViewModel>();
                services.AddTransient<MainWindow>();
            })
            .Build();
    }

    public static IHost? AppHost { get; private set; }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (AppHost is not null)
        {
            await AppHost.StopAsync();
            AppHost.Dispose();
        }
        base.OnExit(e);
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        await AppHost!.StartAsync();

        using (var scope = AppHost.Services.CreateScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDBContext>>();
            using var db = factory.CreateDbContext();
            db.Database.Migrate();
        }

        var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}
