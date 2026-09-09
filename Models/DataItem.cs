using System.ComponentModel.DataAnnotations.Schema;

namespace LogGate.Models
{
    public class DataItem
    {
        public double? AlcotestResult { get; set; }
        public DateTime? AlcotestTime { get; set; }
        public string? Department { get; set; }
        public string? Direction { get; set; }
        public string? EmployeeNumber { get; set; }
        public DateTime? EventTime { get; set; }
        public string? FullName { get; set; }
        public int Id { get; set; }

        [NotMapped]
        public bool IsEarlyDeparture { get; set; }

        [NotMapped]
        public bool IsLate { get; set; }

        public string? Note { get; set; }
        public string? PassNumber { get; set; }
        public string? Position { get; set; }
        public string? Post { get; set; }
        public string? RecordNumber { get; set; }
        public string? SystemNote { get; set; }
        public double? Temperature { get; set; }
        public DateTime? TemperatureTime { get; set; }
    }
}