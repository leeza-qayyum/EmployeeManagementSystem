using System.ComponentModel.DataAnnotations;

namespace EmployeeManagementSystem.Models
{
    public enum Role
    {
        HRManager,
        Employee,
        DepartmentHead
    }

    public class User
    {
        [Key]
        public string Username { get; set; }  
        public string Password { get; set; }
        public Role UserRole { get; set; }
        public int? EmployeeId { get; set; }  
    }
}