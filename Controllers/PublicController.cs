using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EmployeeManagementSystem.Models;
using EmployeeManagementSystem.Services;

namespace EmployeeManagementSystem.Controllers
{
    [AllowAnonymous]  // ✅ This makes the entire controller public
    public class PublicController : Controller
    {
        private readonly IFileDataService _data;
        private readonly ICVAnalyzer _cvAnalyzer;
        private readonly IWebHostEnvironment _environment;

        public PublicController(IFileDataService data, ICVAnalyzer cvAnalyzer, IWebHostEnvironment environment)
        {
            _data = data;
            _cvAnalyzer = cvAnalyzer;
            _environment = environment;
        }

        [HttpGet]
        public IActionResult Apply(int jobId)
        {
            var job = _data.GetJobPostings().FirstOrDefault(j => j.Id == jobId);

            if (job == null || !job.IsActive)
            {
                ViewBag.Error = "The job posting you are looking for does not exist.";
                return View("Closed");
            }

            if (job.ApplicationDeadline.HasValue && job.ApplicationDeadline.Value < DateTime.Now)
            {
                ViewBag.Error = $"The application deadline for this position was {job.ApplicationDeadline.Value.ToShortDateString()}. Applications are now closed.";
                return View("Closed");
            }

            ViewBag.Job = job;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Apply(int jobId, string fullName, string email, string phone, IFormFile cvFile)
        {
            var job = _data.GetJobPostings().FirstOrDefault(j => j.Id == jobId);

            if (job == null || !job.IsActive)
            {

                ViewBag.Error = "This job posting has been closed. Applications are no longer being accepted.";
                ViewBag.ShowClosedMessage = true;
                return View("Closed");
            }

            if (job.ApplicationDeadline.HasValue && job.ApplicationDeadline.Value < DateTime.Now)
            {
                ViewBag.Error = $"The application deadline for this position was {job.ApplicationDeadline.Value.ToShortDateString()}. Applications are now closed.";
                ViewBag.ShowClosedMessage = true;
                return View("Closed");
            }

            if (cvFile == null || cvFile.Length == 0)
            {
                TempData["Error"] = "Please upload your CV.";
                return RedirectToAction("Apply", new { jobId });
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

            // ✅ NEW: Only shortlist if match score >= 10%
            if (matchScore >= 10)
            {
                // Auto-shortlist only candidates with 10% or higher match score
                var jobApps = applications.Where(a => a.JobPostingId == jobId && a.Status == "Pending")
                    .Where(a => a.MatchScore >= 10)  // ✅ Only candidates with >=10% match
                    .OrderByDescending(a => a.MatchScore)
                    .ToList();

                // Reset shortlist flag for all applications of this job
                foreach (var app in applications.Where(a => a.JobPostingId == jobId))
                {
                    app.IsShortlisted = false;
                    if (app.Status != "Hired")
                        app.Status = "Pending";
                }

                // Shortlist top 3 among qualified candidates
                for (int i = 0; i < jobApps.Count && i < 3; i++)
                {
                    jobApps[i].IsShortlisted = true;
                    jobApps[i].Status = "Shortlisted";
                }
                _data.SaveJobApplications(applications);
            }

            // ✅ Send success message to candidate (not redirect to login)
            TempData["ApplicationSuccess"] = "Your application has been submitted successfully! We will contact you soon.";
            return RedirectToAction("ApplicationSuccess");
        }

        [HttpGet]
        public IActionResult ApplicationSuccess()
        {
            var message = TempData["ApplicationSuccess"] as string;
            if (string.IsNullOrEmpty(message))
            {
                return RedirectToAction("Landing");
            }
            ViewBag.Message = message;
            return View();  // ✅ This shows the success page
        }

        [HttpGet]
        public IActionResult Landing()
        {
            var jobs = _data.GetJobPostings()
                .Where(j => j.IsActive && (!j.ApplicationDeadline.HasValue || j.ApplicationDeadline.Value >= DateTime.Now))
                .ToList();
            ViewBag.Jobs = jobs;
            return View();
        }
    }
}