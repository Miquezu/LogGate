using System.ComponentModel.DataAnnotations.Schema;

namespace LogGate.Models;

/// <summary>
/// Сущность события прохода/контроля доступа через СКУД.
/// </summary>
public class DataItem
{
    public int Id { get; set; }
    public string? RecordNumber { get; set; }
    public string? Post { get; set; }
    public DateTime? EventTime { get; set; }
    public string? Direction { get; set; }
    public string? FullName { get; set; }
    public string? Position { get; set; }
    public string? Department { get; set; }
    public string? EmployeeNumber { get; set; }
    public string? PassNumber { get; set; }

    public DateTime? TemperatureTime { get; set; }
    public double? Temperature { get; set; }

    public DateTime? AlcotestTime { get; set; }
    public double? AlcotestResult { get; set; }

    public string? SystemNote { get; set; }
    public string? Note { get; set; }

    [NotMapped]
    public bool IsLate { get; set; }

    [NotMapped]
    public bool IsEarlyDeparture { get; set; }

    [NotMapped]
    public bool HasFever => Temperature.HasValue && Temperature.Value > 37.0;

    [NotMapped]
    public bool HasAlcotestViolation => AlcotestResult.HasValue && AlcotestResult.Value > 0.0;

    [NotMapped]
    public bool HasDisciplineViolation => IsLate || IsEarlyDeparture;
}
}