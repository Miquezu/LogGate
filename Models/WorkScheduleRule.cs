using System;
using System.Collections.Generic;
using System.Text;

namespace LogGate.Models
{
    public class WorkScheduleRule
    {
        public int Id { get; set; }

        // Название отдела (например, "Администрация") ИЛИ ФИО сотрудника
        public string? TargetName { get; set; }

        // Если true — это индивидуальный график (исключение), если false — график отдела
        public bool IsPersonal { get; set; }

        public TimeSpan StartTime { get; set; } // Ожидаемое время прихода (например, 08:00)
        public TimeSpan EndTime { get; set; }   // Ожидаемое время ухода (например, 16:30)
    }
}