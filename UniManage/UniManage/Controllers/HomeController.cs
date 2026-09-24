using System;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Data.Entity;
using UniManage.Data;
using UniManage.Models;
using UniManage.Services;
using UniManage.ViewModels;

namespace UniManage.Controllers
{
public class HomeController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IEmailService _emailService;

    public HomeController()
    {
        _dbContext = ApplicationDbContext.Create();
        _emailService = new SmtpEmailService();
    }

    public HomeController(ApplicationDbContext dbContext, IEmailService emailService)
    {
        _dbContext = dbContext;
        _emailService = emailService;
    }

    public async Task<ActionResult> Index()
    {
        var latestCourses = await _dbContext.Courses
            .Include(c => c.CourseCategory)
            .Include(c => c.Lecturer)
            .OrderByDescending(c => c.Id)
            .Take(6)
            .ToListAsync();
        return View(latestCourses);
    }

    public ActionResult Privacy()
    {
        return View();
    }

    public ActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Guid.NewGuid().ToString("N") });
    }

    [HttpGet]
    public ActionResult About() => View();

    [HttpGet]
    public ActionResult Contact() => View(new ContactViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> Contact(ContactViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        _dbContext.ContactMessages.Add(new ContactMessage
        {
            Name = model.Name,
            Email = model.Email,
            Message = model.Message
        });
        await _dbContext.SaveChangesAsync();

        var contactRecipient = ConfigurationManager.AppSettings["Contact:ToEmail"];
        if (string.IsNullOrWhiteSpace(contactRecipient))
        {
            contactRecipient = ConfigurationManager.AppSettings["Smtp:Username"];
        }

        if (string.IsNullOrWhiteSpace(contactRecipient))
        {
            TempData["ContactError"] = "Message saved, but email is not configured. Set Contact:ToEmail (or SMTP settings) in Web.config.";
            return RedirectToAction(nameof(Contact));
        }

        try
        {
            await _emailService.SendAsync(contactRecipient, "UniManage Contact Form", $"<p>{model.Name} ({model.Email}) sent:<br/>{model.Message}</p>");
            TempData["ContactSuccess"] = "Message sent successfully.";
        }
        catch
        {
            TempData["ContactError"] = "Message saved, but email delivery failed. Please verify SMTP settings in Web.config.";
        }

        return RedirectToAction(nameof(Contact));
    }
}
}
