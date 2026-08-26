using LogGate.Models;
using Microsoft.EntityFrameworkCore;

namespace LogGate.DataAccess
{
    internal class AppDBContext : DbContext
    {
        public AppDBContext(DbContextOptions<AppDBContext> options) : base(options)
        {
        }

        public DbSet<DataItem> DataItems { get; set; }
        public DbSet<ShortenedWorkDay> ShortenedWorkDays { get; set; }
        public DbSet<WorkScheduleRule> WorkScheduleRules { get; set; }
    }
}