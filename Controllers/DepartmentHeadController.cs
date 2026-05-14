using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EmployeeManagementSystem.Models;
using EmployeeManagementSystem.Services;
using System.IO;
using Microsoft.AspNetCore.Http;

namespace EmployeeManagementSystem.Controllers
{
    [Authorize(Roles = "DepartmentHead")]
    public class DepartmentHeadController : Controller
    {
        private readonly IFileDataService _data;
        private readonly IEmailService _emailService;
        public DepartmentHeadController(IFileDataService data, IEmailService emailService) {_data = data; _emailService = emailService;}

        private int GetCurrentEmployeeId() => HttpContext.Session.GetInt32("EmployeeId") ?? 0;

        private string GetHeadDepartment()
        {
            var head = _data.GetEmployees().FirstOrDefault(e => e.Id == GetCurrentEmployeeId());
            return head?.Department;
        }

        public IActionResult Dashboard()
        {
            int headId = GetCurrentEmployeeId();
            var head = _data.GetEmployees().FirstOrDefault(e => e.Id == headId);
            string department = head?.Department ?? "";

            var teamIds = _data.GetEmployees().Where(e => e.Department == department && e.Id != headId).Select(e => e.Id).ToList();
            var pendingLeaves = _data.GetLeaveRequests().Where(l => teamIds.Contains(l.EmployeeId) && l.Status == LeaveStatus.Pending).ToList();

            ViewBag.Department = department;
            ViewBag.TeamCount = teamIds.Count;
            ViewBag.PendingRequests = pendingLeaves.Count;
            ViewBag.PendingLeavesList = pendingLeaves;
            ViewBag.Employees = _data.GetEmployees();

            return View();
        }

        public IActionResult ViewEmployees()
        {
            string dept = GetHeadDepartment();
            var employees = _data.GetEmployees()
                .Where(e => e.Department == dept && e.Id != GetCurrentEmployeeId() && e.Id != 0)
                .ToList();
            return View(employees);
        }

        public IActionResult ViewProfile()
        {
            int empId = GetCurrentEmployeeId();
            var employee = _data.GetEmployees().FirstOrDefault(e => e.Id == empId);
            if (employee == null)
            {
                return NotFound();
            }
            return View(employee);
        }

        [HttpGet]
        public IActionResult EditProfile()
        {
            try
            {
                int employeeId = GetCurrentEmployeeId();
                var employee = _data.GetEmployees().FirstOrDefault(e => e.Id == employeeId);
                if (employee == null)
                {
                    TempData["Error"] = "Profile not found.";
                    return RedirectToAction("Dashboard");
                }
                return View(employee);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Dashboard");
            }
        }

        [HttpPost]
        public async Task<IActionResult> EditProfile(Employee model, IFormFile? profileImage)
        {
            try
            {
                int employeeId = GetCurrentEmployeeId();
                var employees = _data.GetEmployees();
                var employee = employees.FirstOrDefault(e => e.Id == employeeId);

                if (employee == null)
                {
                    TempData["Error"] = "Profile not found.";
                    return RedirectToAction("Dashboard");
                }

                // Preserve read-only values
                model.Name = employee.Name;
                model.Department = employee.Department;
                model.Salary = employee.Salary;
                model.LeaveBalance = employee.LeaveBalance;
                model.Id = employeeId;
                model.ProfilePicturePath = employee.ProfilePicturePath;

                // Validate
                bool hasError = false;
                if (!System.Text.RegularExpressions.Regex.IsMatch(model.Email, @"^[^@\s]+@gmail\.com$"))
                {
                    ModelState.AddModelError("Email", "Email must end with @gmail.com");
                    hasError = true;
                }

                if (!System.Text.RegularExpressions.Regex.IsMatch(model.Phone, @"^\d{11}$"))
                {
                    ModelState.AddModelError("Phone", "Phone number must be exactly 11 numeric digits");
                    hasError = true;
                }

                if (hasError)
                {
                    return View(model);
                }

                // Update fields
                employee.Email = model.Email;
                employee.Phone = model.Phone;

                // Handle profile picture
                if (profileImage != null && profileImage.Length > 0)
                {
                    string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    string fileName = $"depthead_{employeeId}_{DateTime.Now.Ticks}_{Path.GetFileName(profileImage.FileName)}";
                    string filePath = Path.Combine(uploadsFolder, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await profileImage.CopyToAsync(stream);
                    }
                    employee.ProfilePicturePath = $"/uploads/{fileName}";
                }

                // Save
                var index = employees.FindIndex(e => e.Id == employeeId);
                employees[index] = employee;
                _data.SaveEmployees(employees);

                TempData["Message"] = "Profile updated successfully!";
                return RedirectToAction("ViewProfile");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("EditProfile");
            }
        }
        public IActionResult ManageLeaves()
        {
            string dept = GetHeadDepartment();
            var empIds = _data.GetEmployees().Where(e => e.Department == dept).Select(e => e.Id).ToList();
            var pendingLeaves = _data.GetLeaveRequests().Where(l => empIds.Contains(l.EmployeeId) && l.Status == LeaveStatus.Pending).ToList();
            ViewBag.Employees = _data.GetEmployees(); // for display
            return View(pendingLeaves);
        }

