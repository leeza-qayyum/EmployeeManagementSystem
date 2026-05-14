using EmployeeManagementSystem.Models;

namespace EmployeeManagementSystem.Services
{
    public interface IFileDataService
    {
        List<User> GetUsers();
        void SaveUsers(List<User> users);
        List<Employee> GetEmployees();
        void SaveEmployees(List<Employee> employees);
        List<LeaveRequest> GetLeaveRequests();
        void SaveLeaveRequests(List<LeaveRequest> leaves);

        List<JobPosting> GetJobPostings();
        void SaveJobPostings(List<JobPosting> jobs);
        List<JobApplication> GetJobApplications();
        void SaveJobApplications(List<JobApplication> applications);

        List<Meeting> GetMeetings();
        void SaveMeetings(List<Meeting> meetings);
        List<Meeting> GetMeetingsByDepartmentHead(int headId);
        List<Meeting> GetActiveMeetingsForEmployee(int employeeId, int departmentHeadId);

        List<Attendance> GetAttendanceRecords();
        void SaveAttendanceRecords(List<Attendance> records);
        Attendance GetTodayAttendance(int employeeId);
        List<Attendance> GetLateEmployeesByDepartment(string department, DateTime date);
    }
}