using LogGate.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LogGate.DataAccess
{
    internal class AppDBContext : DbContext
    {
        public DbSet<DataItem> DataItems { get; set; }
        public DbSet<ShortenedWorkDay> ShortenedWorkDays { get; set; }
        public DbSet<WorkScheduleRule> WorkScheduleRules { get; set; }

        public AppDBContext()
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connectionString = "Data Source=app_data.db";
            var connection = new SqliteConnection(connectionString);

            connection.Open();

            connection.CreateFunction("lower", (string x) =>
            {
                return x?.ToLower();
            });

            optionsBuilder.UseSqlite(connection);
        }
    }
}