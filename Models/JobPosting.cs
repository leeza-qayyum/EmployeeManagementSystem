namespace EmployeeManagementSystem.Models
{
    public class JobPosting
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string RequiredSkills { get; set; }  // comma-separated
        public string Department { get; set; }
        public DateTime PostedDate { get; set; }
        public DateTime? ApplicationDeadline { get; set; }
        public bool IsActive { get; set; }
        public string GoogleFormLink { get; set; }  // For display purposes
    }

    public class JobApplication
    {
        public int Id { get; set; }
        public int JobPostingId { get; set; }
        public string CandidateName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string CVFilePath { get; set; }
        public string ExtractedSkills { get; set; }
        public int MatchScore { get; set; }
        public bool IsShortlisted { get; set; }
        public DateTime AppliedDate { get; set; }
        public string Status { get; set; } // Pending, Shortlisted, Hired, Rejected
    }
}