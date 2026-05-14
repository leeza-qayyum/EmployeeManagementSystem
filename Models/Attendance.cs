namespace EmployeeManagementSystem.Models
{
    public enum AttendanceStatus
    {
        Present,
        Late,
        Absent,
        HalfDay
    }

    public class Attendance
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public DateTime Date { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public AttendanceStatus Status { get; set; }
        public string Remarks { get; set; }
        public int WorkingHours { get; set; } // in minutes
        public bool IsHoliday { get; set; }
    }
}