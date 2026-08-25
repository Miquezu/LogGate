namespace LogGate.Models
{
    public class WorkScheduleRule
    {
        public TimeSpan EndTime { get; set; }
        public int Id { get; set; }

        public bool IsPersonal { get; set; }

        public bool RequiresAlcotest { get; set; }
        public TimeSpan StartTime { get; set; }
        public string? TargetName { get; set; }
    }
}