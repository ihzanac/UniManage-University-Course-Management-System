using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Hosting;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.AspNet.Identity.EntityFramework;
using System.Web;
using System.Data.Entity;
using System.Data.SqlClient;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Newtonsoft.Json;
using UniManage.Data;
using UniManage.Models;
using UniManage.Services;
using UniManage.ViewModels;

namespace UniManage.Controllers
{
public class AccountController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ApplicationUserManager _userManager;
    private readonly ApplicationSignInManager _signInManager;
    private readonly IEmailService _emailService;
    private readonly IOtpService _otpService;

    public AccountController()
        : this(
            ApplicationDbContext.Create(),
            System.Web.HttpContext.Current.GetOwinContext().GetUserManager<ApplicationUserManager>(),
            System.Web.HttpContext.Current.GetOwinContext().Get<ApplicationSignInManager>(),
            new SmtpEmailService(),
            new OtpService())
    {
    }

    public AccountController(
        ApplicationDbContext dbContext,
        ApplicationUserManager userManager,
        ApplicationSignInManager signInManager,
        IEmailService emailService,
        IOtpService otpService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _signInManager = signInManager;
        _emailService = emailService;
        _otpService = otpService;
    }

    [HttpGet]
    public ActionResult SelectLogin() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> Logout()
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user != null)
        {
            var roleName = (await _userManager.GetRolesAsync(user.Id)).FirstOrDefault() ?? "Unknown";
            _dbContext.SystemUsageLogs.Add(new SystemUsageLog
            {
                UserId = user.Id,
                UserName = string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? "Unknown User" : user.FullName,
                RoleName = roleName,
                ActionType = "Logout",
                ControllerName = "Account",
                ActionName = "Logout",
                Path = "/Account/Logout",
                HttpMethod = HttpContext.Request.HttpMethod,
                StatusCode = 200,
                IpAddress = HttpContext?.Request?.UserHostAddress ?? "N/A",
                Details = "User logout event",
                LoggedAtUtc = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();
        }

        HttpContext.GetOwinContext().Authentication.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public ActionResult Login(string role) => View(new LoginViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> Login(string role, LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null || !await _userManager.IsInRoleAsync(user.Id, role))
        {
            ModelState.AddModelError(string.Empty, "Invalid credentials.");
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Login Failed";
            TempData["ToastMessage"] = "Invalid email, password, or role selected.";
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user.UserName, model.Password, true, true);
        if (result != SignInStatus.Success)
        {
            ModelState.AddModelError(string.Empty, "Login failed.");
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Login Failed";
            TempData["ToastMessage"] = "Please check your credentials and try again.";
            return View(model);
        }

        _dbContext.SystemUsageLogs.Add(new SystemUsageLog
        {
            UserId = user.Id,
            UserName = string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? "Unknown User" : user.FullName,
            RoleName = role,
            ActionType = "Login",
            ControllerName = "Account",
            ActionName = "Login",
            Path = "/Account/Login",
            HttpMethod = HttpContext.Request.HttpMethod,
            StatusCode = 200,
            IpAddress = HttpContext?.Request?.UserHostAddress ?? "N/A",
            Details = "User login event",
            LoggedAtUtc = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        return role switch
        {
            "Administrator" => RedirectToAction("Dashboard", "Admin"),
            "Lecturer" => RedirectToAction("Dashboard", "Lecturer"),
            _ => RedirectToAction("Dashboard", "Student")
        };
    }

    [HttpGet]
    public ActionResult Register() => View(new RegisterViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = new ApplicationUser
        {
            FullName = model.Name,
            Address = model.Address,
            DateOfBirth = model.DateOfBirth,
            Gender = model.Gender,
            UserName = model.Email,
            Email = model.Email
        };
        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error);
            return View(model);
        }

        await _userManager.AddToRoleAsync(user.Id, "Student");
        await _emailService.SendAsync(user.Email!, "Welcome to UniManage", "<p>Your account is ready.</p>");
        TempData["ToastType"] = "success";
        TempData["ToastTitle"] = "Registration Successful";
        TempData["ToastMessage"] = "Your account has been created successfully. Please login to continue.";
        return RedirectToAction(nameof(Login), new { role = "Student" });
    }

    [HttpGet]
    public ActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null) return RedirectToAction(nameof(VerifyOtp), new { email = model.Email });
        var otp = await _otpService.GenerateForgotPasswordOtpAsync(user);
        await _emailService.SendAsync(user.Email!, "UniManage OTP", $"<p>Your OTP is <strong>{otp}</strong>.</p>");
        TempData["ToastType"] = "success";
        TempData["ToastTitle"] = "OTP Sent";
        TempData["ToastMessage"] = "We sent an OTP to your email address.";
        return RedirectToAction(nameof(VerifyOtp), new { email = model.Email });
    }

    [HttpGet]
    public ActionResult VerifyOtp(string email) => View(new VerifyOtpViewModel { Email = email });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> VerifyOtp(VerifyOtpViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null || !await _otpService.VerifyForgotPasswordOtpAsync(user, model.Otp))
        {
            ModelState.AddModelError(string.Empty, "Invalid OTP");
            return View(model);
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user.Id);
        var resetResult = await _userManager.ResetPasswordAsync(user.Id, token, model.NewPassword);
        if (!resetResult.Succeeded)
        {
            foreach (var error in resetResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
            return View(model);
        }

        TempData["ToastType"] = "success";
        TempData["ToastTitle"] = "Password Updated";
        TempData["ToastMessage"] = "Your password has been reset successfully.";
        return RedirectToAction(nameof(Login), new { role = "Student" });
    }
}

public class CoursesController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public CoursesController() : this(ApplicationDbContext.Create()) { }

    public CoursesController(ApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<ActionResult> Index(string search, int? categoryId, decimal? maxPrice, bool apply = false)
    {
        var query = _dbContext.Courses.Include(c => c.CourseCategory).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(c => c.Title.Contains(search));
        if (categoryId.HasValue) query = query.Where(c => c.CourseCategoryId == categoryId);
        if (maxPrice.HasValue) query = query.Where(c => c.Price <= maxPrice);
        if (apply && string.IsNullOrWhiteSpace(search) && !categoryId.HasValue && !maxPrice.HasValue)
        {
            ViewBag.FilterError = "Select at least one filter option before applying.";
        }

        var vm = new CourseListViewModel
        {
            Search = search ?? string.Empty,
            CategoryId = categoryId,
            MaxPrice = maxPrice,
            Categories = await _dbContext.CourseCategories.ToListAsync(),
            Courses = await query.ToListAsync()
        };
        return View(vm);
    }

    public async Task<ActionResult> Details(int id)
    {
        var course = await _dbContext.Courses
            .Include(c => c.CourseCategory)
            .Include(c => c.Lecturer)
            .FirstOrDefaultAsync(c => c.Id == id);
        return course == null ? HttpNotFound() : (ActionResult)View(course);
    }
}

[Authorize(Roles = "Student")]
public class EnrollmentController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ApplicationUserManager _userManager;

    public EnrollmentController()
        : this(ApplicationDbContext.Create(), System.Web.HttpContext.Current.GetOwinContext().GetUserManager<ApplicationUserManager>())
    {
    }

    public EnrollmentController(ApplicationDbContext dbContext, ApplicationUserManager userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> Start(int courseId)
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null) return new HttpUnauthorizedResult();

        var exists = await _dbContext.Enrollments.AnyAsync(e => e.CourseId == courseId && e.StudentId == user.Id);
        if (!exists)
        {
            _dbContext.Enrollments.Add(new Enrollment { CourseId = courseId, StudentId = user.Id });
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToAction("Checkout", "Payment", new { courseId });
    }
}

[Authorize(Roles = "Student")]
public class PaymentController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ApplicationUserManager _userManager;
    private readonly IPaymentService _paymentService;
    private readonly IEmailService _emailService;

    public PaymentController()
        : this(ApplicationDbContext.Create(), System.Web.HttpContext.Current.GetOwinContext().GetUserManager<ApplicationUserManager>(), new StripePaymentService(), new SmtpEmailService())
    {
    }

    public PaymentController(ApplicationDbContext dbContext, ApplicationUserManager userManager, IPaymentService paymentService, IEmailService emailService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _paymentService = paymentService;
        _emailService = emailService;
    }

    public async Task<ActionResult> Checkout(int courseId)
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        var course = await _dbContext.Courses.FindAsync(courseId);
        if (user == null || course == null) return HttpNotFound();
        var protocol = Request?.Url?.Scheme ?? Uri.UriSchemeHttp;
        var success = Url.Action(nameof(Success), "Payment", new { courseId }, protocol);
        var cancel = Url.Action(nameof(Cancel), "Payment", new { courseId }, protocol);
        var checkoutUrl = await _paymentService.CreateCheckoutSessionAsync(course, user, success, cancel);
        return Redirect(checkoutUrl);
    }

    public async Task<ActionResult> Success(int courseId)
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new HttpUnauthorizedResult();
        }

        var enrollment = await _dbContext.Enrollments.FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == user.Id);
        var course = await _dbContext.Courses.FindAsync(courseId);
        if (enrollment == null || course == null) return HttpNotFound();

        enrollment.Status = EnrollmentStatus.Active;
        var existingPayment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.EnrollmentId == enrollment.Id && p.Status == PaymentStatus.Paid);
        if (existingPayment == null)
        {
            // Use explicit SQL insert to avoid EF one-to-one key inference sending tblPayment.Id.
            await _dbContext.Database.ExecuteSqlCommandAsync(
                @"INSERT INTO [dbo].[tblPayment]
                  ([EnrollmentId], [Amount], [Status], [StripeSessionId], [StripePaymentIntentId], [PaidAtUtc])
                  VALUES (@EnrollmentId, @Amount, @Status, @StripeSessionId, @StripePaymentIntentId, @PaidAtUtc)",
                new SqlParameter("@EnrollmentId", enrollment.Id),
                new SqlParameter("@Amount", course.Price),
                new SqlParameter("@Status", (int)PaymentStatus.Paid),
                new SqlParameter("@StripeSessionId", string.Empty),
                new SqlParameter("@StripePaymentIntentId", string.Empty),
                new SqlParameter("@PaidAtUtc", DateTime.UtcNow));
        }

        await _dbContext.SaveChangesAsync();
        await _emailService.SendAsync(user.Email ?? string.Empty, "Payment Success", $"<p>You are enrolled in {course.Title}.</p>");
        return RedirectToAction("Dashboard", "Student");
    }

    public ActionResult Cancel() => View();
}

