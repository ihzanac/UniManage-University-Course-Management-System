using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Data.Entity;
using QuestPDF.Infrastructure;
using UniManage.Data;

namespace UniManage
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            Database.SetInitializer<ApplicationDbContext>(null);
            EnsureIdentityLockoutColumnCompatibility();
            RoleSeed.EnsureRolesAndAdmin();
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        private static void EnsureIdentityLockoutColumnCompatibility()
        {
            const string sql = @"
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'tblUsers'
      AND COLUMN_NAME = 'LockoutEnd'
      AND DATA_TYPE = 'datetimeoffset'
)
BEGIN
    ALTER TABLE [dbo].[tblUsers] ALTER COLUMN [LockoutEnd] [datetime2](7) NULL;
END";

            using (var db = ApplicationDbContext.Create())
            {
                db.Database.ExecuteSqlCommand(sql);
            }
        }
    }
}
