using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EmployeeManagementSystem.Models;
using EmployeeManagementSystem.Services;
using System.IO;
using Microsoft.AspNetCore.Http;

namespace EmployeeManagementSystem.Controllers
{
    [Authorize(Roles = "Employee")]
    public class EmployeeController : Controller
    {
        private readonly IFileDataService _data;
        private readonly IEmailService _emailService;
        public EmployeeController(IFileDataService data, IEmailService emailService) { 
            _data = data;
            _emailService = emailService;
        }

        private int GetCurrentEmployeeId()
        {
            return HttpContext.Session.GetInt32("EmployeeId") ?? 0;
        }

        public IActionResult Dashboard()
        {
            int employeeId = GetCurrentEmployeeId();
            var employee = _data.GetEmployees().FirstOrDefault(e => e.Id == employeeId);
            var leaves = _data.GetLeaveRequests().Where(l => l.EmployeeId == employeeId).ToList();

            ViewBag.LeaveBalance = employee?.LeaveBalance ?? 20;
            ViewBag.PendingLeaves = leaves.Count(l => l.Status == LeaveStatus.Pending);
            ViewBag.ApprovedLeaves = leaves.Count(l => l.Status == LeaveStatus.Approved);
            ViewBag.RejectedLeaves = leaves.Count(l => l.Status == LeaveStatus.Rejected);
            ViewBag.RecentLeaves = leaves.OrderByDescending(l => l.AppliedDate).Take(5).ToList();

            return View();
        }
        public IActionResult ViewProfile()
        {
            var emp = _data.GetEmployees().FirstOrDefault(e => e.Id == GetCurrentEmployeeId());
            return View(emp);
        }


        [HttpGet]
        public IActionResult ApplyLeave() => View();

        [HttpPost]
        public IActionResult ApplyLeave(LeaveRequest leave)
        {
            int employeeId = GetCurrentEmployeeId();

            // Check for any pending leave requests
            var leaves = _data.GetLeaveRequests();
            bool hasPending = leaves.Any(l => l.EmployeeId == employeeId && l.Status == LeaveStatus.Pending);

            if (hasPending)
            {
                TempData["Error"] = "You already have a pending leave request. You cannot apply for another until it is approved or rejected.";
                return RedirectToAction("ApplyLeave");
            }

            // Validate date range (optional but good)
            if (leave.EndDate < leave.StartDate)
            {
                TempData["Error"] = "End date cannot be before start date.";
                return RedirectToAction("ApplyLeave");
            }

            // Proceed to save leave
            leave.Id = leaves.Count > 0 ? leaves.Max(l => l.Id) + 1 : 1;
            leave.EmployeeId = employeeId;
            leave.Status = LeaveStatus.Pending;
            leave.AppliedDate = DateTime.Now;
            leaves.Add(leave);
            _data.SaveLeaveRequests(leaves);

            TempData["Message"] = "Leave request submitted successfully.";
            return RedirectToAction("Dashboard");
        }

        public IActionResult LeaveHistory()
        {
            var leaves = _data.GetLeaveRequests().Where(l => l.EmployeeId == GetCurrentEmployeeId()).OrderByDescending(l => l.AppliedDate).ToList();
            return View(leaves);
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
                    TempData["Error"] = "Employee not found.";
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
                    TempData["Error"] = "Employee not found.";
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

                    string fileName = $"emp_{employeeId}_{DateTime.Now.Ticks}_{Path.GetFileName(profileImage.FileName)}";
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
        [HttpGet]
        public IActionResult Meetings()
        {
            int employeeId = GetCurrentEmployeeId();
            var employee = _data.GetEmployees().FirstOrDefault(e => e.Id == employeeId);

            if (employee == null)
                return NotFound();

            // Find department head for this employee's department
            var departmentHead = _data.GetUsers()
                .FirstOrDefault(u => u.UserRole == Role.DepartmentHead && u.EmployeeId.HasValue);

            if (departmentHead?.EmployeeId == null)
            {
                ViewBag.Message = "No department head assigned yet.";
                return View(new List<Meeting>());
            }

            // Get ALL meetings for this department (including rescheduled)
            var allMeetings = _data.GetMeetings();
            var meetings = allMeetings
                .Where(m => m.DepartmentHeadId == departmentHead.EmployeeId.Value)
                .OrderBy(m => m.MeetingDate)
                .ToList();

            return View(meetings);
        }

       

        [HttpGet]
        public IActionResult MonthlyAttendance(int? year, int? month)
        {
            int employeeId = GetCurrentEmployeeId();

            // Default to current month if no selection
            int selectedYear = year ?? DateTime.Now.Year;
            int selectedMonth = month ?? DateTime.Now.Month;

            var allAttendance = _data.GetAttendanceRecords()
                .Where(a => a.EmployeeId == employeeId)
                .ToList();

            // Filter by selected month/year
            var monthlyRecords = allAttendance
                .Where(a => a.Date.Year == selectedYear && a.Date.Month == selectedMonth)
                .OrderBy(a => a.Date)
                .ToList();

            // Calculate statistics
            int totalDays = DateTime.DaysInMonth(selectedYear, selectedMonth);

            // ✅ Count both Present AND Late as "attended"
            int attendedDays = monthlyRecords.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late);
            int lateDays = monthlyRecords.Count(a => a.Status == AttendanceStatus.Late);
            int presentDays = monthlyRecords.Count(a => a.Status == AttendanceStatus.Present);
            int absentDays = totalDays - attendedDays;
            int halfDays = monthlyRecords.Count(a => a.Status == AttendanceStatus.HalfDay);

            ViewBag.Year = selectedYear;
            ViewBag.Month = selectedMonth;
            ViewBag.MonthName = new DateTime(selectedYear, selectedMonth, 1).ToString("MMMM yyyy");
            ViewBag.TotalDays = totalDays;
            ViewBag.PresentDays = presentDays;
            ViewBag.LateDays = lateDays;
            ViewBag.AttendedDays = attendedDays;  // ✅ New: Present + Late
            ViewBag.AbsentDays = absentDays;
            ViewBag.HalfDays = halfDays;
            ViewBag.AttendancePercentage = totalDays > 0 ? (int)((double)attendedDays / totalDays * 100) : 0;

            return View(monthlyRecords);
        }

    }
}