using LogGate.DataAccess;
using Microsoft.EntityFrameworkCore;
using System.Windows;

namespace LogGate
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Применяем миграции один раз при запуске программы
            using (var db = new AppDBContext())
            {
                db.Database.Migrate();
            }
        }
    }
}