[Authorize(Roles = "Student")]
public class StudentController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ApplicationUserManager _userManager;

    public StudentController()
        : this(ApplicationDbContext.Create(), System.Web.HttpContext.Current.GetOwinContext().GetUserManager<ApplicationUserManager>())
    {
    }

    public StudentController(ApplicationDbContext dbContext, ApplicationUserManager userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task<ActionResult> Dashboard()
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new HttpUnauthorizedResult();
        }

        var activeCourseIds = await _dbContext.Enrollments
            .Where(e => e.StudentId == user.Id && e.Status == EnrollmentStatus.Active)
            .Select(e => e.CourseId)
            .Distinct()
            .ToListAsync();

        ViewBag.Enrollments = activeCourseIds.Count;
        ViewBag.UnreadMessages = await _dbContext.MessageNotifications.CountAsync(n => n.UserId == user.Id && !n.IsRead);
        ViewBag.Assignments = await _dbContext.AssignmentSubmissions.CountAsync(s => s.StudentId == user.Id);
        return View();
    }

    public async Task<ActionResult> MyCourses()
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new HttpUnauthorizedResult();
        }

        var enrollments = await _dbContext.Enrollments
            .Include(e => e.Course)
            .Include(e => e.Course.Lecturer)
            .Include(e => e.Payment)
            .Where(e => e.StudentId == user.Id)
            .OrderByDescending(e => e.Id)
            .ToListAsync();
        return View(enrollments);
    }

    [HttpGet]
    public async Task<ActionResult> DownloadInvoice(int enrollmentId)
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new HttpUnauthorizedResult();
        }

        var enrollment = await _dbContext.Enrollments
            .Include(e => e.Course)
            .Include(e => e.Course.Lecturer)
            .Include(e => e.Payment)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.StudentId == user.Id);

        if (enrollment == null)
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Invoice Error";
            TempData["ToastMessage"] = "Enrollment not found.";
            return RedirectToAction(nameof(MyCourses));
        }

        if (enrollment.Payment == null || enrollment.Payment.Status != PaymentStatus.Paid)
        {
            TempData["ToastType"] = "warning";
            TempData["ToastTitle"] = "Invoice Not Available";
            TempData["ToastMessage"] = "Invoice is available only for paid courses.";
            return RedirectToAction(nameof(MyCourses));
        }

        var payment = enrollment.Payment;
        var paidAtLocal = payment.PaidAtUtc.ToLocalTime();
        var courseTitle = enrollment.Course?.Title ?? "N/A";
        var lecturerName = enrollment.Course?.Lecturer?.FullName ?? "N/A";
        var studentName = string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? "N/A" : user.FullName;

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(header =>
                {
                    header.Item().Text("UniManage").Bold().FontSize(24).FontColor(Colors.Blue.Darken2);
                    header.Item().Text("University Course Management System").FontSize(12).FontColor(Colors.Grey.Darken1);
                    header.Item().PaddingTop(4).Text("Student Payment Invoice").SemiBold().FontSize(16);
                    header.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(12).Column(content =>
                {
                    content.Spacing(10);
                    content.Item().Text($"Invoice No: UM-STU-{payment.Id:D6}").SemiBold();
                    content.Item().Text($"Issued Date: {DateTime.Now:yyyy-MM-dd HH:mm}");

                    content.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(150);
                            columns.RelativeColumn();
                        });

                        void Row(string label, string value)
                        {
                            table.Cell().PaddingVertical(5).PaddingRight(8).Text(label).SemiBold();
                            table.Cell().PaddingVertical(5).Text(value);
                        }

                        Row("Student Name", studentName);
                        Row("Student Email", user.Email ?? "N/A");
                        Row("Course", courseTitle);
                        Row("Lecturer", lecturerName);
                        Row("Amount Paid", $"LKR {payment.Amount:N2}");
                        Row("Payment Status", payment.Status.ToString());
                        Row("Paid At", paidAtLocal.ToString("yyyy-MM-dd HH:mm:ss"));
                    });

                    content.Item().PaddingTop(12).Border(1).BorderColor(Colors.Blue.Lighten3).Padding(10).Text(
                        "Thank you for your payment. This invoice confirms your successful enrollment in the selected course."
                    ).FontColor(Colors.Grey.Darken2);
                });

                page.Footer().Column(footer =>
                {
                    footer.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                    footer.Item().PaddingTop(6).Text("UniManage Support: support@unimanage.edu | +94 11 234 5678")
                        .FontSize(10)
                        .FontColor(Colors.Grey.Darken1);
                    footer.Item().Text("Generated by UniManage Student Portal").FontSize(9).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf();

        var fileName = $"UniManage-Invoice-{payment.Id}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    public async Task<ActionResult> Assignments()
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new HttpUnauthorizedResult();
        }

        var courseIds = await _dbContext.Enrollments
            .Where(e => e.StudentId == user.Id && e.Status == EnrollmentStatus.Active)
            .Select(e => e.CourseId)
            .ToListAsync();

        var assignments = await _dbContext.Assignments
            .Include(a => a.Course)
            .Where(a => courseIds.Contains(a.CourseId))
            .OrderBy(a => a.DeadlineUtc)
            .ToListAsync();

        var assignmentIds = assignments.Select(a => a.Id).ToList();
        var submissionRows = await _dbContext.AssignmentSubmissions
            .Where(s => s.StudentId == user.Id && assignmentIds.Contains(s.AssignmentId))
            .OrderByDescending(s => s.SubmittedAtUtc)
            .ToListAsync();
        var latestSubmissions = submissionRows
            .GroupBy(s => s.AssignmentId)
            .Select(g => g.First())
            .ToList();
        var latestSubmissionLookup = latestSubmissions.ToDictionary(s => s.AssignmentId, s => s);

        var viewModel = new StudentAssignmentsPageViewModel
        {
            Assignments = assignments.Select(a =>
            {
                latestSubmissionLookup.TryGetValue(a.Id, out var submission);
                return new StudentAssignmentCalendarItemViewModel
                {
                    Id = a.Id,
                    Title = a.Title,
                    Description = a.Description,
                    DeadlineUtc = a.DeadlineUtc,
                    CourseTitle = a.Course?.Title ?? "N/A",
                    IsSubmitted = submission != null,
                    LatestSubmittedAtUtc = submission?.SubmittedAtUtc,
                    LatestSubmissionFilePath = submission?.FilePath,
                    IsOverdue = a.DeadlineUtc < DateTime.UtcNow,
                    HasMaterial = !string.IsNullOrWhiteSpace(a.MaterialFilePath),
                    MaterialFileName = a.MaterialFileName
                };
            }).ToList()
        };

        return View(viewModel);
    }

    public async Task<ActionResult> Grades()
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new HttpUnauthorizedResult();
        }

        var gradedSubmissions = await _dbContext.AssignmentSubmissions
            .Include(s => s.Assignment)
            .Include(s => s.Assignment.Course)
            .Include(s => s.Grade)
            .Where(s => s.StudentId == user.Id && s.Grade != null)
            .OrderByDescending(s => s.SubmittedAtUtc)
            .ToListAsync();

        var model = new StudentGradesPageViewModel
        {
            Grades = gradedSubmissions.Select(s => new StudentGradeRowViewModel
            {
                CourseTitle = s.Assignment?.Course?.Title ?? "N/A",
                AssignmentTitle = s.Assignment?.Title ?? "N/A",
                Score = s.Grade != null ? s.Grade.Score : 0,
                Feedback = s.Grade != null ? s.Grade.Feedback : string.Empty,
                SubmittedAtUtc = s.SubmittedAtUtc
            }).ToList()
        };

        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> SubmitAssignment(int assignmentId, HttpPostedFileBase submissionFile)
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        var assignment = await _dbContext.Assignments.FindAsync(assignmentId);
        if (user == null || assignment == null)
        {
            return HttpNotFound();
        }

        if (submissionFile == null || submissionFile.ContentLength == 0)
        {
            TempData["Error"] = "Please choose a file before submitting.";
            return RedirectToAction(nameof(Assignments));
        }

        var canSubmit = await _dbContext.Enrollments.AnyAsync(e =>
            e.StudentId == user.Id &&
            e.CourseId == assignment.CourseId &&
            e.Status == EnrollmentStatus.Active);
        if (!canSubmit)
        {
            TempData["Error"] = "You are not allowed to submit for this assignment.";
            return RedirectToAction(nameof(Assignments));
        }

        var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
        var extension = Path.GetExtension(submissionFile.FileName);
        if (string.IsNullOrWhiteSpace(extension) ||
            !allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Only PDF or Word documents (.pdf, .doc, .docx) are allowed.";
            return RedirectToAction(nameof(Assignments));
        }

        var isLateSubmission = assignment.DeadlineUtc < DateTime.UtcNow;

        var uploadDirectory = HostingEnvironment.MapPath("~/uploads/submissions")
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uploads", "submissions");
        Directory.CreateDirectory(uploadDirectory);
        var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(submissionFile.FileName)}";
        var filePath = Path.Combine(uploadDirectory, fileName);
        using (var stream = System.IO.File.Create(filePath))
        {
            submissionFile.InputStream.CopyTo(stream);
        }

        _dbContext.AssignmentSubmissions.Add(new AssignmentSubmission
        {
            AssignmentId = assignmentId,
            StudentId = user.Id,
            FilePath = $"/uploads/submissions/{fileName}",
            Status = isLateSubmission ? AssignmentSubmissionStatus.Late : AssignmentSubmissionStatus.Submitted
        });
        await _dbContext.SaveChangesAsync();
        TempData["ToastType"] = isLateSubmission ? "warning" : "success";
        TempData["ToastTitle"] = isLateSubmission ? "Late Submission Accepted" : "Submission Successful";
        TempData["ToastMessage"] = isLateSubmission
            ? "Your submission was accepted after the deadline and marked as late."
            : "Assignment submitted successfully.";
        return RedirectToAction(nameof(Assignments));
    }

    [HttpGet]
    public async Task<ActionResult> ViewSubmission(int assignmentId)
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new HttpUnauthorizedResult();
        }

        var submission = await _dbContext.AssignmentSubmissions
            .Where(s => s.AssignmentId == assignmentId && s.StudentId == user.Id)
            .OrderByDescending(s => s.SubmittedAtUtc)
            .FirstOrDefaultAsync();

        if (submission == null)
        {
            TempData["Error"] = "No submission found for this assignment yet.";
            return RedirectToAction(nameof(Assignments));
        }

        var relativePath = submission.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = HostingEnvironment.MapPath("~/" + submission.FilePath.TrimStart('/'))
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
        if (!System.IO.File.Exists(absolutePath))
        {
            TempData["Error"] = "Submitted file could not be found.";
            return RedirectToAction(nameof(Assignments));
        }

        var downloadFileName = Path.GetFileName(absolutePath);
        return File(absolutePath, "application/octet-stream", downloadFileName);
    }

    [HttpGet]
    public async Task<ActionResult> DownloadAssignmentMaterial(int assignmentId)
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new HttpUnauthorizedResult();
        }

        var assignment = await _dbContext.Assignments
            .Include(a => a.Course)
            .FirstOrDefaultAsync(a => a.Id == assignmentId);
        if (assignment == null || string.IsNullOrWhiteSpace(assignment.MaterialFilePath))
        {
            TempData["Error"] = "Assignment material is not available.";
            return RedirectToAction(nameof(Assignments));
        }

        var canAccess = await _dbContext.Enrollments.AnyAsync(e =>
            e.StudentId == user.Id &&
            e.CourseId == assignment.CourseId &&
            e.Status == EnrollmentStatus.Active);
        if (!canAccess)
        {
            return new HttpStatusCodeResult(403);
        }

        var relativePath = assignment.MaterialFilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = HostingEnvironment.MapPath("~/" + assignment.MaterialFilePath.TrimStart('/'))
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
        if (!System.IO.File.Exists(absolutePath))
        {
            TempData["Error"] = "Assignment material file could not be found.";
            return RedirectToAction(nameof(Assignments));
        }

        var fileName = string.IsNullOrWhiteSpace(assignment.MaterialFileName)
            ? Path.GetFileName(absolutePath)
            : assignment.MaterialFileName;
        return File(absolutePath, "application/octet-stream", fileName);
    }

    [HttpGet]
    public async Task<ActionResult> Messages(int? focusMessageId = null)
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new HttpUnauthorizedResult();
        }

        var allowedLecturerIds = await _dbContext.Enrollments
            .Where(e => e.StudentId == user.Id &&
                        e.Status == EnrollmentStatus.Active &&
                        e.Course != null &&
                        e.Course.LecturerId != null)
            .Select(e => e.Course.LecturerId)
            .Distinct()
            .ToListAsync();

        var messages = await _dbContext.Messages
            .Include(m => m.Sender)
            .Where(m =>
                (m.SenderId == user.Id && allowedLecturerIds.Contains(m.ReceiverId)) ||
                (m.ReceiverId == user.Id && allowedLecturerIds.Contains(m.SenderId)))
            .OrderByDescending(m => m.SentAtUtc)
            .Take(50)
            .ToListAsync();

        var allowedLecturers = await _dbContext.Users
            .Where(u => allowedLecturerIds.Contains(u.Id))
            .OrderBy(u => u.FullName)
            .ToListAsync();
        ViewBag.Lecturers = allowedLecturers;
        ViewBag.FocusMessageId = focusMessageId;
        ViewBag.CurrentUserId = user?.Id ?? string.Empty;
        return View(messages);
    }

    [HttpGet]
    public async Task<ActionResult> Conversation(string partnerId)
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null || string.IsNullOrWhiteSpace(partnerId))
        {
            return Json(Array.Empty<object>(), JsonRequestBehavior.AllowGet);
        }

        var canChat = await _dbContext.Enrollments.AnyAsync(e =>
            e.StudentId == user.Id &&
            e.Status == EnrollmentStatus.Active &&
            e.Course != null &&
            e.Course.LecturerId == partnerId);
        if (!canChat)
        {
            return new HttpStatusCodeResult(403);
        }

        var messages = await _dbContext.Messages
            .Include(m => m.Sender)
            .Where(m =>
                (m.SenderId == user.Id && m.ReceiverId == partnerId) ||
                (m.SenderId == partnerId && m.ReceiverId == user.Id))
            .OrderBy(m => m.SentAtUtc)
            .Take(200)
            .ToListAsync();

        return Json(messages.Select(m => new
        {
            id = m.Id,
            senderId = m.SenderId,
            senderName = m.Sender?.FullName ?? "User",
            content = m.Content,
            sentAt = m.SentAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
        }), JsonRequestBehavior.AllowGet);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> SendMessageAjax(string receiverId, string content)
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new HttpUnauthorizedResult();
        }

        if (string.IsNullOrWhiteSpace(receiverId) || string.IsNullOrWhiteSpace(content))
        {
            return new HttpStatusCodeResult(400, "Receiver and message are required.");
        }

        var canChat = await _dbContext.Enrollments.AnyAsync(e =>
            e.StudentId == user.Id &&
            e.Status == EnrollmentStatus.Active &&
            e.Course != null &&
            e.Course.LecturerId == receiverId);
        if (!canChat)
        {
            return new HttpStatusCodeResult(403);
        }

        var message = new Message
        {
            SenderId = user.Id,
            ReceiverId = receiverId,
            Content = content.Trim()
        };
        _dbContext.Messages.Add(message);
        await _dbContext.SaveChangesAsync();

        _dbContext.MessageNotifications.Add(new MessageNotification
        {
            MessageId = message.Id,
            UserId = receiverId,
            IsRead = false
        });
        await _dbContext.SaveChangesAsync();

        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<ActionResult> OpenNotification(int notificationId)
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new HttpUnauthorizedResult();
        }

        var notification = await _dbContext.MessageNotifications
            .Include(n => n.Message)
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == user.Id);
        if (notification == null || notification.Message == null)
        {
            TempData["ToastType"] = "warning";
            TempData["ToastTitle"] = "Notification";
            TempData["ToastMessage"] = "Notification not found or unavailable.";
            return RedirectToAction(nameof(Dashboard));
        }

        notification.IsRead = true;
        await _dbContext.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(notification.Message.Content) &&
            notification.Message.Content.StartsWith("[ASSIGNMENT]", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Assignments));
        }

        return RedirectToAction(nameof(Messages), new { focusMessageId = notification.MessageId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> MarkAllNotificationsRead()
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new HttpUnauthorizedResult();
        }

        var unreadNotifications = await _dbContext.MessageNotifications
            .Where(n => n.UserId == user.Id && !n.IsRead)
            .ToListAsync();
        if (unreadNotifications.Count > 0)
        {
            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
            }
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Dashboard));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> SendMessage(MessageCreateViewModel model)
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (!ModelState.IsValid || user == null)
        {
            TempData["Error"] = "Message validation failed.";
            return RedirectToAction(nameof(Messages));
        }

        var canChat = await _dbContext.Enrollments.AnyAsync(e =>
            e.StudentId == user.Id &&
            e.Status == EnrollmentStatus.Active &&
            e.Course != null &&
            e.Course.LecturerId == model.ReceiverId);
        if (!canChat)
        {
            TempData["Error"] = "You can only chat with lecturers of your enrolled courses.";
            return RedirectToAction(nameof(Messages));
        }

        var message = new Message
        {
            SenderId = user.Id,
            ReceiverId = model.ReceiverId,
            Content = model.Content
        };
        _dbContext.Messages.Add(message);
        await _dbContext.SaveChangesAsync();

        _dbContext.MessageNotifications.Add(new MessageNotification
        {
            MessageId = message.Id,
            UserId = model.ReceiverId,
            IsRead = false
        });
        await _dbContext.SaveChangesAsync();
        TempData["Success"] = "Message sent.";
        return RedirectToAction(nameof(Messages));
    }
}

