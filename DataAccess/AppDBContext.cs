using LogGate.Models;
using Microsoft.EntityFrameworkCore;

namespace LogGate.DataAccess
{
    internal class AppDBContext : DbContext
    {
        public DbSet<DataItem> DataItems { get; set; }
        public AppDBContext()
        {
            Database.Migrate();
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=app_data.db");
        }
    }
}
