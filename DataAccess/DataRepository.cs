using Microsoft.EntityFrameworkCore;

namespace LogGate.DataAccess;

internal class DataRepository(IDbContextFactory<AppDBContext> contextFactory) : IDataRepository
{
    public async Task<List<WorkScheduleRule>> GetAllWorkRulesAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.WorkScheduleRules.AsNoTracking().ToListAsync();
    }

    public async Task<List<DataItem>> GetEmployeeHistoryAsync(string employeeName)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.DataItems
            .AsNoTracking()
            .Where(x => x.FullName == employeeName)
            .OrderByDescending(x => x.EventTime)
            .ToListAsync();
    }

    public async Task<List<DataItem>> GetFilteredLogsAsync(string? searchText, DateTime? startDate, DateTime? endDate, string? department = null)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var query = context.DataItems.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var lowerText = searchText.ToLower();
            query = query.Where(x =>
                (x.FullName != null && x.FullName.ToLower().Contains(lowerText)) ||
                (x.PassNumber != null && x.PassNumber.ToLower().Contains(lowerText)) ||
                (x.Department != null && x.Department.ToLower().Contains(lowerText))
            );
        }

        if (!string.IsNullOrWhiteSpace(department) && department != "Все подразделения")
        {
            query = query.Where(x => x.Department == department);
        }

        if (startDate.HasValue)
            query = query.Where(x => x.EventTime >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(x => x.EventTime <= endDate.Value.AddDays(1).AddTicks(-1));

        return await query.ToListAsync();
    }

    public async Task<List<string>> GetDepartmentsAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.DataItems
            .AsNoTracking()
            .Where(x => !string.IsNullOrEmpty(x.Department))
            .Select(x => x.Department!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();
    }

    public async Task<List<DateTime>> GetShortenedDaysByYearAsync(int year)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.ShortenedWorkDays
            .AsNoTracking()
            .Where(x => x.Date.Year == year)
            .Select(x => x.Date)
            .ToListAsync();
    }

    public async Task<int> GetTotalCountAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.DataItems.CountAsync();
    }

    public async Task<int> SaveItemsAsync(IEnumerable<DataItem> items)
    {
        var incomingList = items
            .Where(x => (!string.IsNullOrWhiteSpace(x.FullName) || !string.IsNullOrWhiteSpace(x.PassNumber)) && x.EventTime.HasValue)
            .ToList();

        if (incomingList.Count == 0) return 0;

        // Если в CSV отсутствовал или был пуст номер строки, проставляем его автоматически
        int counter = 1;
        foreach (var item in incomingList)
        {
            if (string.IsNullOrWhiteSpace(item.RecordNumber))
            {
                item.RecordNumber = counter.ToString();
            }
            counter++;
        }

        await using var context = await contextFactory.CreateDbContextAsync();

        var minDate = incomingList.Min(x => x.EventTime!.Value);
        var maxDate = incomingList.Max(x => x.EventTime!.Value);

        // Выборка ключей по физическим данным события (ФИО, пропуск, время, направление)
        var existingKeys = await context.DataItems
            .AsNoTracking()
            .Where(x => x.EventTime >= minDate && x.EventTime <= maxDate)
            .Select(x => new { x.FullName, x.PassNumber, x.EventTime, x.Direction })
            .ToListAsync();

        var existingHashSet = existingKeys
            .Select(x => (x.FullName ?? string.Empty, x.PassNumber ?? string.Empty, x.EventTime, x.Direction ?? string.Empty))
            .ToHashSet();

        // Исключаем записи, которые уже есть в БД, а также дубликаты внутри самой входящей пачки
        List<DataItem> uniqueItems = [];
        HashSet<(string, string, DateTime?, string)> seenInBatch = [];

        foreach (var item in incomingList)
        {
            var key = (item.FullName ?? string.Empty, item.PassNumber ?? string.Empty, item.EventTime, item.Direction ?? string.Empty);
            if (!existingHashSet.Contains(key) && seenInBatch.Add(key))
            {
                uniqueItems.Add(item);
            }
        }

        if (uniqueItems.Count != 0)
        {
            await context.DataItems.AddRangeAsync(uniqueItems);
            await context.SaveChangesAsync();
            return uniqueItems.Count;
        }

        return 0;
    }

    public async Task SaveShortenedDaysAsync(IEnumerable<DateTime> dates)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var entities = dates.Select(d => new ShortenedWorkDay { Date = d });
        await context.ShortenedWorkDays.AddRangeAsync(entities);
        await context.SaveChangesAsync();
    }

    public async Task SaveWorkRulesAsync(IEnumerable<WorkScheduleRule> rules)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.WorkScheduleRules.ExecuteDeleteAsync();

        var cleanRules = rules.Select(r => new WorkScheduleRule
        {
            TargetName = r.TargetName,
            IsPersonal = r.IsPersonal,
            StartTime = r.StartTime,
            EndTime = r.EndTime,
            RequiresAlcotest = r.RequiresAlcotest
        }).ToList();

        if (cleanRules.Count != 0)
        {
            await context.WorkScheduleRules.AddRangeAsync(cleanRules);
        }

        await context.SaveChangesAsync();
    }

    public async Task UpdateItemsNotesAsync(IEnumerable<DataItem> items)
    {
        var notesMap = items
            .Where(x => x.Id > 0)
            .ToDictionary(x => x.Id, x => x.Note);

        if (notesMap.Count == 0) return;

        await using var context = await contextFactory.CreateDbContextAsync();
        var ids = notesMap.Keys.ToList();

        var entities = await context.DataItems
            .Where(x => ids.Contains(x.Id))
            .ToListAsync();

        foreach (var entity in entities)
        {
            if (notesMap.TryGetValue(entity.Id, out var newNote))
            {
                entity.Note = newNote;
            }
        }

        await context.SaveChangesAsync();
    }
}
}