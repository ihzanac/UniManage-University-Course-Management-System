using System;
using System.Linq;
using System.Web.Mvc;
using System.Web;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using UniManage.Data;
using UniManage.Models;

namespace UniManage.App_Start
{
    public class SystemUsageLogFilter : ActionFilterAttribute
    {
        private static readonly string[] IgnoredPathPrefixes = { "/lib", "/css", "/js", "/images", "/uploads", "/favicon" };

        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            var http = filterContext.HttpContext;
            if (http?.User?.Identity?.IsAuthenticated != true)
            {
                return;
            }

            var path = http.Request?.Path ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path) || IgnoredPathPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            if (path.IndexOf("/DownloadPaymentReceipt", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            try
            {
                using (var db = ApplicationDbContext.Create())
                {
                    var identity = http.User.Identity;
                    var userId = identity.GetUserId() ?? string.Empty;
                    var userName = string.IsNullOrWhiteSpace(identity.Name) ? "Unknown User" : identity.Name;
                    var roleName = "Unknown";
                    if (http.User.IsInRole("Administrator"))
                    {
                        roleName = "Administrator";
                    }
                    else if (http.User.IsInRole("Lecturer"))
                    {
                        roleName = "Lecturer";
                    }
                    else if (http.User.IsInRole("Student"))
                    {
                        roleName = "Student";
                    }

                    db.SystemUsageLogs.Add(new SystemUsageLog
                    {
                        UserId = userId,
                        UserName = userName,
                        RoleName = roleName,
                        ActionType = http.Request.HttpMethod == "POST" ? "Create/Update" : "Read",
                        ControllerName = (string)filterContext.RouteData.Values["controller"] ?? string.Empty,
                        ActionName = (string)filterContext.RouteData.Values["action"] ?? string.Empty,
                        Path = path,
                        HttpMethod = http.Request.HttpMethod,
                        StatusCode = http.Response.StatusCode,
                        IpAddress = http.Request.UserHostAddress ?? "N/A",
                        Details = "Request logged from MVC filter",
                        LoggedAtUtc = DateTime.UtcNow
                    });
                    db.SaveChanges();
                }
            }
            catch
            {
            }
        }
    }
}