        [HttpPost]
        [HttpPost]
        public IActionResult ApproveLeave(int leaveId)
        {
            var leaves = _data.GetLeaveRequests();
            var leave = leaves.FirstOrDefault(l => l.Id == leaveId);

            if (leave != null && leave.Status == LeaveStatus.Pending)
            {
                // Calculate number of days requested
                int daysRequested = (leave.EndDate - leave.StartDate).Days + 1;

                // Get the employee and deduct days from leave balance
                var employees = _data.GetEmployees();
                var employee = employees.FirstOrDefault(e => e.Id == leave.EmployeeId);

                if (employee != null && employee.LeaveBalance >= daysRequested)
                {
                    employee.LeaveBalance -= daysRequested;
                    _data.SaveEmployees(employees);

                    // Update leave status
                    leave.Status = LeaveStatus.Approved;
                    _data.SaveLeaveRequests(leaves);

                    TempData["Message"] = $"Leave approved. {daysRequested} day(s) deducted from {employee.Name}'s leave balance.";
                }
                else if (employee != null && employee.LeaveBalance < daysRequested)
                {
                    TempData["Error"] = $"Cannot approve: {employee.Name} has only {employee.LeaveBalance} day(s) remaining.";
                }
            }

            return RedirectToAction("ManageLeaves");
        }

       
        [HttpPost]
        public IActionResult RejectLeave(int leaveId)
        {
            var leaves = _data.GetLeaveRequests();
            var leave = leaves.FirstOrDefault(l => l.Id == leaveId);

            if (leave != null && leave.Status == LeaveStatus.Pending)
            {
                leave.Status = LeaveStatus.Rejected;
                _data.SaveLeaveRequests(leaves);
                TempData["Message"] = "Leave request rejected.";
            }

            return RedirectToAction("ManageLeaves");
        }

        [HttpGet]
        public IActionResult Meetings()
        {
            int headId = GetCurrentEmployeeId();
            var allMeetings = _data.GetMeetings();

            var meetings = allMeetings
                .Where(m => m.DepartmentHeadId == headId)
                .OrderByDescending(m => m.MeetingDate)
                .ToList();

            return View(meetings);
        }
        [HttpGet]
        public IActionResult ScheduleMeeting()
        {
            return View();
        }

        [HttpGet]
        public IActionResult RescheduleMeeting(int id)
        {
            var meeting = _data.GetMeetings().FirstOrDefault(m => m.Id == id);
            if (meeting == null)
                return NotFound();
            return View(meeting);
        }

