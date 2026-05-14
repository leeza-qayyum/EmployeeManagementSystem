using System;
using System.IO;
using System.Linq;
using EmployeeManagementSystem.Models;
using EmployeeManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace EmployeeManagementSystem.Controllers
{
   
    [Authorize(Roles = "HRManager")]
    public class HRController : Controller
    {
        private readonly IFileDataService _data;
        private readonly IEmailService _emailService;
        private readonly ICVAnalyzer _cvAnalyzer;           // Add this
        private readonly IWebHostEnvironment _environment;

        public HRController(IFileDataService data, IEmailService emailService)
        {
            _data = data;
            _emailService = emailService;
        }


        public IActionResult Dashboard()
        {
            var employees = _data.GetEmployees().Where(e => e.Id != 0).ToList();
            var jobs = _data.GetJobPostings();
            var applications = _data.GetJobApplications();

            ViewBag.TotalEmployees = employees.Count();
            ViewBag.TotalDepartments = employees.Select(e => e.Department).Distinct().Count();
            ViewBag.TotalHeads = _data.GetUsers().Count(u => u.UserRole == Role.DepartmentHead);
            ViewBag.ActiveJobs = jobs.Count(j => j.IsActive);
            ViewBag.RecentJobs = jobs.OrderByDescending(j => j.PostedDate).Take(5).ToList();
            ViewBag.RecentApplications = applications.OrderByDescending(a => a.AppliedDate).Take(5).ToList();
            ViewBag.AllJobs = jobs;

            return View();
        }
        public IActionResult ViewProfile()
        {
            var username = User.Identity.Name ?? HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
                return RedirectToAction("Login", "Account");

            var user = _data.GetUsers().FirstOrDefault(u => u.Username == username);
            if (user == null)
                return NotFound();

            // Ensure there is an employee record with ID = 0 (for HR)
            var employees = _data.GetEmployees();
            var hrEmployee = employees.FirstOrDefault(e => e.Id == 0);
            if (hrEmployee == null)
            {
                hrEmployee = new Employee
                {
                    Id = 0,
                    Name = "HR Admin",
                    Email = "hr@company.com",
                    Department = "Human Resources",
                    Phone = "00000000000",
                    Salary = 75000
                };
                employees.Add(hrEmployee);
                _data.SaveEmployees(employees);
            }

            // Link the logged-in admin user to this employee if not already linked
            if (user.EmployeeId != 0)
            {
                user.EmployeeId = 0;
                var allUsers = _data.GetUsers();
                var index = allUsers.FindIndex(u => u.Username == user.Username);
                if (index != -1)
                {
                    allUsers[index].EmployeeId = 0;
                    _data.SaveUsers(allUsers);
                }
            }

            // Retrieve the employee profile
            var profile = _data.GetEmployees().FirstOrDefault(e => e.Id == 0);
            if (profile == null)
                return NotFound();

            return View(profile);
        }

        [HttpGet]
        public IActionResult EditProfile()
        {
            var username = User.Identity.Name ?? HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
                return RedirectToAction("Login", "Account");

            var user = _data.GetUsers().FirstOrDefault(u => u.Username == username);
            if (user == null || user.EmployeeId != 0)
                return NotFound();

            var employee = _data.GetEmployees().FirstOrDefault(e => e.Id == 0);
            if (employee == null)
                return NotFound();

            return View(employee);
        }

        [HttpPost]
        public async Task<IActionResult> EditProfile(Employee emp, IFormFile? profileImage)
        {
            if (!ModelState.IsValid)
                return View(emp);

            var employees = _data.GetEmployees();
            var index = employees.FindIndex(e => e.Id == 0);
            if (index == -1)
                return NotFound();

            // Handle profile picture upload
            if (profileImage != null && profileImage.Length > 0)
            {
                // Delete old image if exists
                string oldPath = employees[index].ProfilePicturePath;
                if (!string.IsNullOrEmpty(oldPath))
                {
                    string fullOldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", oldPath.TrimStart('/'));
                    if (System.IO.File.Exists(fullOldPath))
                        System.IO.File.Delete(fullOldPath);
                }

                // Save new image
                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string fileName = $"hr_{DateTime.Now.Ticks}_{Path.GetFileName(profileImage.FileName)}";
                string filePath = Path.Combine(uploadsFolder, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await profileImage.CopyToAsync(stream);
                }
                emp.ProfilePicturePath = $"/uploads/{fileName}";
            }
            else
            {
                // Keep existing picture
                emp.ProfilePicturePath = employees[index].ProfilePicturePath;
            }

            // Preserve Id = 0
            emp.Id = 0;
            employees[index] = emp;
            _data.SaveEmployees(employees);

            TempData["Message"] = "Your profile has been updated successfully.";
            return RedirectToAction("ViewProfile");
        }

        // Manage Employees
        public IActionResult ManageEmployees()
        {
            var employees = _data.GetEmployees().Where(e => e.Id != 0).ToList();
            return View(employees);
        }

        [HttpGet]
        public IActionResult AddEmployee() => View();

        [HttpPost]
        public async Task<IActionResult> AddEmployee(Employee emp, IFormFile? profileImage)
        {
            if (!ModelState.IsValid)
                return View(emp);

            var employees = _data.GetEmployees();
            emp.Id = employees.Count > 0 ? employees.Max(e => e.Id) + 1 : 1;
            emp.LeaveBalance = 20;
            // Handle profile picture upload (unchanged)
            if (profileImage != null && profileImage.Length > 0)
            {
                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string fileName = $"emp_{emp.Id}_{Path.GetFileName(profileImage.FileName)}";
                string filePath = Path.Combine(uploadsFolder, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await profileImage.CopyToAsync(stream);
                }
                emp.ProfilePicturePath = $"/uploads/{fileName}";
            }

            employees.Add(emp);
            _data.SaveEmployees(employees);

            // Generate random password and create user account
            string plainPassword = GenerateRandomPassword();
            string hashedPassword = PasswordHasher.HashPassword(plainPassword);
            var users = _data.GetUsers();
            string username = emp.Email.Split('@')[0];
            if (!users.Any(u => u.EmployeeId == emp.Id))
            {
                users.Add(new User
                {
                    Username = username,
                    Password = hashedPassword,
                    UserRole = Role.Employee,
                    EmployeeId = emp.Id
                });
                _data.SaveUsers(users);
            }

            // ==================== START OF EMAIL SENDING ====================
            try
            {
                string subject = "WILK - Login Credentials";
                string body = $@"
            <h3>Welcome to WILK</h3>
            <p>Your login credentials to the system are created.</p>
            <p><strong>Username:</strong> {username}<br />
            <strong>Password:</strong> {plainPassword}</p>
            <p>Please log in and change your password after first login.</p>
            <p>Thank you.</p>
        ";
                await _emailService.SendEmailAsync(emp.Email, subject, body);
                Console.WriteLine($"Email sent to {emp.Email}");
            }
            catch (Exception ex)
            {
                // Log error but don't break the flow (optional: you can store in a log file)
                System.IO.File.AppendAllText("email_errors.log", $"{DateTime.Now}: {ex.ToString()}\n");
                Console.WriteLine($"Email failed to send: {ex.Message}");
            }
            // ==================== END OF EMAIL SENDING ====================

            TempData["Message"] = $"Employee added. Username: {username}, Password: {plainPassword}";
            return RedirectToAction("ManageEmployees");
        }

        [HttpGet]
        public IActionResult EditEmployee(int id)
        {
            var emp = _data.GetEmployees().FirstOrDefault(e => e.Id == id);
            if (emp == null) return NotFound();
            return View(emp);
        }

        [HttpPost]
        public async Task<IActionResult> EditEmployee(Employee emp, IFormFile? profileImage)
        {
            if (!ModelState.IsValid)
                return View(emp);

            var employees = _data.GetEmployees();
            var index = employees.FindIndex(e => e.Id == emp.Id);
            if (index == -1)
                return NotFound();

            // Handle new profile picture
            if (profileImage != null && profileImage.Length > 0)
            {
                // Delete old image if exists
                string oldPath = employees[index].ProfilePicturePath;
                if (!string.IsNullOrEmpty(oldPath))
                {
                    string fullOldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", oldPath.TrimStart('/'));
                    if (System.IO.File.Exists(fullOldPath))
                        System.IO.File.Delete(fullOldPath);
                }

                // Save new image
                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string fileName = $"emp_{emp.Id}_{Path.GetFileName(profileImage.FileName)}";
                string filePath = Path.Combine(uploadsFolder, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await profileImage.CopyToAsync(stream);
                }
                emp.ProfilePicturePath = $"/uploads/{fileName}";
            }
            else
            {
                // Keep existing picture
                emp.ProfilePicturePath = employees[index].ProfilePicturePath;
            }

            employees[index] = emp;
            _data.SaveEmployees(employees);
            TempData["Message"] = "Employee updated successfully.";
            return RedirectToAction("ManageEmployees");
        }


        public IActionResult DeleteEmployee(int id)
        {
            // Check if the employee is a Department Head
            var users = _data.GetUsers();
            var isDeptHead = users.Any(u => u.EmployeeId == id && u.UserRole == Role.DepartmentHead);

            if (isDeptHead)
            {
                TempData["Error"] = "Cannot delete employee who is a Department Head. Please appoint a new Department Head first.";
                return RedirectToAction("ManageEmployees");
            }

            // Proceed with deletion
            var employees = _data.GetEmployees();
            employees.RemoveAll(e => e.Id == id);
            _data.SaveEmployees(employees);

            // Remove user account if exists
            users.RemoveAll(u => u.EmployeeId == id);
            _data.SaveUsers(users);

            TempData["Message"] = "Employee deleted successfully.";
            return RedirectToAction("ManageEmployees");

            
        }


        // GET: Show list of distinct departments
        [HttpGet]
        public IActionResult AppointHead()
        {
            var employees = _data.GetEmployees();
            var departments = employees
            .Where(e => e.Id != 0)                     // exclude HR manager
            .Select(e => e.Department)
            .Distinct()
            .ToList();
            ViewBag.Departments = departments;
            return View("SelectDepartment");
        }

        // POST: Department selected – show employees of that department
        [HttpPost]
        [HttpPost]
        public IActionResult AppointHead(string department)
        {
            var employees = _data.GetEmployees()
                .Where(e => e.Department == department && e.Id != 0)
                .ToList();
            ViewBag.Department = department;
            return View("SelectEmployee", employees);
        }

        [HttpPost]
        public IActionResult ConfirmAppoint(int employeeId, string department)
        {
            var users = _data.GetUsers();
            var allEmployees = _data.GetEmployees();

            // Check if selected employee is already a Department Head
            var selectedUser = users.FirstOrDefault(u => u.EmployeeId == employeeId);
            if (selectedUser != null && selectedUser.UserRole == Role.DepartmentHead)
            {
                TempData["Error"] = $"Employee '{selectedUser.Username}' is already a Department Head. Cannot appoint again.";
                // Redirect back to the employee selection page for the same department
                var employees = _data.GetEmployees().Where(e => e.Department == department).ToList();
                ViewBag.Department = department;
                return View("SelectEmployee", employees);
            }

            // Find current Department Head of the same department
            var currentHeadUser = users.FirstOrDefault(u => u.UserRole == Role.DepartmentHead &&
                                                            u.EmployeeId.HasValue &&
                                                            allEmployees.FirstOrDefault(e => e.Id == u.EmployeeId.Value)?.Department == department);

            // Demote current head (if exists) to Employee role
            if (currentHeadUser != null)
            {
                currentHeadUser.UserRole = Role.Employee;
            }

            // Promote the new head
            if (selectedUser != null)
            {
                selectedUser.UserRole = Role.DepartmentHead;
                _data.SaveUsers(users);
                TempData["Message"] = $"Employee {selectedUser.Username} is now the Department Head of {department}.";
            }
            else
            {
                TempData["Error"] = "Employee not found in user accounts.";
            }

            return RedirectToAction("AppointHead");
        }
        private static string GenerateRandomPassword(int length = 8)
        {
            const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*";
            var random = new Random();
            return new string(Enumerable.Repeat(validChars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        // ==================== JOB POSTING & RECRUITMENT ====================

        [HttpGet]
        public IActionResult CreateJobPosting()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateJobPosting(JobPosting job)
        {
            var jobs = _data.GetJobPostings();
            job.Id = jobs.Count > 0 ? jobs.Max(j => j.Id) + 1 : 1;
            job.PostedDate = DateTime.Now;
            job.IsActive = true;

            // Generate unique form link (points to your app)
            string baseUrl = $"{Request.Scheme}://{Request.Host}";
            job.GoogleFormLink = $"{baseUrl}/Public/Apply?jobId={job.Id}";

            jobs.Add(job);
            _data.SaveJobPostings(jobs);

            TempData["Message"] = $"Job posting created! Share this link with candidates: {job.GoogleFormLink}";
            return RedirectToAction("ManageJobPostings");
        }

        [HttpGet]
        public IActionResult ManageJobPostings()
        {
            var jobs = _data.GetJobPostings().OrderByDescending(j => j.PostedDate).ToList();
            return View(jobs);
        }

        [HttpPost]
        public IActionResult CloseJobPosting(int id)
        {
            var jobs = _data.GetJobPostings();
            var job = jobs.FirstOrDefault(j => j.Id == id);
            if (job != null)
            {
                job.IsActive = false;
                _data.SaveJobPostings(jobs);
                TempData["Message"] = "Job posting closed.";
            }
            return RedirectToAction("ManageJobPostings");
        }

        // Public facing - candidates apply here
        [HttpGet]
        public IActionResult ApplyForJob(int jobId)
        {
            var job = _data.GetJobPostings().FirstOrDefault(j => j.Id == jobId);
            if (job == null || !job.IsActive)
            {
                TempData["Error"] = "This job posting is no longer active.";
                return RedirectToAction("Index", "Home");
            }
            ViewBag.Job = job;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ApplyForJob(int jobId, string fullName, string email, string phone, IFormFile cvFile)
        {
            var job = _data.GetJobPostings().FirstOrDefault(j => j.Id == jobId);
            if (job == null || !job.IsActive)
            {
                TempData["Error"] = "This job posting is no longer active.";
                return RedirectToAction("Index", "Home");
            }

            if (cvFile == null || cvFile.Length == 0)
            {
                TempData["Error"] = "Please upload your CV.";
                return RedirectToAction("ApplyForJob", new { jobId });
            }

            // Save CV
            string uploadsFolder = Path.Combine(_environment.WebRootPath, "cv_uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            string fileName = $"{DateTime.Now.Ticks}_{Path.GetFileName(cvFile.FileName)}";
            string filePath = Path.Combine(uploadsFolder, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await cvFile.CopyToAsync(stream);
            }

            // Analyze CV
            // Analyze CV - pass required skills for extraction
            string cvText = _cvAnalyzer.ExtractTextFromCV(filePath);
            string extractedSkills = _cvAnalyzer.ExtractSkills(cvText, job.RequiredSkills);
            int matchScore = _cvAnalyzer.CalculateMatchScore(extractedSkills, job.RequiredSkills);

            // Save application
            var applications = _data.GetJobApplications();
            var application = new JobApplication
            {
                Id = applications.Count > 0 ? applications.Max(a => a.Id) + 1 : 1,
                JobPostingId = jobId,
                CandidateName = fullName,
                Email = email,
                Phone = phone,
                CVFilePath = $"/cv_uploads/{fileName}",
                ExtractedSkills = extractedSkills,
                MatchScore = matchScore,
                IsShortlisted = false,
                AppliedDate = DateTime.Now,
                Status = "Pending"
            };
            applications.Add(application);
            _data.SaveJobApplications(applications);

            // Auto-shortlist top 3 for this job
            var jobApps = applications.Where(a => a.JobPostingId == jobId && a.Status == "Pending")
                .OrderByDescending(a => a.MatchScore).ToList();

            for (int i = 0; i < jobApps.Count && i < 3; i++)
            {
                jobApps[i].IsShortlisted = true;
                jobApps[i].Status = "Shortlisted";
            }
            _data.SaveJobApplications(applications);

            // Notify HR
            try
            {
                string hrEmail = "hr@company.com"; // Get from settings
                string subject = $"New Application for {job.Title}";
                string body = $@"
            <h3>New Job Application</h3>
            <p><strong>Position:</strong> {job.Title}</p>
            <p><strong>Candidate:</strong> {fullName}</p>
            <p><strong>Email:</strong> {email}</p>
            <p><strong>Match Score:</strong> {matchScore}%</p>
            <p><a href='{Request.Scheme}://{Request.Host}/HR/ViewApplications?jobId={jobId}'>Review Applications</a></p>
        ";
                await _emailService.SendEmailAsync(hrEmail, subject, body);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email notification failed: {ex.Message}");
            }

            TempData["Message"] = "Application submitted successfully! We'll contact you soon.";
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult ViewApplications(int jobId)
        {
            var job = _data.GetJobPostings().FirstOrDefault(j => j.Id == jobId);
            if (job == null)
                return NotFound();

            var applications = _data.GetJobApplications()
                .Where(a => a.JobPostingId == jobId)
                .OrderByDescending(a => a.MatchScore)
                .ToList();

            ViewBag.Job = job;
            return View(applications);
        }

        [HttpGet]
        public IActionResult ShortlistedCandidates(int jobId)
        {
            var job = _data.GetJobPostings().FirstOrDefault(j => j.Id == jobId);
            if (job == null)
                return NotFound();

            // Only show candidates that are shortlisted AND not rejected
            var shortlisted = _data.GetJobApplications()
                .Where(a => a.JobPostingId == jobId && a.IsShortlisted && a.Status != "Rejected")
                .OrderByDescending(a => a.MatchScore)
                .ToList();

            ViewBag.Job = job;
            ViewBag.HasRejected = _data.GetJobApplications().Any(a => a.JobPostingId == jobId && a.Status == "Rejected");
            return View(shortlisted);
        }

        [HttpPost]
        public async Task<IActionResult> HireCandidate(int applicationId)
        {
            var application = _data.GetJobApplications().FirstOrDefault(a => a.Id == applicationId);
            if (application == null)
            {
                TempData["Error"] = "Application not found.";
                return RedirectToAction("ManageJobPostings");
            }

            var job = _data.GetJobPostings().FirstOrDefault(j => j.Id == application.JobPostingId);

            // Create employee
            var employees = _data.GetEmployees();
            int newId = employees.Count > 0 ? employees.Max(e => e.Id) + 1 : 1;

            var newEmployee = new Employee
            {
                Id = newId,
                Name = application.CandidateName,
                Email = application.Email,
                Phone = application.Phone,
                Department = job?.Department ?? "General",
                Salary = 0,
                LeaveBalance = 20,
                ProfilePicturePath = null
            };
            employees.Add(newEmployee);
            _data.SaveEmployees(employees);

            // Generate credentials
            string plainPassword = GenerateRandomPassword(10);
            string hashedPassword = PasswordHasher.HashPassword(plainPassword);
            string username = application.Email.Split('@')[0];

            // Check for duplicate username
            var users = _data.GetUsers();
            if (users.Any(u => u.Username == username))
            {
                username = $"{username}{newId}";
            }

            users.Add(new User
            {
                Username = username,
                Password = hashedPassword,
                UserRole = Role.Employee,
                EmployeeId = newId
            });
            _data.SaveUsers(users);

            // Update application status
            application.Status = "Hired";
            application.IsShortlisted = true;
            _data.SaveJobApplications(_data.GetJobApplications());

            // Close job posting (optional)
            if (job != null)
            {
                job.IsActive = false;
                _data.SaveJobPostings(_data.GetJobPostings());
            }

            // Send welcome email
            try
            {
                string subject = "Congratulations! You're Hired!";
                string body = $@"
            <h2>Welcome to the Team!</h2>
            <p>Dear {application.CandidateName},</p>
            <p>We are pleased to offer you the position of <strong>{job?.Title}</strong>.</p>
            <p>Your account has been created in our Employee Management System:</p>
            <p><strong>Username:</strong> {username}<br />
            <strong>Temporary Password:</strong> {plainPassword}</p>
            <p>Please log in and change your password immediately.</p>
            <p><a href='{Request.Scheme}://{Request.Host}/Account/Login'>Login Here</a></p>
            <br />
            <p>Best regards,<br />HR Team</p>
        ";
                await _emailService.SendEmailAsync(application.Email, subject, body);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Welcome email failed: {ex.Message}");
            }

            TempData["Message"] = $"{application.CandidateName} has been hired! Credentials sent to email.";
            return RedirectToAction("ManageJobPostings");
        }

        [HttpPost]
        public async Task<IActionResult> RejectAllNonShortlisted(int jobId)
        {
            var applications = _data.GetJobApplications();
            var job = _data.GetJobPostings().FirstOrDefault(j => j.Id == jobId);

            if (job == null)
            {
                TempData["Error"] = "Job not found.";
                return RedirectToAction("ManageJobPostings");
            }

            // Get all non-shortlisted, non-hired applications
            var toReject = applications.Where(a => a.JobPostingId == jobId
                && !a.IsShortlisted
                && a.Status != "Hired").ToList();

            int rejectionCount = 0;

            foreach (var app in toReject)
            {
                try
                {
                    // Send rejection email
                    string subject = "Update on Your Job Application";
                    string body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2 style='color: #2C3E8F;'>Thank you for your application</h2>
                    <p>Dear <strong>{app.CandidateName}</strong>,</p>
                    <p>Thank you for applying for the position of <strong>{job.Title}</strong> at our company.</p>
                    <p>We have carefully reviewed all applications and regret to inform you that we have decided to move forward with other candidates.</p>
                    <p>We wish you the best in your job search and future career endeavors.</p>
                    <br />
                    <p>Sincerely,<br />HR Team</p>
                </body>
                </html>
            ";
                    await _emailService.SendEmailAsync(app.Email, subject, body);
                    rejectionCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Rejection email failed for {app.Email}: {ex.Message}");
                }
            }

            // ✅ Remove rejected applications from the list entirely
            var remainingApplications = applications
                .Where(a => !toReject.Contains(a))
                .ToList();

            _data.SaveJobApplications(remainingApplications);

            TempData["Message"] = $"{rejectionCount} candidate(s) have been rejected and removed from the system.";
            return RedirectToAction("ShortlistedCandidates", new { jobId });
        }
    }
}