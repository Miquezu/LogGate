using LogGate.DataAccess;
using LogGate.Interfaces;
using LogGate.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace LogGate
{
    public partial class App : Application
    {
        public App()
        {
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // 1. База данных
                    services.AddDbContext<AppDBContext>();

                    // 2. Сервисы (Singleton - один экземпляр на всю программу, Scoped - на один цикл работы)
                    services.AddSingleton<IFileParser, CsvFileParser>();
                    services.AddSingleton<IDialogService, OpenDialog>();
                    services.AddScoped<IDataRepository, DataRepository>();

                    // 3. ViewModels и Окна (Transient - новый экземпляр при каждом запросе)
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<MainWindow>();
                })
                .Build();
        }

        // Глобальный хост приложения
        public static IHost? AppHost { get; private set; }

        protected override async void OnExit(ExitEventArgs e)
        {
            await AppHost!.StopAsync();
            AppHost.Dispose();
            base.OnExit(e);
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            await AppHost!.StartAsync();

            // Применяем миграции безопасно через DI
            using (var scope = AppHost.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDBContext>();
                db.Database.Migrate();
            }

            // Просим DI-контейнер собрать нам MainWindow (со всеми зависимостями!)
            var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}