        [HttpPost]
        public async Task<IActionResult> ScheduleMeeting(Meeting meeting)
        {
            int headId = GetCurrentEmployeeId();
            var head = _data.GetEmployees().FirstOrDefault(e => e.Id == headId);

            if (head == null)
                return NotFound();

            meeting.Id = _data.GetMeetings().Count > 0 ? _data.GetMeetings().Max(m => m.Id) + 1 : 1;
            meeting.DepartmentHeadId = headId;
            meeting.Status = MeetingStatus.Scheduled;
            meeting.CreatedDate = DateTime.Now;

            var meetings = _data.GetMeetings();
            meetings.Add(meeting);
            _data.SaveMeetings(meetings);

            // Get all employees in department
            var teamMembers = _data.GetEmployees().Where(e => e.Department == head.Department && e.Id != headId).ToList();

            // Build email body as a single string
            string meetingLinkHtml = string.IsNullOrEmpty(meeting.MeetingLink)
                ? ""
                : $"<p><strong>Meeting Link:</strong> <a href='{meeting.MeetingLink}'>{meeting.MeetingLink}</a></p>";

            // Send email notifications to all team members
            foreach (var member in teamMembers)
            {
                try
                {
                    string subject = $"New Meeting Scheduled: {meeting.Title}";
                    string body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2 style='color: #2C3E8F;'>New Meeting Scheduled</h2>
                    <p>Dear {member.Name},</p>
                    <p>Your Department Head has scheduled a meeting:</p>
                    <p><strong>Title:</strong> {meeting.Title}</p>
                    <p><strong>Date:</strong> {meeting.MeetingDate.ToShortDateString()}</p>
                    <p><strong>Time:</strong> {meeting.StartTime} - {meeting.EndTime}</p>
                    <p><strong>Location:</strong> {meeting.Location}</p>
                    {meetingLinkHtml}
                    <p><strong>Description:</strong> {meeting.Description}</p>
                    <br />
                    <p>Please mark your calendar.</p>
                    <p>Best regards,<br />{head.Name}</p>
                </body>
                </html>
            ";
                    await _emailService.SendEmailAsync(member.Email, subject, body);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Email failed for {member.Email}: {ex.Message}");
                }
            }

            TempData["Message"] = $"Meeting scheduled and notifications sent to {teamMembers.Count} employee(s).";
            return RedirectToAction("Meetings");
        }

        [HttpPost]
        public async Task<IActionResult> RescheduleMeeting(int id, DateTime newDate, TimeSpan newStartTime, TimeSpan newEndTime, string reason)
        {
            var meetings = _data.GetMeetings();
            var meeting = meetings.FirstOrDefault(m => m.Id == id);

            if (meeting == null)
                return NotFound();

            int headId = GetCurrentEmployeeId();
            var head = _data.GetEmployees().FirstOrDefault(e => e.Id == headId);

            // Store original date for reference
            var originalDate = meeting.MeetingDate;
            var originalStart = meeting.StartTime;

            // Update meeting
            meeting.RescheduledFrom = meeting.MeetingDate;
            meeting.MeetingDate = newDate;
            meeting.StartTime = newStartTime;
            meeting.EndTime = newEndTime;
            meeting.RescheduleReason = reason;
            meeting.Status = MeetingStatus.Rescheduled;

            _data.SaveMeetings(meetings);

            // Build meeting link HTML
            string meetingLinkHtml = string.IsNullOrEmpty(meeting.MeetingLink)
                ? ""
                : $"<p><strong>Meeting Link:</strong> <a href='{meeting.MeetingLink}'>{meeting.MeetingLink}</a></p>";

            // Notify employees about reschedule
            var teamMembers = _data.GetEmployees().Where(e => e.Department == head.Department && e.Id != headId).ToList();

            foreach (var member in teamMembers)
            {
                try
                {
                    string subject = $"Meeting Rescheduled: {meeting.Title}";
                    string body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2 style='color: #F5A623;'>Meeting Rescheduled</h2>
                    <p>Dear {member.Name},</p>
                    <p>The following meeting has been rescheduled:</p>
                    <p><strong>Title:</strong> {meeting.Title}</p>
                    <p><strong>Original Date/Time:</strong> {originalDate.ToShortDateString()} at {originalStart}</p>
                    <p><strong>New Date/Time:</strong> {newDate.ToShortDateString()} at {newStartTime} - {newEndTime}</p>
                    <p><strong>Reason:</strong> {reason}</p>
                    <p><strong>Location:</strong> {meeting.Location}</p>
                    {meetingLinkHtml}
                    <br />
                    <p>Please update your calendar.</p>
                    <p>Best regards,<br />{head.Name}</p>
                </body>
                </html>
            ";
                    await _emailService.SendEmailAsync(member.Email, subject, body);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Email failed for {member.Email}: {ex.Message}");
                }
            }

            TempData["Message"] = $"Meeting rescheduled and notifications sent to {teamMembers.Count} employee(s).";
            return RedirectToAction("Meetings");
        }

