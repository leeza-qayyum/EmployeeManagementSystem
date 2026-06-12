# Employee Management System

A complete desktop-based Employee Management System built with **C# Windows Forms (.NET Framework 4.8)**. This system helps organizations manage employees, leaves, attendance, recruitment, and meetings efficiently.

---

## 🚀 Features

### 👨‍💼 HR Manager
- Secure login with password hashing
- Add, edit, and delete employees
- Auto-generate passwords and send credentials via email
- Appoint Department Heads (two-step: department → employee)
- Create job postings with required skills
- View applications with auto-shortlisting (top 3 by CV match score)
- One-click hiring (auto-creates employee account)
- View monthly attendance reports

### 👤 Employee
- View and edit profile (picture, email, phone)
- Apply for leave (checks available balance)
- View leave history with status (Pending/Approved/Rejected)
- Auto-mark attendance on login (late after 9:30 AM)
- View monthly attendance with statistics
- View department meetings

### 👔 Department Head
- View employees in department
- Approve/reject leave requests (auto-deducts from balance)
- View monthly attendance with late employees list
- Schedule, reschedule, and mark meetings as done
- Email notifications for meetings

---

## 🛠️ Technology Stack

| Layer | Technology |
|-------|------------|
| **Frontend** | Windows Forms (.NET Framework 4.8) |
| **Language** | C# |
| **Data Storage** | JSON Files (file-based, no database) |
| **Authentication** | SHA256 password hashing |
| **Email** | SMTP (Gmail App Password) |

---

## 📁 Project Structure
EmployeeManagementSystem/
├── Account/
│ ├── LoginForm.cs
│ └── ChangePasswordForm.cs
├── HR/
│ ├── HRDashboardForm.cs
│ ├── ManageEmployeesForm.cs
│ ├── AddEmployeeForm.cs
│ ├── EditEmployeeForm.cs
│ ├── AppointHeadForm.cs
│ ├── CreateJobPostingForm.cs
│ ├── ManageJobPostingsForm.cs
│ ├── ShortlistedCandidatesForm.cs
│ ├── ViewApplicationsForm.cs
│ └── ViewProfileForm.cs
├── Employee/
│ ├── EmployeeDashboardForm.cs
│ ├── ApplyLeaveForm.cs
│ ├── LeaveHistoryForm.cs
│ ├── MyAttendanceForm.cs
│ ├── MeetingsForm.cs
│ └── ViewProfileForm.cs
├── DepartmentHead/
│ ├── DeptHeadDashboardForm.cs
│ ├── ViewEmployeesForm.cs
│ ├── ManageLeavesForm.cs
│ ├── MonthlyAttendanceForm.cs
│ ├── ManageMeetingsForm.cs
│ ├── ScheduleMeetingForm.cs
│ └── ViewProfileForm.cs
├── Common/
│ └── Models/
│ ├── Employee.cs
│ ├── LeaveRequest.cs
│ ├── Attendance.cs
│ ├── Meeting.cs
│ └── User.cs
├── Program.cs
└── EmployeeManagementSystem.csproj

---

## 🖥️ How to Run the Project

### Prerequisites
- **Visual Studio 2022** (any edition)
- **.NET Framework 4.8** (included with VS 2022)

### Steps

1. **Clone or download** this repository
2. **Open** `EmployeeManagementSystem.sln` or `EmployeeManagementSystem.csproj` in Visual Studio
3. **Build** the solution (Ctrl + Shift + B)
4. **Run** the application (F5)

### Contributors
Leeza Qayyum, 
Iman Fatima, 
Muneeb UR Rehman

}
