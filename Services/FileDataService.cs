using System.Text.Json;
using EmployeeManagementSystem.Models;

namespace EmployeeManagementSystem.Services
{
    public class FileDataService : IFileDataService
    {
        private readonly string _userFile = Path.Combine("Data", "users.json");
        private readonly string _employeeFile = Path.Combine("Data", "employees.json");
        private readonly string _leaveFile = Path.Combine("Data", "leaves.json");
        private readonly object _lock = new object();

        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            AllowTrailingCommas = true
        };

        public FileDataService()
        {
            Directory.CreateDirectory("Data");
            SeedData();
            EnsureHRProfileExists();
        }

        private void SeedData()
        {
            // Create only admin user if file doesn't exist
            if (!File.Exists(_userFile))
            {
                var users = new List<User>
        {
            new User { Username = "admin", Password = PasswordHasher.HashPassword("admin123"), UserRole = Role.HRManager, EmployeeId = null }
        };
                SaveUsers(users);
            }

            // Create empty employees file (no default employees)
            if (!File.Exists(_employeeFile))
            {
                var employees = new List<Employee>();
                SaveEmployees(employees);
            }

            // Create empty leaves file
            if (!File.Exists(_leaveFile))
            {
                SaveLeaveRequests(new List<LeaveRequest>());
            }
        }

        public void EnsureHRProfileExists()
        {
            var users = GetUsers();
            var admin = users.FirstOrDefault(u => u.Username == "admin");
            if (admin == null) return;

            var employees = GetEmployees();
            Employee hrEmployee = null;

            // If admin already has EmployeeId, try to find that employee
            if (admin.EmployeeId.HasValue)
            {
                hrEmployee = employees.FirstOrDefault(e => e.Id == admin.EmployeeId.Value);
            }

            // If no valid employee, create one
            if (hrEmployee == null)
            {
                hrEmployee = new Employee
                {
                    Id = 0,
                    Name = "Leeza Qayyum",
                    Email = "leeza@gmail.com",
                    Department = "HR Manager",
                    Phone = "03027947500",
                    Salary = 800000
                };
                // Remove any existing employee with Id=0 (avoid duplicate)
                employees.RemoveAll(e => e.Id == 0);
                employees.Add(hrEmployee);
                SaveEmployees(employees);
            }

            // Link admin to this employee
            admin.EmployeeId = 0;
            SaveUsers(users);
        }

        public List<User> GetUsers()
        {
            lock (_lock)
            {
                var json = File.ReadAllText(_userFile);
                return JsonSerializer.Deserialize<List<User>>(json, _options) ?? new List<User>();
            }
        }

        public void SaveUsers(List<User> users)
        {
            lock (_lock)
            {
                var json = JsonSerializer.Serialize(users, _options);
                File.WriteAllText(_userFile, json);
            }
        }

        public List<Employee> GetEmployees()
        {
            lock (_lock)
            {
                var json = File.ReadAllText(_employeeFile);
                return JsonSerializer.Deserialize<List<Employee>>(json, _options) ?? new List<Employee>();
            }
        }



        public void SaveEmployees(List<Employee> employees)
        {
            lock (_lock)
            {
                var json = JsonSerializer.Serialize(employees, _options);
                File.WriteAllText(_employeeFile, json);
            }
        }

        public List<LeaveRequest> GetLeaveRequests()
        {
            lock (_lock)
            {
                var json = File.ReadAllText(_leaveFile);
                return JsonSerializer.Deserialize<List<LeaveRequest>>(json, _options) ?? new List<LeaveRequest>();
            }
        }

        public void SaveLeaveRequests(List<LeaveRequest> leaves)
        {
            lock (_lock)
            {
                var json = JsonSerializer.Serialize(leaves, _options);
                File.WriteAllText(_leaveFile, json);
            }
        }

        private readonly string _jobPostingsFile = Path.Combine("Data", "job_postings.json");
        private readonly string _applicationsFile = Path.Combine("Data", "applications.json");

        public List<JobPosting> GetJobPostings()
        {
            if (!File.Exists(_jobPostingsFile)) return new List<JobPosting>();
            var json = File.ReadAllText(_jobPostingsFile);
            return JsonSerializer.Deserialize<List<JobPosting>>(json, _options) ?? new List<JobPosting>();
        }

        public void SaveJobPostings(List<JobPosting> jobs)
        {
            var json = JsonSerializer.Serialize(jobs, _options);
            File.WriteAllText(_jobPostingsFile, json);
        }

        public List<JobApplication> GetJobApplications()
        {
            if (!File.Exists(_applicationsFile)) return new List<JobApplication>();
            var json = File.ReadAllText(_applicationsFile);
            return JsonSerializer.Deserialize<List<JobApplication>>(json, _options) ?? new List<JobApplication>();
        }

        public void SaveJobApplications(List<JobApplication> applications)
        {
            var json = JsonSerializer.Serialize(applications, _options);
            File.WriteAllText(_applicationsFile, json);
        }

        private readonly string _meetingsFile = Path.Combine("Data", "meetings.json");

        public List<Meeting> GetMeetings()
        {
            if (!File.Exists(_meetingsFile)) return new List<Meeting>();
            var json = File.ReadAllText(_meetingsFile);
            return JsonSerializer.Deserialize<List<Meeting>>(json, _options) ?? new List<Meeting>();
        }

        public void SaveMeetings(List<Meeting> meetings)
        {
            var json = JsonSerializer.Serialize(meetings, _options);
            File.WriteAllText(_meetingsFile, json);
        }

        public List<Meeting> GetMeetingsByDepartmentHead(int headId)
        {
            return GetMeetings().Where(m => m.DepartmentHeadId == headId).OrderBy(m => m.MeetingDate).ToList();
        }

        public List<Meeting> GetActiveMeetingsForEmployee(int employeeId, int departmentHeadId)
        {
            return GetMeetings()
                .Where(m => m.DepartmentHeadId == departmentHeadId
                            && m.Status == MeetingStatus.Scheduled
                            && m.MeetingDate >= DateTime.Now.Date)
                .OrderBy(m => m.MeetingDate)
                .ToList();
        }

        private readonly string _attendanceFile = Path.Combine("Data", "attendance.json");

        public List<Attendance> GetAttendanceRecords()
        {
            if (!File.Exists(_attendanceFile)) return new List<Attendance>();
            var json = File.ReadAllText(_attendanceFile);
            return JsonSerializer.Deserialize<List<Attendance>>(json, _options) ?? new List<Attendance>();
        }

        public void SaveAttendanceRecords(List<Attendance> records)
        {
            var json = JsonSerializer.Serialize(records, _options);
            File.WriteAllText(_attendanceFile, json);
        }

        public Attendance GetTodayAttendance(int employeeId)
        {
            var today = DateTime.Now.Date;
            var allRecords = GetAttendanceRecords();
            var result = allRecords.FirstOrDefault(a => a.EmployeeId == employeeId && a.Date.Date == today);

            Console.WriteLine($"GetTodayAttendance for employee {employeeId}: {(result != null ? $"Found with status {result.Status}" : "Not found")}");
            return result;
        }

        public List<Attendance> GetLateEmployeesByDepartment(string department, DateTime date)
        {
            var employees = GetEmployees().Where(e => e.Department == department && e.Id != 0).ToList();
            var employeeIds = employees.Select(e => e.Id).ToList();

            return GetAttendanceRecords()
                .Where(a => employeeIds.Contains(a.EmployeeId) && a.Date.Date == date.Date && a.Status == AttendanceStatus.Late)
                .ToList();
        }
    }
}