        [HttpPost]
        public IActionResult MarkMeetingDone(int id)
        {
            var meetings = _data.GetMeetings();
            var meeting = meetings.FirstOrDefault(m => m.Id == id);

            if (meeting != null)
            {
                meeting.Status = MeetingStatus.Completed;
                _data.SaveMeetings(meetings);
                TempData["Message"] = $"Meeting '{meeting.Title}' marked as completed.";
            }
            else
            {
                TempData["Error"] = "Meeting not found.";
            }

            return RedirectToAction("Meetings");
        }

        [HttpGet]
        public IActionResult MonthlyAttendance(int? year, int? month)
        {
            int headId = GetCurrentEmployeeId();
            var head = _data.GetEmployees().FirstOrDefault(e => e.Id == headId);

            if (head == null)
                return NotFound();

            // Default to current month if no selection
            int selectedYear = year ?? DateTime.Now.Year;
            int selectedMonth = month ?? DateTime.Now.Month;

            // Get all employees in department
            var departmentEmployees = _data.GetEmployees()
                .Where(e => e.Department == head.Department && e.Id != headId)
                .ToList();

            var employeeIds = departmentEmployees.Select(e => e.Id).ToList();

            // Get attendance records for department employees
            var allAttendance = _data.GetAttendanceRecords()
                .Where(a => employeeIds.Contains(a.EmployeeId))
                .ToList();

            // Filter by selected month/year
            var monthlyRecords = allAttendance
                .Where(a => a.Date.Year == selectedYear && a.Date.Month == selectedMonth)
                .OrderBy(a => a.Date)
                .ToList();

            // Calculate statistics
            int totalDays = DateTime.DaysInMonth(selectedYear, selectedMonth);
            int totalEmployees = departmentEmployees.Count;
            int totalPresent = monthlyRecords.Count(a => a.Status == AttendanceStatus.Present);
            int totalLate = monthlyRecords.Count(a => a.Status == AttendanceStatus.Late);
            int attendedDays = totalPresent + totalLate;

            // Get late employees names as simple List<string>
            var lateEmployeeIds = monthlyRecords
                .Where(a => a.Status == AttendanceStatus.Late)
                .Select(a => a.EmployeeId)
                .Distinct()
                .ToList();

            var lateEmployeesNames = departmentEmployees
                .Where(e => lateEmployeeIds.Contains(e.Id))
                .Select(e => e.Name)
                .ToList();

            ViewBag.Year = selectedYear;
            ViewBag.Month = selectedMonth;
            ViewBag.MonthName = new DateTime(selectedYear, selectedMonth, 1).ToString("MMMM yyyy");
            ViewBag.Department = head.Department;
            ViewBag.TotalEmployees = totalEmployees;
            ViewBag.TotalDays = totalDays;
            ViewBag.TotalPresent = totalPresent;
            ViewBag.TotalLate = totalLate;
            ViewBag.AttendedDays = attendedDays;
            ViewBag.LateEmployeesCount = lateEmployeesNames.Count;
            ViewBag.LateEmployeesList = lateEmployeesNames;  // ✅ Now a List<string>
            ViewBag.Employees = _data.GetEmployees().ToList();

            return View(monthlyRecords);
        }



    }
}