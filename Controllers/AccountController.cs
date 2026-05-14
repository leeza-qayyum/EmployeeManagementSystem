using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using EmployeeManagementSystem.Models;
using EmployeeManagementSystem.Services;


namespace EmployeeManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly IFileDataService _data;

        public AccountController(IFileDataService data)
        {
            _data = data;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            var user = _data.GetUsers().FirstOrDefault(u => u.Username == username);
            if (user != null && PasswordHasher.VerifyPassword(password, user.Password))
            {
                if (user.EmployeeId.HasValue && user.UserRole != Role.HRManager)
                {
                    bool isDeptHead = (user.UserRole == Role.DepartmentHead);
                    MarkAttendanceOnLogin(user.EmployeeId.Value, isDeptHead);
                }
                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.UserRole.ToString())
        };
                if (user.EmployeeId.HasValue)
                    claims.Add(new Claim("EmployeeId", user.EmployeeId.Value.ToString()));

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                HttpContext.Session.SetString("Username", user.Username);
                HttpContext.Session.SetString("Role", user.UserRole.ToString());
                if (user.EmployeeId.HasValue)
                    HttpContext.Session.SetInt32("EmployeeId", user.EmployeeId.Value);

                return user.UserRole switch
                {
                    Role.HRManager => RedirectToAction("Dashboard", "HR"),
                    Role.DepartmentHead => RedirectToAction("Dashboard", "DepartmentHead"),
                    _ => RedirectToAction("Dashboard", "Employee")
                };
            }
            ViewBag.Error = "Invalid username or password";
            return View();
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ChangePassword(string username, string currentPassword, string newPassword, string confirmPassword)
        {
            var users = _data.GetUsers();
            var user = users.FirstOrDefault(u => u.Username == username);
            if (user == null)
            {
                ModelState.AddModelError("", "Username not found.");
                return View();
            }

            if (!PasswordHasher.VerifyPassword(currentPassword, user.Password))
            {
                ModelState.AddModelError("", "Current password is incorrect.");
                return View();
            }

            // Validate new password
            if (newPassword.Length < 7)
                ModelState.AddModelError("", "Password must be at least 7 characters long.");
            if (!System.Text.RegularExpressions.Regex.IsMatch(newPassword, @"\d"))
                ModelState.AddModelError("", "Password must contain at least one number.");
            if (!System.Text.RegularExpressions.Regex.IsMatch(newPassword, @"[!@#$%^&*(),.?"":{}|<>]"))
                ModelState.AddModelError("", "Password must contain at least one special character.");
            if (newPassword != confirmPassword)
                ModelState.AddModelError("", "New password and confirmation do not match.");

            if (!ModelState.IsValid)
                return View();

            // Update password
            user.Password = PasswordHasher.HashPassword(newPassword);
            _data.SaveUsers(users);

            TempData["Message"] = "Password changed successfully. Please log in with your new password.";
            return RedirectToAction("Login");
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        private void MarkAttendanceOnLogin(int employeeId, bool isDepartmentHead = false)
        {
            var today = DateTime.Now.Date;
            var existingAttendance = _data.GetTodayAttendance(employeeId);

            if (existingAttendance != null)
            {
                Console.WriteLine($"Attendance already exists for employee {employeeId} on {today}");
                return;
            }

            var checkInTime = DateTime.Now;
            var cutoffTime = new TimeSpan(9, 30, 0); // 9:30 AM

            AttendanceStatus status = AttendanceStatus.Present;
            if (!isDepartmentHead && checkInTime.TimeOfDay > cutoffTime)
            {
                status = AttendanceStatus.Late;
                Console.WriteLine($"Employee {employeeId} marked as LATE at {checkInTime}");
            }

            var attendance = new Attendance
            {
                Id = _data.GetAttendanceRecords().Count > 0 ? _data.GetAttendanceRecords().Max(a => a.Id) + 1 : 1,
                EmployeeId = employeeId,
                Date = today,
                CheckInTime = checkInTime,
                Status = status,
                Remarks = status == AttendanceStatus.Late ? "Arrived after 9:30 AM" : "",
                WorkingHours = 0
            };

            var records = _data.GetAttendanceRecords();
            records.Add(attendance);
            _data.SaveAttendanceRecords(records);
            Console.WriteLine($"Attendance saved for employee {employeeId} with status: {status}");
        }
    }
}