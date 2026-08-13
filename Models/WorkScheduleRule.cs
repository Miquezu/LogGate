namespace LogGate.Models
{
    public class WorkScheduleRule
    {
        public TimeSpan EndTime { get; set; }
        public int Id { get; set; }

        // Если true — это индивидуальный график (исключение), если false — график отдела
        public bool IsPersonal { get; set; }

        public TimeSpan StartTime { get; set; }
        public string? TargetName { get; set; }
    }
}