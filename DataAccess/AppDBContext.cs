using Microsoft.EntityFrameworkCore;

namespace LogGate.DataAccess;

internal class AppDBContext(DbContextOptions<AppDBContext> options) : DbContext(options)
{
    public DbSet<DataItem> DataItems => Set<DataItem>();
    public DbSet<ShortenedWorkDay> ShortenedWorkDays => Set<ShortenedWorkDay>();
    public DbSet<WorkScheduleRule> WorkScheduleRules => Set<WorkScheduleRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DataItem>()
            .HasIndex(x => new { x.RecordNumber, x.EventTime });

        modelBuilder.Entity<DataItem>()
            .HasIndex(x => x.EventTime);

        modelBuilder.Entity<DataItem>()
            .HasIndex(x => new { x.FullName, x.EventTime });
    }
}
