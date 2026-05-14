namespace EmployeeManagementSystem.Models
{
    public enum MeetingStatus
    {
        Scheduled,
        Completed,
        Rescheduled
    }

    public class Meeting
    {
        public int Id { get; set; }
        public int DepartmentHeadId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime MeetingDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Location { get; set; }
        public string MeetingLink { get; set; }  // For virtual meetings
        public MeetingStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? RescheduledFrom { get; set; }  // Track original date if rescheduled
        public string RescheduleReason { get; set; }
    }
}