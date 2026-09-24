using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;
using UniManage.Models;

namespace UniManage.ViewModels
{
public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public class RegisterViewModel
{
    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string Address { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public DateTime? DateOfBirth { get; set; }

    [Required]
    public string Gender { get; set; } = "Male";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public class ForgotPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class VerifyOtpViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Otp { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;
}

public class CourseListViewModel
{
    public string Search { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public decimal? MaxPrice { get; set; }
    public List<Course> Courses { get; set; } = new List<Course>();
    public List<CourseCategory> Categories { get; set; } = new List<CourseCategory>();
}

public class ContactViewModel
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Message { get; set; } = string.Empty;
}

public class CourseManageViewModel
{
    public int? Id { get; set; }

    [Required, MaxLength(180)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Range(0, 1000000)]
    public decimal Price { get; set; }

    [Required]
    public int CourseCategoryId { get; set; }

    [Required]
    public string LecturerId { get; set; } = string.Empty;

    public string ImageUrl { get; set; }
    public HttpPostedFileBase ImageFile { get; set; }
}

public class CategoryManageViewModel
{
    public int? Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;
}

public class AssignmentManageViewModel
{
    public int? Id { get; set; }

    [Required]
    public int CourseId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateTime DeadlineUtc { get; set; }

    public HttpPostedFileBase MaterialFile { get; set; }
}

public class StudentAssignmentsPageViewModel
{
    public List<StudentAssignmentCalendarItemViewModel> Assignments { get; set; } = new List<StudentAssignmentCalendarItemViewModel>();
}

public class StudentAssignmentCalendarItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DeadlineUtc { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public bool IsSubmitted { get; set; }
    public DateTime? LatestSubmittedAtUtc { get; set; }
    public string LatestSubmissionFilePath { get; set; }
    public bool IsOverdue { get; set; }
    public bool HasMaterial { get; set; }
    public string MaterialFileName { get; set; }
}

public class StudentGradeRowViewModel
{
    public string CourseTitle { get; set; } = string.Empty;
    public string AssignmentTitle { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string Feedback { get; set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; set; }
}

public class StudentGradesPageViewModel
{
    public List<StudentGradeRowViewModel> Grades { get; set; } = new List<StudentGradeRowViewModel>();
}

public class GradeManageViewModel
{
    [Required]
    public int SubmissionId { get; set; }

    [Range(0, 100)]
    public decimal Score { get; set; }

    [MaxLength(800)]
    public string Feedback { get; set; } = string.Empty;
}

public class MessageCreateViewModel
{
    [Required]
    public string ReceiverId { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Content { get; set; } = string.Empty;
}

public class AdminReportsViewModel
{
    public int TotalUsers { get; set; }
    public int TotalCourses { get; set; }
    public int TotalEnrollments { get; set; }
    public int ActiveEnrollments { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalPayments { get; set; }
    public List<CoursePopularityItemViewModel> TopCourses { get; set; } = new List<CoursePopularityItemViewModel>();
    public List<MonthlyPaymentItemViewModel> MonthlyPayments { get; set; } = new List<MonthlyPaymentItemViewModel>();
    public List<StudentPerformanceItemViewModel> StudentPerformance { get; set; } = new List<StudentPerformanceItemViewModel>();
    public List<PaymentStatusItemViewModel> PaymentStatusSummary { get; set; } = new List<PaymentStatusItemViewModel>();
    public List<EnrollmentStatusItemViewModel> EnrollmentStatusSummary { get; set; } = new List<EnrollmentStatusItemViewModel>();
}

public class CoursePopularityItemViewModel
{
    public string CourseTitle { get; set; } = string.Empty;
    public int EnrollmentCount { get; set; }
}

public class MonthlyPaymentItemViewModel
{
    public string MonthLabel { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

public class StudentPerformanceItemViewModel
{
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public int GradedSubmissions { get; set; }
    public decimal AverageScore { get; set; }
}

public class PaymentStatusItemViewModel
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

public class EnrollmentStatusItemViewModel
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class LecturerReportsViewModel
{
    public List<StudentPerformanceItemViewModel> StudentPerformance { get; set; } = new List<StudentPerformanceItemViewModel>();
    public List<CoursePopularityItemViewModel> CoursePopularity { get; set; } = new List<CoursePopularityItemViewModel>();
}

public class AdminUsersPageViewModel
{
    public List<AdminUserRowViewModel> Users { get; set; } = new List<AdminUserRowViewModel>();
    public AdminUserManageViewModel CreateUser { get; set; } = new AdminUserManageViewModel();
}

public class AdminUserRowViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public bool IsCurrentUser { get; set; }
}

public class AdminUserManageViewModel
{
    public string Id { get; set; }

    [Required, MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Gender { get; set; } = "NotSpecified";

    public DateTime? DateOfBirth { get; set; }

    [Required]
    public string RoleName { get; set; } = "Student";

    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public class AdminPaymentRowViewModel
{
    public int Id { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime PaidAtUtc { get; set; }
    public bool CanGeneratePdf { get; set; }
}

public class AdminPaymentsPageViewModel
{
    public List<AdminPaymentRowViewModel> Payments { get; set; } = new List<AdminPaymentRowViewModel>();
}

public class SystemUsageLogRowViewModel
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string ControllerName { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public DateTime LoggedAtUtc { get; set; }
}

public class SystemUsagePageViewModel
{
    public int TotalLogs { get; set; }
    public int UniqueUsers { get; set; }
    public int TodayLogs { get; set; }
    public string SearchTerm { get; set; } = string.Empty;
    public int FilteredLogs { get; set; }
    public List<SystemUsageLogRowViewModel> Logs { get; set; } = new List<SystemUsageLogRowViewModel>();
}

public class EnrollmentStatisticsPageViewModel
{
    public int TotalEnrollments { get; set; }
    public int ActiveEnrollments { get; set; }
    public int PendingEnrollments { get; set; }
    public int CancelledEnrollments { get; set; }
    public List<CoursePopularityItemViewModel> TopCourses { get; set; } = new List<CoursePopularityItemViewModel>();
}
}