[Authorize(Roles = "Lecturer")]
public class LecturerController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ApplicationUserManager _userManager;

    public LecturerController()
        : this(ApplicationDbContext.Create(), System.Web.HttpContext.Current.GetOwinContext().GetUserManager<ApplicationUserManager>())
    {
    }

    public LecturerController(ApplicationDbContext dbContext, ApplicationUserManager userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    private static TimeZoneInfo GetSriLankaTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Sri Lanka Standard Time"); }
        catch (TimeZoneNotFoundException)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
        }
    }

    public async Task<ActionResult> Dashboard()
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new HttpUnauthorizedResult();
        }

        ViewBag.Courses = await _dbContext.Courses.CountAsync(c => c.LecturerId == user.Id);
        ViewBag.Assignments = await _dbContext.Assignments.CountAsync(a => a.Course != null && a.Course.LecturerId == user.Id);
        return View();
    }

    public async Task<ActionResult> Courses()
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new HttpUnauthorizedResult();
        }

        var courses = await _dbContext.Courses
            .Include(c => c.CourseCategory)
            .Where(c => c.LecturerId == user.Id)
            .ToListAsync();
        return View(courses);
    }

    [HttpGet]
    public async Task<ActionResult> Assignments()
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new HttpUnauthorizedResult();
        }

        var assignments = await _dbContext.Assignments
            .Include(a => a.Course)
            .Where(a => a.Course != null && a.Course.LecturerId == user.Id)
            .ToListAsync();
        ViewBag.Courses = await _dbContext.Courses.Where(c => c.LecturerId == user.Id).ToListAsync();
        return View(assignments);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> CreateAssignment(AssignmentManageViewModel model)
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (!ModelState.IsValid || user == null)
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Assignment Error";
            TempData["ToastMessage"] = "Invalid assignment data. Please check all fields and try again.";
            return RedirectToAction(nameof(Assignments));
        }

        var isOwnCourse = await _dbContext.Courses.AnyAsync(c => c.Id == model.CourseId && c.LecturerId == user.Id);
        if (!isOwnCourse)
        {
            return new HttpStatusCodeResult(403);
        }

        string materialPath = string.Empty;
        string materialName = string.Empty;
        if (model.MaterialFile != null && model.MaterialFile.ContentLength > 0)
        {
            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".ppt", ".pptx" };
            var extension = Path.GetExtension(model.MaterialFile.FileName);
            if (string.IsNullOrWhiteSpace(extension) ||
                !allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                TempData["ToastType"] = "warning";
                TempData["ToastTitle"] = "Invalid Material";
                TempData["ToastMessage"] = "Assignment material must be PDF, Word, or PowerPoint file.";
                return RedirectToAction(nameof(Assignments));
            }

            if (model.MaterialFile.ContentLength > 10 * 1024 * 1024)
            {
                TempData["ToastType"] = "warning";
                TempData["ToastTitle"] = "File Too Large";
                TempData["ToastMessage"] = "Assignment material size must be 10MB or less.";
                return RedirectToAction(nameof(Assignments));
            }

            var uploadDirectory = HostingEnvironment.MapPath("~/uploads/assignment-materials")
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uploads", "assignment-materials");
            Directory.CreateDirectory(uploadDirectory);
            var storedName = $"{Guid.NewGuid()}{extension}";
            var storedPath = Path.Combine(uploadDirectory, storedName);
            using (var stream = System.IO.File.Create(storedPath))
            {
                model.MaterialFile.InputStream.CopyTo(stream);
            }

            materialPath = $"/uploads/assignment-materials/{storedName}";
            materialName = Path.GetFileName(model.MaterialFile.FileName);
        }

        _dbContext.Assignments.Add(new Assignment
        {
            CourseId = model.CourseId,
            Title = model.Title,
            Description = model.Description,
            DeadlineUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(model.DeadlineUtc, DateTimeKind.Unspecified),
                GetSriLankaTimeZone()),
            MaterialFilePath = materialPath,
            MaterialFileName = materialName
        });
        await _dbContext.SaveChangesAsync();

        var enrolledStudentIds = await _dbContext.Enrollments
            .Where(e => e.CourseId == model.CourseId && e.Status == EnrollmentStatus.Active)
            .Select(e => e.StudentId)
            .Distinct()
            .ToListAsync();
        if (enrolledStudentIds.Count > 0)
        {
            var deadlineSriLanka = TimeZoneInfo.ConvertTimeFromUtc(
                TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(model.DeadlineUtc, DateTimeKind.Unspecified),
                    GetSriLankaTimeZone()),
                GetSriLankaTimeZone());
            var assignmentMessage = $"[ASSIGNMENT] New coursework added: {model.Title}. Deadline: {deadlineSriLanka:yyyy-MM-dd hh:mm tt} (SLST).";

            var messages = enrolledStudentIds.Select(studentId => new Message
            {
                SenderId = user.Id,
                ReceiverId = studentId,
                Content = assignmentMessage
            }).ToList();

            _dbContext.Messages.AddRange(messages);
            await _dbContext.SaveChangesAsync();

            var notifications = messages.Select(m => new MessageNotification
            {
                MessageId = m.Id,
                UserId = m.ReceiverId,
                IsRead = false
            }).ToList();

            _dbContext.MessageNotifications.AddRange(notifications);
            await _dbContext.SaveChangesAsync();
        }

        var sriLankaNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetSriLankaTimeZone());
        TempData["ToastType"] = "success";
        TempData["ToastTitle"] = "Assignment Created";
        TempData["ToastMessage"] = $"Assignment created successfully at {sriLankaNow:yyyy-MM-dd hh:mm tt} (Sri Lanka Standard Time).";
        return RedirectToAction(nameof(Assignments));
    }

    [HttpGet]
    public async Task<ActionResult> DownloadAssignmentMaterial(int assignmentId)
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        var assignment = await _dbContext.Assignments
            .Include(a => a.Course)
            .FirstOrDefaultAsync(a => a.Id == assignmentId);

        if (user == null || assignment == null || assignment.Course?.LecturerId != user.Id)
        {
            return new HttpStatusCodeResult(403);
        }

        if (string.IsNullOrWhiteSpace(assignment.MaterialFilePath))
        {
            TempData["ToastType"] = "warning";
            TempData["ToastTitle"] = "Material Not Found";
            TempData["ToastMessage"] = "No assignment material attached.";
            return RedirectToAction(nameof(Assignments));
        }

        var relativePath = assignment.MaterialFilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = HostingEnvironment.MapPath("~/" + assignment.MaterialFilePath.TrimStart('/'))
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
        if (!System.IO.File.Exists(absolutePath))
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Download Error";
            TempData["ToastMessage"] = "Material file could not be found.";
            return RedirectToAction(nameof(Assignments));
        }

        var fileName = string.IsNullOrWhiteSpace(assignment.MaterialFileName)
            ? Path.GetFileName(absolutePath)
            : assignment.MaterialFileName;
        return File(absolutePath, "application/octet-stream", fileName);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> DeleteAssignment(int id)
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new HttpUnauthorizedResult();
        }

        var assignment = await _dbContext.Assignments.Include(a => a.Course).FirstOrDefaultAsync(a => a.Id == id);
        if (assignment == null || assignment.Course?.LecturerId != user.Id)
        {
            return new HttpStatusCodeResult(403);
        }

        _dbContext.Assignments.Remove(assignment);
        await _dbContext.SaveChangesAsync();
        TempData["ToastType"] = "success";
        TempData["ToastTitle"] = "Assignment Deleted";
        TempData["ToastMessage"] = "Assignment deleted successfully.";
        return RedirectToAction(nameof(Assignments));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> UpdateAssignment(AssignmentManageViewModel model)
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null || model.Id == null || !ModelState.IsValid)
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Update Error";
            TempData["ToastMessage"] = "Invalid assignment details provided.";
            return RedirectToAction(nameof(Assignments));
        }

        var assignment = await _dbContext.Assignments
            .Include(a => a.Course)
            .FirstOrDefaultAsync(a => a.Id == model.Id.Value);
        if (assignment == null || assignment.Course?.LecturerId != user.Id)
        {
            return new HttpStatusCodeResult(403);
        }

        var isOwnTargetCourse = await _dbContext.Courses.AnyAsync(c => c.Id == model.CourseId && c.LecturerId == user.Id);
        if (!isOwnTargetCourse)
        {
            return new HttpStatusCodeResult(403);
        }

        assignment.CourseId = model.CourseId;
        assignment.Title = model.Title;
        assignment.Description = model.Description;
        assignment.DeadlineUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(model.DeadlineUtc, DateTimeKind.Unspecified),
            GetSriLankaTimeZone());

        if (model.MaterialFile != null && model.MaterialFile.ContentLength > 0)
        {
            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".ppt", ".pptx" };
            var extension = Path.GetExtension(model.MaterialFile.FileName);
            if (string.IsNullOrWhiteSpace(extension) ||
                !allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                TempData["ToastType"] = "warning";
                TempData["ToastTitle"] = "Invalid Material";
                TempData["ToastMessage"] = "Assignment material must be PDF, Word, or PowerPoint file.";
                return RedirectToAction(nameof(Assignments));
            }

            if (model.MaterialFile.ContentLength > 10 * 1024 * 1024)
            {
                TempData["ToastType"] = "warning";
                TempData["ToastTitle"] = "File Too Large";
                TempData["ToastMessage"] = "Assignment material size must be 10MB or less.";
                return RedirectToAction(nameof(Assignments));
            }

            var uploadDirectory = HostingEnvironment.MapPath("~/uploads/assignment-materials")
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uploads", "assignment-materials");
            Directory.CreateDirectory(uploadDirectory);
            var storedName = $"{Guid.NewGuid()}{extension}";
            var storedPath = Path.Combine(uploadDirectory, storedName);
            using (var stream = System.IO.File.Create(storedPath))
            {
                model.MaterialFile.InputStream.CopyTo(stream);
            }

            if (!string.IsNullOrWhiteSpace(assignment.MaterialFilePath))
            {
                var oldRelative = assignment.MaterialFilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var oldAbsolute = HostingEnvironment.MapPath("~/" + assignment.MaterialFilePath.TrimStart('/'))
                    ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, oldRelative);
                if (System.IO.File.Exists(oldAbsolute))
                {
                    System.IO.File.Delete(oldAbsolute);
                }
            }

            assignment.MaterialFilePath = $"/uploads/assignment-materials/{storedName}";
            assignment.MaterialFileName = Path.GetFileName(model.MaterialFile.FileName);
        }

        await _dbContext.SaveChangesAsync();

        var updatedEnrolledStudentIds = await _dbContext.Enrollments
            .Where(e => e.CourseId == assignment.CourseId && e.Status == EnrollmentStatus.Active)
            .Select(e => e.StudentId)
            .Distinct()
            .ToListAsync();
        if (updatedEnrolledStudentIds.Count > 0)
        {
            var updatedDeadlineSriLanka = TimeZoneInfo.ConvertTimeFromUtc(
                assignment.DeadlineUtc,
                GetSriLankaTimeZone());
            var updateMessage = $"[ASSIGNMENT] Coursework updated: {assignment.Title}. Updated deadline: {updatedDeadlineSriLanka:yyyy-MM-dd hh:mm tt} (SLST).";

            var updateMessages = updatedEnrolledStudentIds.Select(studentId => new Message
            {
                SenderId = user.Id,
                ReceiverId = studentId,
                Content = updateMessage
            }).ToList();

            _dbContext.Messages.AddRange(updateMessages);
            await _dbContext.SaveChangesAsync();

            var updateNotifications = updateMessages.Select(m => new MessageNotification
            {
                MessageId = m.Id,
                UserId = m.ReceiverId,
                IsRead = false
            }).ToList();

            _dbContext.MessageNotifications.AddRange(updateNotifications);
            await _dbContext.SaveChangesAsync();
        }

        TempData["ToastType"] = "success";
        TempData["ToastTitle"] = "Assignment Updated";
        TempData["ToastMessage"] = "Assignment updated successfully.";
        return RedirectToAction(nameof(Assignments));
    }

    public async Task<ActionResult> Submissions(int assignmentId, string search)
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new HttpUnauthorizedResult();
        }

        var assignment = await _dbContext.Assignments.Include(a => a.Course).FirstOrDefaultAsync(a => a.Id == assignmentId);
        if (assignment == null || assignment.Course?.LecturerId != user.Id)
        {
            return new HttpStatusCodeResult(403);
        }

        ViewBag.AssignmentId = assignmentId;
        ViewBag.SearchTerm = search ?? string.Empty;

        var submissionsQuery = _dbContext.AssignmentSubmissions
            .Include(s => s.Student)
            .Include(s => s.Grade)
            .Where(s => s.AssignmentId == assignmentId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            submissionsQuery = submissionsQuery.Where(s =>
                (s.Student != null && (
                    (!string.IsNullOrWhiteSpace(s.Student.FullName) && s.Student.FullName.Contains(term)) ||
                    (!string.IsNullOrWhiteSpace(s.Student.Email) && s.Student.Email.Contains(term))
                ))
            );
        }

        var submissions = await submissionsQuery.ToListAsync();

        // Grade mapping uses AssignmentSubmissionId in SQL schema, so load explicitly.
        var submissionIds = submissions.Select(s => s.Id).ToList();
        if (submissionIds.Count > 0)
        {
            var grades = await _dbContext.Grades
                .Where(g => submissionIds.Contains(g.AssignmentSubmissionId))
                .ToListAsync();
            var gradeLookup = grades.ToDictionary(g => g.AssignmentSubmissionId, g => g);
            foreach (var submission in submissions)
            {
                if (gradeLookup.TryGetValue(submission.Id, out var grade))
                {
                    submission.Grade = grade;
                }
            }
        }

        return View(submissions);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> SaveGrade(GradeManageViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid grade details.";
            return RedirectToAction(nameof(Assignments));
        }

        var submission = await _dbContext.AssignmentSubmissions
            .Include(s => s.Assignment)
            .Include(s => s.Assignment.Course)
            .Include(s => s.Grade)
            .FirstOrDefaultAsync(s => s.Id == model.SubmissionId);
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new HttpUnauthorizedResult();
        }

        if (submission == null || submission.Assignment?.Course?.LecturerId != user.Id)
        {
            return new HttpStatusCodeResult(403);
        }

        await _dbContext.Database.ExecuteSqlCommandAsync(
            @"IF EXISTS (SELECT 1 FROM [dbo].[tblGrade] WHERE [AssignmentSubmissionId] = @AssignmentSubmissionId)
              BEGIN
                  UPDATE [dbo].[tblGrade]
                  SET [Score] = @Score, [Feedback] = @Feedback
                  WHERE [AssignmentSubmissionId] = @AssignmentSubmissionId
              END
              ELSE
              BEGIN
                  INSERT INTO [dbo].[tblGrade] ([AssignmentSubmissionId], [Score], [Feedback])
                  VALUES (@AssignmentSubmissionId, @Score, @Feedback)
              END",
            new SqlParameter("@AssignmentSubmissionId", submission.Id),
            new SqlParameter("@Score", model.Score),
            new SqlParameter("@Feedback", model.Feedback ?? string.Empty));

        TempData["Success"] = "Grade saved.";
        return RedirectToAction(nameof(Submissions), new { assignmentId = submission.AssignmentId });
    }

    [HttpGet]
    public async Task<ActionResult> Messages()
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new HttpUnauthorizedResult();
        }

        var allowedStudentIds = await _dbContext.Enrollments
            .Where(e => e.Status == EnrollmentStatus.Active && e.Course != null && e.Course.LecturerId == user.Id)
            .Select(e => e.StudentId)
            .Distinct()
            .ToListAsync();

        var messages = await _dbContext.Messages
            .Include(m => m.Sender)
            .Where(m =>
                (m.SenderId == user.Id && allowedStudentIds.Contains(m.ReceiverId)) ||
                (m.ReceiverId == user.Id && allowedStudentIds.Contains(m.SenderId)))
            .OrderByDescending(m => m.SentAtUtc)
            .Take(50)
            .ToListAsync();

        var allowedStudents = await _dbContext.Users
            .Where(u => allowedStudentIds.Contains(u.Id))
            .OrderBy(u => u.FullName)
            .ToListAsync();
        ViewBag.Students = allowedStudents;
        ViewBag.CurrentUserId = user?.Id ?? string.Empty;
        return View(messages);
    }

    [HttpGet]
    public async Task<ActionResult> Conversation(string partnerId)
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null || string.IsNullOrWhiteSpace(partnerId))
        {
            return Json(Array.Empty<object>(), JsonRequestBehavior.AllowGet);
        }

        var canChat = await _dbContext.Enrollments.AnyAsync(e =>
            e.Status == EnrollmentStatus.Active &&
            e.StudentId == partnerId &&
            e.Course != null &&
            e.Course.LecturerId == user.Id);
        if (!canChat)
        {
            return new HttpStatusCodeResult(403);
        }

        var messages = await _dbContext.Messages
            .Include(m => m.Sender)
            .Where(m =>
                (m.SenderId == user.Id && m.ReceiverId == partnerId) ||
                (m.SenderId == partnerId && m.ReceiverId == user.Id))
            .OrderBy(m => m.SentAtUtc)
            .Take(200)
            .ToListAsync();

        return Json(messages.Select(m => new
        {
            id = m.Id,
            senderId = m.SenderId,
            senderName = m.Sender?.FullName ?? "User",
            content = m.Content,
            sentAt = m.SentAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
        }), JsonRequestBehavior.AllowGet);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> SendMessageAjax(string receiverId, string content)
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new HttpUnauthorizedResult();
        }

        if (string.IsNullOrWhiteSpace(receiverId) || string.IsNullOrWhiteSpace(content))
        {
            return new HttpStatusCodeResult(400, "Receiver and message are required.");
        }

        var canChat = await _dbContext.Enrollments.AnyAsync(e =>
            e.Status == EnrollmentStatus.Active &&
            e.StudentId == receiverId &&
            e.Course != null &&
            e.Course.LecturerId == user.Id);
        if (!canChat)
        {
            return new HttpStatusCodeResult(403);
        }

        var message = new Message
        {
            SenderId = user.Id,
            ReceiverId = receiverId,
            Content = content.Trim()
        };
        _dbContext.Messages.Add(message);
        await _dbContext.SaveChangesAsync();
        _dbContext.MessageNotifications.Add(new MessageNotification
        {
            MessageId = message.Id,
            UserId = receiverId,
            IsRead = false
        });
        await _dbContext.SaveChangesAsync();

        return Json(new { success = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> SendMessage(MessageCreateViewModel model)
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (!ModelState.IsValid || user == null)
        {
            TempData["Error"] = "Message validation failed.";
            return RedirectToAction(nameof(Messages));
        }

        var canChat = await _dbContext.Enrollments.AnyAsync(e =>
            e.Status == EnrollmentStatus.Active &&
            e.StudentId == model.ReceiverId &&
            e.Course != null &&
            e.Course.LecturerId == user.Id);
        if (!canChat)
        {
            TempData["Error"] = "You can only chat with students in your courses.";
            return RedirectToAction(nameof(Messages));
        }

        var message = new Message { SenderId = user.Id, ReceiverId = model.ReceiverId, Content = model.Content };
        _dbContext.Messages.Add(message);
        await _dbContext.SaveChangesAsync();
        _dbContext.MessageNotifications.Add(new MessageNotification { MessageId = message.Id, UserId = model.ReceiverId });
        await _dbContext.SaveChangesAsync();
        TempData["Success"] = "Message sent.";
        return RedirectToAction(nameof(Messages));
    }

    [HttpGet]
    public async Task<ActionResult> OpenNotification(int notificationId)
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new HttpUnauthorizedResult();
        }

        var notification = await _dbContext.MessageNotifications
            .Include(n => n.Message)
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == user.Id);
        if (notification == null || notification.Message == null)
        {
            TempData["ToastType"] = "warning";
            TempData["ToastTitle"] = "Notification";
            TempData["ToastMessage"] = "Notification not found or unavailable.";
            return RedirectToAction(nameof(Dashboard));
        }

        notification.IsRead = true;
        await _dbContext.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(notification.Message.Content) &&
            notification.Message.Content.StartsWith("[ASSIGNMENT]", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Assignments));
        }

        return RedirectToAction(nameof(Messages));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> MarkAllNotificationsRead()
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new HttpUnauthorizedResult();
        }

        var unreadNotifications = await _dbContext.MessageNotifications
            .Where(n => n.UserId == user.Id && !n.IsRead)
            .ToListAsync();
        if (unreadNotifications.Count > 0)
        {
            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
            }
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Dashboard));
    }

    [HttpGet]
    public async Task<ActionResult> Reports()
    {
        return View(await BuildLecturerReportsViewModelAsync());
    }

    [HttpGet]
    public async Task<ActionResult> ExportReportPdf(string reportType = "all-reports")
    {
        var vm = await BuildLecturerReportsViewModelAsync();
        var normalizedType = string.IsNullOrWhiteSpace(reportType) ? "all-reports" : reportType.Trim().ToLowerInvariant();
        var reportTitle = normalizedType switch
        {
            "student-performance" => "Student Performance Report",
            "course-popularity" => "Course Popularity Report",
            _ => "Lecturer Reports"
        };

        var generatedAt = DateTime.Now;
        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(header =>
                {
                    header.Spacing(5);
                    header.Item().Text("UniManage").Bold().FontSize(22).FontColor(Colors.Blue.Darken2);
                    header.Item().Text("Lecturer Reports").SemiBold().FontSize(14);
                    header.Item().Text(reportTitle).FontSize(12).FontColor(Colors.Grey.Darken2);
                    header.Item().Text($"Generated: {generatedAt:yyyy-MM-dd HH:mm}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    header.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(10).Column(content =>
                {
                    content.Spacing(14);
                    bool includeAll = normalizedType == "all-reports";

                    if (includeAll || normalizedType == "student-performance")
                    {
                        content.Item().Text("Student Performance").SemiBold().FontSize(12);
                        if (!vm.StudentPerformance.Any())
                        {
                            content.Item().Text("No student performance data available.").FontColor(Colors.Grey.Darken1);
                        }
                        else
                        {
                            content.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(2);
                                    columns.ConstantColumn(100);
                                    columns.ConstantColumn(100);
                                });
                                table.Header(header =>
                                {
                                    header.Cell().Padding(6).Background(Colors.Blue.Lighten4).Text("Student").SemiBold();
                                    header.Cell().Padding(6).Background(Colors.Blue.Lighten4).AlignRight().Text("Submissions").SemiBold();
                                    header.Cell().Padding(6).Background(Colors.Blue.Lighten4).AlignRight().Text("Avg Score").SemiBold();
                                });
                                foreach (var item in vm.StudentPerformance)
                                {
                                    var studentText = $"{item.StudentName} ({item.StudentEmail})";
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Text(studentText);
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).AlignRight().Text(item.GradedSubmissions.ToString());
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).AlignRight().Text(item.AverageScore.ToString("N2"));
                                }
                            });
                        }
                    }

                    if (includeAll || normalizedType == "course-popularity")
                    {
                        content.Item().Text("Course Popularity").SemiBold().FontSize(12);
                        if (!vm.CoursePopularity.Any())
                        {
                            content.Item().Text("No course enrollment data available.").FontColor(Colors.Grey.Darken1);
                        }
                        else
                        {
                            content.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn();
                                    columns.ConstantColumn(90);
                                });
                                table.Header(header =>
                                {
                                    header.Cell().Padding(6).Background(Colors.Blue.Lighten4).Text("Course").SemiBold();
                                    header.Cell().Padding(6).Background(Colors.Blue.Lighten4).AlignRight().Text("Enrollments").SemiBold();
                                });
                                foreach (var item in vm.CoursePopularity)
                                {
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Text(item.CourseTitle);
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).AlignRight().Text(item.EnrollmentCount.ToString());
                                }
                            });
                        }
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Generated by UniManage Lecturer Portal").FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();

        var fileName = $"UniManage-Lecturer-{normalizedType}-Report-{DateTime.Now:yyyyMMddHHmmss}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    private async Task<LecturerReportsViewModel> BuildLecturerReportsViewModelAsync()
    {
        var user = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (user == null)
        {
            return new LecturerReportsViewModel();
        }

        var studentPerformance = await _dbContext.Grades
            .Include(g => g.AssignmentSubmission)
            .Include(g => g.AssignmentSubmission.Student)
            .Include(g => g.AssignmentSubmission)
            .Include(g => g.AssignmentSubmission.Assignment)
            .Include(g => g.AssignmentSubmission.Assignment.Course)
            .Where(g => g.AssignmentSubmission != null &&
                        g.AssignmentSubmission.Student != null &&
                        g.AssignmentSubmission.Assignment != null &&
                        g.AssignmentSubmission.Assignment.Course != null &&
                        g.AssignmentSubmission.Assignment.Course.LecturerId == user.Id)
            .GroupBy(g => new
            {
                StudentId = g.AssignmentSubmission.StudentId,
                StudentName = g.AssignmentSubmission.Student.FullName,
                StudentEmail = g.AssignmentSubmission.Student.Email
            })
            .Select(g => new StudentPerformanceItemViewModel
            {
                StudentName = g.Key.StudentName,
                StudentEmail = g.Key.StudentEmail ?? string.Empty,
                GradedSubmissions = g.Count(),
                AverageScore = g.Average(x => x.Score)
            })
            .OrderByDescending(x => x.AverageScore)
            .ThenByDescending(x => x.GradedSubmissions)
            .Take(20)
            .ToListAsync();

        var coursePopularity = await _dbContext.Enrollments
            .Include(e => e.Course)
            .Where(e => e.Course != null && e.Course.LecturerId == user.Id)
            .GroupBy(e => new { e.CourseId, Title = e.Course.Title })
            .Select(g => new CoursePopularityItemViewModel
            {
                CourseTitle = g.Key.Title,
                EnrollmentCount = g.Count()
            })
            .OrderByDescending(x => x.EnrollmentCount)
            .ToListAsync();

        return new LecturerReportsViewModel
        {
            StudentPerformance = studentPerformance,
            CoursePopularity = coursePopularity
        };
    }
}

