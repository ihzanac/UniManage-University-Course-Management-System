using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNet.Identity.EntityFramework;

namespace UniManage.Models
{
public class ApplicationUser : IdentityUser
{
    [MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(255)]
    public string Address { get; set; } = string.Empty;

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(20)]
    public string Gender { get; set; } = "NotSpecified";

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}

public class CourseCategory
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public ICollection<Course> Courses { get; set; } = new List<Course>();
}

public class Course
{
    public int Id { get; set; }

    [Required, MaxLength(180)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [MaxLength(500)]
    public string ImageUrl { get; set; } = "/images/default-course.jpg";

    [Range(0, 1000000)]
    public decimal Price { get; set; }

    public int CourseCategoryId { get; set; }
    public CourseCategory CourseCategory { get; set; }

    public string LecturerId { get; set; } = string.Empty;
    public ApplicationUser Lecturer { get; set; }

    public ICollection<CourseMaterial> Materials { get; set; } = new List<CourseMaterial>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
}

public class CourseMaterial
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course Course { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string FilePath { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public enum EnrollmentStatus
{
    Pending = 1,
    Active = 2,
    Cancelled = 3
}

public class Enrollment
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course Course { get; set; }

    public string StudentId { get; set; } = string.Empty;
    public ApplicationUser Student { get; set; }

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Pending;
    public DateTime EnrolledAtUtc { get; set; } = DateTime.UtcNow;
    public Payment Payment { get; set; }
}

public enum PaymentStatus
{
    Pending = 1,
    Paid = 2,
    Cancelled = 3,
    Failed = 4
}

public class Payment
{
    public int Id { get; set; }
    public int EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string StripeSessionId { get; set; } = string.Empty;
    public string StripePaymentIntentId { get; set; } = string.Empty;
    public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;
}

public class Assignment
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course Course { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    public DateTime DeadlineUtc { get; set; }
    [MaxLength(300)]
    public string MaterialFilePath { get; set; } = string.Empty;
    [MaxLength(200)]
    public string MaterialFileName { get; set; } = string.Empty;

    public ICollection<AssignmentSubmission> Submissions { get; set; } = new List<AssignmentSubmission>();
}

public enum AssignmentSubmissionStatus
{
    Submitted = 1,
    Late = 2
}

public class AssignmentSubmission
{
    public int Id { get; set; }
    public int AssignmentId { get; set; }
    public Assignment Assignment { get; set; }

    public string StudentId { get; set; } = string.Empty;
    public ApplicationUser Student { get; set; }

    [Required]
    public string FilePath { get; set; } = string.Empty;

    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public AssignmentSubmissionStatus Status { get; set; } = AssignmentSubmissionStatus.Submitted;
    public Grade Grade { get; set; }
}

public class Grade
{
    public int Id { get; set; }
    public int AssignmentSubmissionId { get; set; }
    public AssignmentSubmission AssignmentSubmission { get; set; }
    public decimal Score { get; set; }

    [MaxLength(800)]
    public string Feedback { get; set; } = string.Empty;
}

public class Message
{
    public int Id { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public ApplicationUser Sender { get; set; }
    public string ReceiverId { get; set; } = string.Empty;
    public ApplicationUser Receiver { get; set; }

    [Required, MaxLength(1000)]
    public string Content { get; set; } = string.Empty;

    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<MessageNotification> Notifications { get; set; } = new List<MessageNotification>();
}

public class MessageNotification
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public Message Message { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; }
    public bool IsRead { get; set; }
}

public class ContactMessage
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
}

public class OtpToken
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; }
    public string Purpose { get; set; } = "ForgotPassword";
    public string Code { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public bool IsUsed { get; set; }
}

public class SystemUsageLog
{
    public int Id { get; set; }

    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(120)]
    public string UserName { get; set; } = "Guest";

    [MaxLength(80)]
    public string RoleName { get; set; } = "Unknown";

    [MaxLength(80)]
    public string ActionType { get; set; } = string.Empty;

    [MaxLength(80)]
    public string ControllerName { get; set; } = string.Empty;

    [MaxLength(80)]
    public string ActionName { get; set; } = string.Empty;

    [MaxLength(250)]
    public string Path { get; set; } = string.Empty;

    [MaxLength(10)]
    public string HttpMethod { get; set; } = string.Empty;

    public int StatusCode { get; set; }

    [MaxLength(80)]
    public string IpAddress { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Details { get; set; } = string.Empty;

    public DateTime LoggedAtUtc { get; set; } = DateTime.UtcNow;
}
}
