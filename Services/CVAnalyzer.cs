using System.Text;
using System.Text.RegularExpressions;

namespace EmployeeManagementSystem.Services
{
    public interface ICVAnalyzer
    {
        string ExtractTextFromCV(string filePath);
        string ExtractSkills(string cvText, string requiredSkills);
        int CalculateMatchScore(string extractedSkills, string requiredSkills);
    }

    public class CVAnalyzer : ICVAnalyzer
    {
        // Common stop words to ignore
        private readonly HashSet<string> _stopWords = new HashSet<string>
        {
            "a", "an", "and", "the", "of", "to", "in", "for", "on", "with", "by", "at", "from",
            "is", "was", "are", "were", "been", "be", "have", "has", "had", "having",
            "this", "that", "these", "those", "it", "they", "we", "you", "he", "she",
            "experience", "working", "knowledge", "proficient", "skilled", "familiar"
        };

        public string ExtractTextFromCV(string filePath)
        {
            if (!File.Exists(filePath))
                return "";

            string extension = Path.GetExtension(filePath).ToLower();

            try
            {
                if (extension == ".txt")
                {
                    return File.ReadAllText(filePath, Encoding.UTF8);
                }
                else if (extension == ".pdf" || extension == ".docx")
                {
                    // For demo purposes - return sample text
                    // In production, use proper PDF/DOCX parsing libraries
                    return @"
                        I have strong experience in the following technologies:
                        C#, ASP.NET Core, SQL Server, JavaScript, React, Entity Framework, LINQ,
                        Git, Azure, Agile methodology, REST API development.
                        I am a team player with excellent problem solving and communication skills.
                    ";
                }
                else
                {
                    return File.ReadAllText(filePath, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading CV: {ex.Message}");
                return "";
            }
        }

        public string ExtractSkills(string cvText, string requiredSkills)
        {
            if (string.IsNullOrEmpty(cvText) || string.IsNullOrEmpty(requiredSkills))
                return "";

            var foundSkills = new List<string>();
            var cvTextLower = cvText.ToLower();

            // Split required skills by commas OR by spaces (for phrases)
            var requiredList = requiredSkills.Split(',')
                .Select(s => s.Trim().ToLower())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            // Also split by spaces to catch individual keywords
            var requiredKeywords = requiredList
                .SelectMany(s => s.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Select(k => k.Trim())
                .Where(k => k.Length > 2) // Ignore very short words
                .Distinct()
                .ToList();

            // Check for exact skill matches (case insensitive)
            foreach (var required in requiredList)
            {
                // Check if the exact skill phrase exists in CV
                if (cvTextLower.Contains(required))
                {
                    foundSkills.Add(required);
                }
                // Check for individual keywords within the required phrase
                else
                {
                    var keywords = required.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var keyword in keywords)
                    {
                        if (keyword.Length > 2 && cvTextLower.Contains(keyword))
                        {
                            foundSkills.Add(required); // Add the full required skill
                            break;
                        }
                    }
                }
            }

            // Also check for individual keywords from required skills
            foreach (var keyword in requiredKeywords)
            {
                if (!foundSkills.Any(s => s.Contains(keyword)) && cvTextLower.Contains(keyword))
                {
                    foundSkills.Add(keyword);
                }
            }

            return string.Join(",", foundSkills.Distinct());
        }

        public int CalculateMatchScore(string extractedSkills, string requiredSkills)
        {
            if (string.IsNullOrEmpty(extractedSkills) || string.IsNullOrEmpty(requiredSkills))
                return 0;

            var extracted = extractedSkills.Split(',')
                .Select(s => s.Trim().ToLower())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToHashSet();

            var required = requiredSkills.Split(',')
                .Select(s => s.Trim().ToLower())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToHashSet();

            if (!required.Any()) return 0;

            int matched = 0;

            foreach (var req in required)
            {
                // Check for exact match
                if (extracted.Contains(req))
                {
                    matched++;
                }
                // Check for partial match (e.g., "SQL" matches "SQL Server")
                else if (extracted.Any(e => req.Contains(e) || e.Contains(req)))
                {
                    matched++;
                }
            }

            return (int)Math.Round((double)matched / required.Count * 100);
        }
    }
}