[Authorize(Roles = "Administrator")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ApplicationUserManager _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly string _webRootPath;

    public AdminController()
        : this(
            ApplicationDbContext.Create(),
            System.Web.HttpContext.Current.GetOwinContext().GetUserManager<ApplicationUserManager>(),
            new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(ApplicationDbContext.Create())),
            HostingEnvironment.MapPath("~/") ?? string.Empty)
    {
    }

    public AdminController(
        ApplicationDbContext dbContext,
        ApplicationUserManager userManager,
        RoleManager<IdentityRole> roleManager,
        string webRootPath)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _webRootPath = webRootPath;
    }

    private static string NormalizeGender(string value)
    {
        if (string.Equals(value, "Male", StringComparison.OrdinalIgnoreCase))
        {
            return "Male";
        }
        if (string.Equals(value, "Female", StringComparison.OrdinalIgnoreCase))
        {
            return "Female";
        }
        return "Prefer not to say";
    }

    private static bool IsAllowedImage(HttpPostedFileBase imageFile)
    {
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(imageFile.FileName);
        return !string.IsNullOrWhiteSpace(extension) &&
               allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private string SaveCourseImage(HttpPostedFileBase imageFile)
    {
        var uploadsFolder = HostingEnvironment.MapPath("~/uploads/courses") ?? Path.Combine(_webRootPath ?? string.Empty, "uploads", "courses");
        Directory.CreateDirectory(uploadsFolder);
        var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            imageFile.InputStream.CopyTo(stream);
        }
        return $"/uploads/courses/{fileName}";
    }

    public async Task<ActionResult> Dashboard()
    {
        var monthStartUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var nextMonthStartUtc = monthStartUtc.AddMonths(1);
        var dailyPaidRevenue = await _dbContext.Payments
            .Where(p => p.Status == PaymentStatus.Paid && p.PaidAtUtc >= monthStartUtc)
            .Where(p => p.PaidAtUtc < nextMonthStartUtc)
            .GroupBy(p => new { p.PaidAtUtc.Year, p.PaidAtUtc.Month, p.PaidAtUtc.Day })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Day,
                Revenue = g.Sum(x => x.Amount)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ThenBy(x => x.Day)
            .ToListAsync();

        var chartLabels = dailyPaidRevenue
            .Select(x => new DateTime(x.Year, x.Month, x.Day).ToString("dd MMM", CultureInfo.InvariantCulture))
            .ToList();
        var chartValues = dailyPaidRevenue.Select(x => x.Revenue).ToList();

        ViewBag.Users = await _userManager.Users.CountAsync();
        ViewBag.Courses = await _dbContext.Courses.CountAsync();
        ViewBag.Payments = await _dbContext.Payments.CountAsync();
        ViewBag.TotalPayments = (await _dbContext.Payments
            .Where(p => p.Status == PaymentStatus.Paid)
            .Select(p => (decimal?)p.Amount)
            .SumAsync()) ?? 0m;
        ViewBag.PaymentChartLabels = JsonConvert.SerializeObject(chartLabels);
        ViewBag.PaymentChartValues = JsonConvert.SerializeObject(chartValues);
        return View();
    }

    public async Task<ActionResult> Users(string search)
    {
        var searchTerm = string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim();
        var currentUser = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        var usersQuery = _userManager.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            usersQuery = usersQuery.Where(u =>
                u.FullName.Contains(searchTerm) ||
                (u.Email != null && u.Email.Contains(searchTerm)) ||
                u.Gender.Contains(searchTerm));
        }

        var users = await usersQuery.OrderBy(u => u.Email).ToListAsync();
        var rows = new List<AdminUserRowViewModel>();
        foreach (var user in users)
        {
            var roleName = (await _userManager.GetRolesAsync(user.Id)).FirstOrDefault() ?? "Student";
            rows.Add(new AdminUserRowViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Gender = user.Gender,
                DateOfBirth = user.DateOfBirth,
                RoleName = roleName,
                IsCurrentUser = currentUser != null && user.Id == currentUser.Id
            });
        }

        ViewBag.SearchTerm = searchTerm;
        return View(new AdminUsersPageViewModel
        {
            Users = rows,
            CreateUser = new AdminUserManageViewModel()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> CreateUser(AdminUserManageViewModel model)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Password))
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Create User Failed";
            TempData["ToastMessage"] = "Please provide valid details and password.";
            return RedirectToAction(nameof(Users));
        }

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Create User Failed";
            TempData["ToastMessage"] = "A user with this email already exists.";
            return RedirectToAction(nameof(Users));
        }

        if (!await _roleManager.RoleExistsAsync(model.RoleName))
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Create User Failed";
            TempData["ToastMessage"] = "Selected role is not available.";
            return RedirectToAction(nameof(Users));
        }

        var user = new ApplicationUser
        {
            FullName = model.FullName,
            UserName = model.Email,
            Email = model.Email,
            Gender = NormalizeGender(model.Gender),
            DateOfBirth = model.DateOfBirth,
            Address = string.Empty
        };
        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Create User Failed";
            TempData["ToastMessage"] = string.Join(" ", createResult.Errors);
            return RedirectToAction(nameof(Users));
        }

        await _userManager.AddToRoleAsync(user.Id, model.RoleName);
        TempData["ToastType"] = "success";
        TempData["ToastTitle"] = "User Created";
        TempData["ToastMessage"] = "New user account has been created successfully.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> UpdateUser(AdminUserManageViewModel model)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Id))
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Update Failed";
            TempData["ToastMessage"] = "Please provide valid user details.";
            return RedirectToAction(nameof(Users));
        }

        var targetUser = await _userManager.FindByIdAsync(model.Id);
        if (targetUser == null)
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Update Failed";
            TempData["ToastMessage"] = "User not found.";
            return RedirectToAction(nameof(Users));
        }

        var currentUser = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (currentUser != null &&
            targetUser.Id == currentUser.Id &&
            !string.Equals(model.RoleName, "Administrator", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Update Failed";
            TempData["ToastMessage"] = "You cannot remove Administrator role from your own account.";
            return RedirectToAction(nameof(Users));
        }

        targetUser.FullName = model.FullName;
        targetUser.Email = model.Email;
        targetUser.UserName = model.Email;
        targetUser.Gender = NormalizeGender(model.Gender);
        targetUser.DateOfBirth = model.DateOfBirth;

        var updateResult = await _userManager.UpdateAsync(targetUser);
        if (!updateResult.Succeeded)
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Update Failed";
            TempData["ToastMessage"] = string.Join(" ", updateResult.Errors);
            return RedirectToAction(nameof(Users));
        }

        var currentRoles = await _userManager.GetRolesAsync(targetUser.Id);
        if (!currentRoles.Contains(model.RoleName))
        {
            await _userManager.RemoveFromRolesAsync(targetUser.Id, currentRoles.ToArray());
            await _userManager.AddToRoleAsync(targetUser.Id, model.RoleName);
        }

        TempData["ToastType"] = "success";
        TempData["ToastTitle"] = "User Updated";
        TempData["ToastMessage"] = "User details updated successfully.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> DeleteUser(string id)
    {
        var currentUser = await _userManager.FindByIdAsync(User.Identity.GetUserId());
        if (currentUser != null && id == currentUser.Id)
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Delete Failed";
            TempData["ToastMessage"] = "You cannot delete your own administrator account.";
            return RedirectToAction(nameof(Users));
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Delete Failed";
            TempData["ToastMessage"] = "User not found.";
            return RedirectToAction(nameof(Users));
        }

        var deleteResult = await _userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Delete Failed";
            TempData["ToastMessage"] = string.Join(" ", deleteResult.Errors);
            return RedirectToAction(nameof(Users));
        }

        TempData["ToastType"] = "success";
        TempData["ToastTitle"] = "User Deleted";
        TempData["ToastMessage"] = "User removed successfully.";
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<ActionResult> Courses(string search)
    {
        var searchTerm = string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim();
        ViewBag.Categories = await _dbContext.CourseCategories.OrderBy(c => c.Name).ToListAsync();
        var lecturerRole = await _roleManager.FindByNameAsync("Lecturer");
        var lecturers = new List<ApplicationUser>();
        if (lecturerRole != null)
        {
            var lecturerUserIds = _dbContext.Users
                .Where(u => u.Roles.Any(r => r.RoleId == lecturerRole.Id))
                .Select(u => u.Id);
            lecturers = await _dbContext.Users
                .Where(u => lecturerUserIds.Contains(u.Id))
                .OrderBy(u => u.FullName)
                .ToListAsync();
        }
        ViewBag.Lecturers = lecturers;
        var coursesQuery = _dbContext.Courses
            .Include(c => c.CourseCategory)
            .Include(c => c.Lecturer)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            coursesQuery = coursesQuery.Where(c =>
                c.Title.Contains(searchTerm) ||
                c.Description.Contains(searchTerm) ||
                (c.CourseCategory != null && c.CourseCategory.Name.Contains(searchTerm)) ||
                (c.Lecturer != null && c.Lecturer.FullName.Contains(searchTerm)));
        }

        var courses = await coursesQuery
            .OrderByDescending(c => c.Id)
            .ToListAsync();

        ViewBag.SearchTerm = searchTerm;
        return View(courses);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> CreateCourse(CourseManageViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Course Error";
            TempData["ToastMessage"] = "Course validation failed.";
            return RedirectToAction(nameof(Courses));
        }

        if (model.ImageFile == null || model.ImageFile.ContentLength == 0)
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Course Error";
            TempData["ToastMessage"] = "Course image is required.";
            return RedirectToAction(nameof(Courses));
        }

        if (!IsAllowedImage(model.ImageFile))
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Course Error";
            TempData["ToastMessage"] = "Only JPG, PNG, or WEBP images are allowed.";
            return RedirectToAction(nameof(Courses));
        }

        if (model.ImageFile.ContentLength > 5 * 1024 * 1024)
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Course Error";
            TempData["ToastMessage"] = "Image size must be 5MB or less.";
            return RedirectToAction(nameof(Courses));
        }

        var imagePath = SaveCourseImage(model.ImageFile);

        _dbContext.Courses.Add(new Course
        {
            Title = model.Title,
            Description = model.Description,
            Price = model.Price,
            CourseCategoryId = model.CourseCategoryId,
            LecturerId = model.LecturerId,
            ImageUrl = imagePath
        });
        await _dbContext.SaveChangesAsync();
        TempData["ToastType"] = "success";
        TempData["ToastTitle"] = "Course Created";
        TempData["ToastMessage"] = "Course added successfully.";
        return RedirectToAction(nameof(Courses));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> UpdateCourse(CourseManageViewModel model)
    {
        if (!ModelState.IsValid || !model.Id.HasValue)
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Course Error";
            TempData["ToastMessage"] = "Please provide valid course details.";
            return RedirectToAction(nameof(Courses));
        }

        var course = await _dbContext.Courses.FindAsync(model.Id.Value);
        if (course == null)
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Course Error";
            TempData["ToastMessage"] = "Course not found.";
            return RedirectToAction(nameof(Courses));
        }

        if (model.ImageFile != null && model.ImageFile.ContentLength > 0)
        {
            if (!IsAllowedImage(model.ImageFile))
            {
                TempData["ToastType"] = "danger";
                TempData["ToastTitle"] = "Course Error";
                TempData["ToastMessage"] = "Only JPG, PNG, or WEBP images are allowed.";
                return RedirectToAction(nameof(Courses));
            }

            if (model.ImageFile.ContentLength > 5 * 1024 * 1024)
            {
                TempData["ToastType"] = "danger";
                TempData["ToastTitle"] = "Course Error";
                TempData["ToastMessage"] = "Image size must be 5MB or less.";
                return RedirectToAction(nameof(Courses));
            }

            course.ImageUrl = SaveCourseImage(model.ImageFile);
        }

        course.Title = model.Title;
        course.Description = model.Description;
        course.Price = model.Price;
        course.CourseCategoryId = model.CourseCategoryId;
        course.LecturerId = model.LecturerId;

        await _dbContext.SaveChangesAsync();
        TempData["ToastType"] = "success";
        TempData["ToastTitle"] = "Course Updated";
        TempData["ToastMessage"] = "Course updated successfully.";
        return RedirectToAction(nameof(Courses));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> DeleteCourse(int id)
    {
        var course = await _dbContext.Courses.FindAsync(id);
        if (course == null)
        {
            return HttpNotFound();
        }

        _dbContext.Courses.Remove(course);
        await _dbContext.SaveChangesAsync();
        TempData["ToastType"] = "success";
        TempData["ToastTitle"] = "Course Deleted";
        TempData["ToastMessage"] = "Course deleted successfully.";
        return RedirectToAction(nameof(Courses));
    }

    [HttpGet]
    public async Task<ActionResult> Categories()
    {
        return View(await _dbContext.CourseCategories.OrderBy(c => c.Name).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> CreateCategory(CategoryManageViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Category Error";
            TempData["ToastMessage"] = "Category validation failed.";
            return RedirectToAction(nameof(Categories));
        }

        _dbContext.CourseCategories.Add(new CourseCategory { Name = model.Name });
        await _dbContext.SaveChangesAsync();
        TempData["ToastType"] = "success";
        TempData["ToastTitle"] = "Category Created";
        TempData["ToastMessage"] = "Category added successfully.";
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> UpdateCategory(CategoryManageViewModel model)
    {
        if (!ModelState.IsValid || !model.Id.HasValue)
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Category Error";
            TempData["ToastMessage"] = "Please provide valid category details.";
            return RedirectToAction(nameof(Categories));
        }

        var category = await _dbContext.CourseCategories.FindAsync(model.Id.Value);
        if (category == null)
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Category Error";
            TempData["ToastMessage"] = "Category not found.";
            return RedirectToAction(nameof(Categories));
        }

        category.Name = model.Name;
        await _dbContext.SaveChangesAsync();
        TempData["ToastType"] = "success";
        TempData["ToastTitle"] = "Category Updated";
        TempData["ToastMessage"] = "Category updated successfully.";
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> DeleteCategory(int id)
    {
        var category = await _dbContext.CourseCategories.FindAsync(id);
        if (category == null)
        {
            return HttpNotFound();
        }

        _dbContext.CourseCategories.Remove(category);
        await _dbContext.SaveChangesAsync();
        TempData["ToastType"] = "success";
        TempData["ToastTitle"] = "Category Deleted";
        TempData["ToastMessage"] = "Category deleted successfully.";
        return RedirectToAction(nameof(Categories));
    }

    [HttpGet]
    public async Task<ActionResult> Reports()
    {
        return View(await BuildAdminReportsViewModelAsync());
    }

    [HttpGet]
    public async Task<ActionResult> ExportReportPdf(string reportType = "all-reports")
    {
        var vm = await BuildAdminReportsViewModelAsync();
        var normalizedType = string.IsNullOrWhiteSpace(reportType) ? "all-reports" : reportType.Trim().ToLowerInvariant();
        var reportTitle = normalizedType switch
        {
            "course-popularity" => "Course Popularity Report",
            "student-performance" => "Student Performance Report",
            "payment-report" => "Payment Report",
            "enrollment-statistics" => "Enrollment Statistics Report",
            _ => "All Reports"
        };

        var generatedAt = DateTime.Now;
        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(header =>
                {
                    header.Spacing(5);
                    header.Item().Text("UniManage").Bold().FontSize(22).FontColor(Colors.Blue.Darken2);
                    header.Item().Text("University Course Management System").FontSize(11).FontColor(Colors.Grey.Darken1);
                    header.Item().Text(reportTitle).SemiBold().FontSize(15);
                    header.Item().Text($"Generated: {generatedAt:yyyy-MM-dd HH:mm}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    header.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(10).Column(content =>
                {
                    content.Spacing(14);

                    bool includeAll = normalizedType == "all-reports";

                    if (includeAll || normalizedType == "course-popularity")
                    {
                        content.Item().Text("Course Popularity").SemiBold().FontSize(12);
                        if (!vm.TopCourses.Any())
                        {
                            content.Item().Text("No enrollment data available.").FontColor(Colors.Grey.Darken1);
                        }
                        else
                        {
                            content.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn();
                                    columns.ConstantColumn(90);
                                });
                                table.Header(header =>
                                {
                                    header.Cell().Padding(6).Background(Colors.Blue.Lighten4).Text("Course").SemiBold();
                                    header.Cell().Padding(6).Background(Colors.Blue.Lighten4).AlignRight().Text("Enrollments").SemiBold();
                                });
                                foreach (var item in vm.TopCourses)
                                {
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Text(item.CourseTitle);
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).AlignRight().Text(item.EnrollmentCount.ToString());
                                }
                            });
                        }
                    }

                    if (includeAll || normalizedType == "student-performance")
                    {
                        content.Item().Text("Student Performance").SemiBold().FontSize(12);
                        if (!vm.StudentPerformance.Any())
                        {
                            content.Item().Text("No graded submission data available.").FontColor(Colors.Grey.Darken1);
                        }
                        else
                        {
                            content.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(2);
                                    columns.ConstantColumn(100);
                                    columns.ConstantColumn(100);
                                });
                                table.Header(header =>
                                {
                                    header.Cell().Padding(6).Background(Colors.Blue.Lighten4).Text("Student").SemiBold();
                                    header.Cell().Padding(6).Background(Colors.Blue.Lighten4).AlignRight().Text("Submissions").SemiBold();
                                    header.Cell().Padding(6).Background(Colors.Blue.Lighten4).AlignRight().Text("Avg Score").SemiBold();
                                });
                                foreach (var item in vm.StudentPerformance)
                                {
                                    var studentText = $"{item.StudentName} ({item.StudentEmail})";
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Text(studentText);
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).AlignRight().Text(item.GradedSubmissions.ToString());
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).AlignRight().Text(item.AverageScore.ToString("N2"));
                                }
                            });
                        }
                    }

                    if (includeAll || normalizedType == "payment-report")
                    {
                        content.Item().Text("Payment Report (Last 6 Months)").SemiBold().FontSize(12);
                        if (!vm.MonthlyPayments.Any())
                        {
                            content.Item().Text("No paid transactions found in recent months.").FontColor(Colors.Grey.Darken1);
                        }
                        else
                        {
                            content.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn();
                                    columns.ConstantColumn(120);
                                });
                                table.Header(header =>
                                {
                                    header.Cell().Padding(6).Background(Colors.Blue.Lighten4).Text("Month").SemiBold();
                                    header.Cell().Padding(6).Background(Colors.Blue.Lighten4).AlignRight().Text("Revenue (LKR)").SemiBold();
                                });
                                foreach (var month in vm.MonthlyPayments)
                                {
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Text(month.MonthLabel);
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).AlignRight().Text(month.Revenue.ToString("N2"));
                                }
                            });
                        }

                        content.Item().Text("Payment Status Summary").SemiBold().FontSize(11);
                        if (!vm.PaymentStatusSummary.Any())
                        {
                            content.Item().Text("No payment status data available.").FontColor(Colors.Grey.Darken1);
                        }
                        else
                        {
                            content.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn();
                                    columns.ConstantColumn(70);
                                    columns.ConstantColumn(120);
                                });
                                table.Header(header =>
                                {
                                    header.Cell().Padding(6).Background(Colors.Blue.Lighten4).Text("Status").SemiBold();
                                    header.Cell().Padding(6).Background(Colors.Blue.Lighten4).AlignRight().Text("Count").SemiBold();
                                    header.Cell().Padding(6).Background(Colors.Blue.Lighten4).AlignRight().Text("Amount (LKR)").SemiBold();
                                });
                                foreach (var item in vm.PaymentStatusSummary)
                                {
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Text(item.Status);
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).AlignRight().Text(item.Count.ToString());
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).AlignRight().Text(item.Amount.ToString("N2"));
                                }
                            });
                        }
                    }

                    if (includeAll || normalizedType == "enrollment-statistics")
                    {
                        content.Item().Text("Enrollment Statistics").SemiBold().FontSize(12);
                        if (!vm.EnrollmentStatusSummary.Any())
                        {
                            content.Item().Text("No enrollment data available.").FontColor(Colors.Grey.Darken1);
                        }
                        else
                        {
                            content.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn();
                                    columns.ConstantColumn(90);
                                });
                                table.Header(header =>
                                {
                                    header.Cell().Padding(6).Background(Colors.Blue.Lighten4).Text("Status").SemiBold();
                                    header.Cell().Padding(6).Background(Colors.Blue.Lighten4).AlignRight().Text("Count").SemiBold();
                                });
                                foreach (var item in vm.EnrollmentStatusSummary)
                                {
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Text(item.Status);
                                    table.Cell().Padding(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).AlignRight().Text(item.Count.ToString());
                                }
                            });
                        }
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Generated by UniManage Admin Reports").FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();

        var safeType = normalizedType.Replace(" ", "-");
        var fileName = $"UniManage-{safeType}-Report-{DateTime.Now:yyyyMMddHHmmss}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    private async Task<AdminReportsViewModel> BuildAdminReportsViewModelAsync()
    {
        var monthStartUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-5);

        var topCourses = await _dbContext.Enrollments
            .Include(e => e.Course)
            .Where(e => e.Course != null)
            .GroupBy(e => new { e.CourseId, Title = e.Course.Title })
            .Select(g => new CoursePopularityItemViewModel
            {
                CourseTitle = g.Key.Title,
                EnrollmentCount = g.Count()
            })
            .OrderByDescending(x => x.EnrollmentCount)
            .Take(5)
            .ToListAsync();

        var monthlyPayments = await _dbContext.Payments
            .Where(p => p.Status == PaymentStatus.Paid && p.PaidAtUtc >= monthStartUtc)
            .GroupBy(p => new { p.PaidAtUtc.Year, p.PaidAtUtc.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Revenue = g.Sum(x => x.Amount)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync();

        var monthlyPaymentItems = monthlyPayments
            .Select(x => new MonthlyPaymentItemViewModel
            {
                MonthLabel = new DateTime(x.Year, x.Month, 1).ToString("MMM yyyy", CultureInfo.InvariantCulture),
                Revenue = x.Revenue
            })
            .ToList();

        var studentPerformance = await _dbContext.Grades
            .Include(g => g.AssignmentSubmission)
            .Include(g => g.AssignmentSubmission.Student)
            .Where(g => g.AssignmentSubmission != null && g.AssignmentSubmission.Student != null)
            .GroupBy(g => new
            {
                StudentId = g.AssignmentSubmission.StudentId,
                StudentName = g.AssignmentSubmission.Student.FullName,
                StudentEmail = g.AssignmentSubmission.Student.Email
            })
            .Select(g => new StudentPerformanceItemViewModel
            {
                StudentName = g.Key.StudentName,
                StudentEmail = g.Key.StudentEmail ?? string.Empty,
                GradedSubmissions = g.Count(),
                AverageScore = g.Average(x => x.Score)
            })
            .OrderByDescending(x => x.AverageScore)
            .ThenByDescending(x => x.GradedSubmissions)
            .Take(10)
            .ToListAsync();

        var paymentStatusSummary = await _dbContext.Payments
            .GroupBy(p => p.Status)
            .Select(g => new PaymentStatusItemViewModel
            {
                Status = g.Key.ToString(),
                Count = g.Count(),
                Amount = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        var enrollmentStatusSummary = await _dbContext.Enrollments
            .GroupBy(e => e.Status)
            .Select(g => new EnrollmentStatusItemViewModel
            {
                Status = g.Key.ToString(),
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        return new AdminReportsViewModel
        {
            TotalUsers = await _userManager.Users.CountAsync(),
            TotalCourses = await _dbContext.Courses.CountAsync(),
            TotalEnrollments = await _dbContext.Enrollments.CountAsync(),
            ActiveEnrollments = await _dbContext.Enrollments.CountAsync(e => e.Status == EnrollmentStatus.Active),
            TotalRevenue = (await _dbContext.Payments
                .Where(p => p.Status == PaymentStatus.Paid)
                .Select(p => (decimal?)p.Amount)
                .SumAsync()) ?? 0m,
            TotalPayments = await _dbContext.Payments.CountAsync(),
            TopCourses = topCourses,
            MonthlyPayments = monthlyPaymentItems,
            StudentPerformance = studentPerformance,
            PaymentStatusSummary = paymentStatusSummary,
            EnrollmentStatusSummary = enrollmentStatusSummary
        };
    }

    [HttpGet]
    public async Task<ActionResult> SystemUsage(string search)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var searchTerm = string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim();

        var logsQuery = _dbContext.SystemUsageLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            logsQuery = logsQuery.Where(l =>
                (l.UserName != null && l.UserName.Contains(searchTerm)) ||
                (l.UserId != null && l.UserId.Contains(searchTerm)));
        }

        var logs = await logsQuery
            .OrderByDescending(l => l.Id)
            .Take(500)
            .ToListAsync();

        var userIds = logs
            .Where(l => !string.IsNullOrWhiteSpace(l.UserId))
            .Select(l => l.UserId)
            .Distinct()
            .ToList();

        var userDisplayNames = await _dbContext.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.UserName,
                u.Email
            })
            .ToDictionaryAsync(
                u => u.Id,
                u =>
                {
                    if (!string.IsNullOrWhiteSpace(u.FullName))
                    {
                        return u.FullName;
                    }

                    var candidate = !string.IsNullOrWhiteSpace(u.UserName) ? u.UserName : u.Email;
                    if (string.IsNullOrWhiteSpace(candidate))
                    {
                        return "Unknown User";
                    }

                    var atIndex = candidate.IndexOf("@", StringComparison.Ordinal);
                    return atIndex > 0 ? candidate.Substring(0, atIndex) : candidate;
                });

        var vm = new SystemUsagePageViewModel
        {
            TotalLogs = await _dbContext.SystemUsageLogs.CountAsync(),
            UniqueUsers = await _dbContext.SystemUsageLogs
                .Where(l => l.UserId != null && l.UserId != string.Empty)
                .Select(l => l.UserId)
                .Distinct()
                .CountAsync(),
            TodayLogs = await _dbContext.SystemUsageLogs.CountAsync(l => l.LoggedAtUtc >= todayUtc),
            SearchTerm = searchTerm,
            FilteredLogs = logs.Count,
            Logs = logs.Select(l => new SystemUsageLogRowViewModel
            {
                Id = l.Id,
                UserName = !string.IsNullOrWhiteSpace(l.UserId) && userDisplayNames.ContainsKey(l.UserId)
                    ? userDisplayNames[l.UserId]
                    : (!string.IsNullOrWhiteSpace(l.UserName)
                        ? (l.UserName.Contains("@") ? l.UserName.Split('@')[0] : l.UserName)
                        : "Unknown User"),
                RoleName = l.RoleName,
                ActionType = l.ActionType,
                ControllerName = l.ControllerName,
                ActionName = l.ActionName,
                HttpMethod = l.HttpMethod,
                StatusCode = l.StatusCode,
                IpAddress = l.IpAddress,
                LoggedAtUtc = l.LoggedAtUtc
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> ClearSystemUsageLogs()
    {
        var logs = await _dbContext.SystemUsageLogs.ToListAsync();
        if (logs.Any())
        {
            _dbContext.SystemUsageLogs.RemoveRange(logs);
            await _dbContext.SaveChangesAsync();
        }

        TempData["ToastType"] = "success";
        TempData["ToastTitle"] = "Logs Cleared";
        TempData["ToastMessage"] = "All system usage logs were cleared successfully.";
        return RedirectToAction(nameof(SystemUsage));
    }

    [HttpGet]
    public async Task<ActionResult> EnrollmentStatistics()
    {
        var topCourses = await _dbContext.Enrollments
            .Include(e => e.Course)
            .Where(e => e.Course != null)
            .GroupBy(e => new { e.CourseId, Title = e.Course.Title })
            .Select(g => new CoursePopularityItemViewModel
            {
                CourseTitle = g.Key.Title,
                EnrollmentCount = g.Count()
            })
            .OrderByDescending(x => x.EnrollmentCount)
            .Take(10)
            .ToListAsync();

        var vm = new EnrollmentStatisticsPageViewModel
        {
            TotalEnrollments = await _dbContext.Enrollments.CountAsync(),
            ActiveEnrollments = await _dbContext.Enrollments.CountAsync(e => e.Status == EnrollmentStatus.Active),
            PendingEnrollments = await _dbContext.Enrollments.CountAsync(e => e.Status == EnrollmentStatus.Pending),
            CancelledEnrollments = await _dbContext.Enrollments.CountAsync(e => e.Status == EnrollmentStatus.Cancelled),
            TopCourses = topCourses
        };

        return View(vm);
    }

    public async Task<ActionResult> Payments(string search)
    {
        var searchTerm = string.IsNullOrWhiteSpace(search) ? string.Empty : search.Trim();
        var paymentsQuery = _dbContext.Payments
            .Include(p => p.Enrollment)
            .Include(p => p.Enrollment.Course)
            .Include(p => p.Enrollment)
            .Include(p => p.Enrollment.Student)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var hasStatusFilter = Enum.TryParse<PaymentStatus>(searchTerm, true, out var parsedStatus);
            paymentsQuery = paymentsQuery.Where(p =>
                (p.Enrollment != null && p.Enrollment.Student != null &&
                    (
                        (!string.IsNullOrWhiteSpace(p.Enrollment.Student.FullName) && p.Enrollment.Student.FullName.Contains(searchTerm)) ||
                        (!string.IsNullOrWhiteSpace(p.Enrollment.Student.Email) && p.Enrollment.Student.Email.Contains(searchTerm))
                    )) ||
                (p.Enrollment != null && p.Enrollment.Course != null &&
                    !string.IsNullOrWhiteSpace(p.Enrollment.Course.Title) &&
                    p.Enrollment.Course.Title.Contains(searchTerm)) ||
                (hasStatusFilter && p.Status == parsedStatus));
        }

        var payments = await paymentsQuery
            .OrderByDescending(p => p.Id)
            .ToListAsync();

        var vm = new AdminPaymentsPageViewModel
        {
            Payments = payments.Select(p => new AdminPaymentRowViewModel
            {
                Id = p.Id,
                StudentName = p.Enrollment?.Student?.FullName ?? "N/A",
                StudentEmail = p.Enrollment?.Student?.Email ?? "N/A",
                CourseTitle = p.Enrollment?.Course?.Title ?? "N/A",
                Amount = p.Amount,
                Status = p.Status.ToString(),
                PaidAtUtc = p.PaidAtUtc,
                CanGeneratePdf = p.Status == PaymentStatus.Paid
            }).ToList()
        };

        ViewBag.SearchTerm = searchTerm;
        return View(vm);
    }

    [HttpGet]
    public async Task<ActionResult> DownloadPaymentReceipt(int id)
    {
        var payment = await _dbContext.Payments
            .Include(p => p.Enrollment)
            .Include(p => p.Enrollment.Course)
            .Include(p => p.Enrollment)
            .Include(p => p.Enrollment.Student)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment == null)
        {
            TempData["ToastType"] = "danger";
            TempData["ToastTitle"] = "Receipt Error";
            TempData["ToastMessage"] = "Payment record not found.";
            return RedirectToAction(nameof(Payments));
        }

        if (payment.Status != PaymentStatus.Paid)
        {
            TempData["ToastType"] = "warning";
            TempData["ToastTitle"] = "Receipt Not Available";
            TempData["ToastMessage"] = "PDF receipt can only be generated for paid payments.";
            return RedirectToAction(nameof(Payments));
        }

        var paidAtLocal = payment.PaidAtUtc.ToLocalTime();
        var studentName = payment.Enrollment?.Student?.FullName ?? "N/A";
        var studentEmail = payment.Enrollment?.Student?.Email ?? "N/A";
        var courseTitle = payment.Enrollment?.Course?.Title ?? "N/A";

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(header =>
                {
                    header.Item().Text("UniManage").Bold().FontSize(24).FontColor(Colors.Blue.Darken2);
                    header.Item().Text("University Course Management System").FontSize(12).FontColor(Colors.Grey.Darken1);
                    header.Item().PaddingTop(4).Text("Payment Receipt").SemiBold().FontSize(16);
                    header.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(12).Column(content =>
                {
                    content.Spacing(10);
                    content.Item().Text($"Receipt No: UM-PAY-{payment.Id:D6}").SemiBold();
                    content.Item().Text($"Issued Date: {DateTime.Now:yyyy-MM-dd HH:mm}");

                    content.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(140);
                            columns.RelativeColumn();
                        });

                        void Row(string label, string value)
                        {
                            table.Cell().PaddingVertical(5).PaddingRight(8).Text(label).SemiBold();
                            table.Cell().PaddingVertical(5).Text(value);
                        }

                        Row("Student Name", studentName);
                        Row("Student Email", studentEmail);
                        Row("Course", courseTitle);
                        Row("Amount", $"LKR {payment.Amount:N2}");
                        Row("Status", payment.Status.ToString());
                        Row("Paid At", paidAtLocal.ToString("yyyy-MM-dd HH:mm:ss"));
                        Row("Stripe Session", string.IsNullOrWhiteSpace(payment.StripeSessionId) ? "N/A" : payment.StripeSessionId);
                        Row("Payment Intent", string.IsNullOrWhiteSpace(payment.StripePaymentIntentId) ? "N/A" : payment.StripePaymentIntentId);
                    });

                    content.Item().PaddingTop(12).Border(1).BorderColor(Colors.Blue.Lighten3).Padding(10).Text(
                        "Thank you for your payment. This is an official UniManage receipt for your enrollment."
                    ).FontColor(Colors.Grey.Darken2);
                });

                page.Footer().Column(footer =>
                {
                    footer.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                    footer.Item().PaddingTop(6).Text("UniManage Support: support@unimanage.edu | +94 11 234 5678")
                        .FontSize(10)
                        .FontColor(Colors.Grey.Darken1);
                    footer.Item().Text("Generated by UniManage Payment System").FontSize(9).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf();

        var fileName = $"UniManage-Receipt-{payment.Id}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }
}
}

