using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using UniManage.Data;
using UniManage.Models;

namespace UniManage
{
    public class ApplicationUserManager : UserManager<ApplicationUser>
    {
        public ApplicationUserManager(IUserStore<ApplicationUser> store) : base(store)
        {
        }

        public static ApplicationUserManager Create(IdentityFactoryOptions<ApplicationUserManager> options, IOwinContext context)
        {
            var manager = new ApplicationUserManager(new UserStore<ApplicationUser>(context.Get<ApplicationDbContext>()));
            manager.PasswordHasher = new HybridPasswordHasher();
            manager.UserValidator = new UserValidator<ApplicationUser>(manager)
            {
                AllowOnlyAlphanumericUserNames = false,
                RequireUniqueEmail = true
            };
            manager.PasswordValidator = new PasswordValidator
            {
                RequiredLength = 8,
                RequireDigit = false,
                RequireLowercase = false,
                RequireUppercase = false,
                RequireNonLetterOrDigit = false
            };

            if (options.DataProtectionProvider != null)
            {
                var dataProtector = options.DataProtectionProvider.Create("UniManage.Identity");
                manager.UserTokenProvider = new DataProtectorTokenProvider<ApplicationUser>(dataProtector);
            }

            return manager;
        }
    }

    public class ApplicationSignInManager : SignInManager<ApplicationUser, string>
    {
        public ApplicationSignInManager(ApplicationUserManager userManager, IAuthenticationManager authenticationManager)
            : base(userManager, authenticationManager)
        {
        }

        public static ApplicationSignInManager Create(IdentityFactoryOptions<ApplicationSignInManager> options, IOwinContext context)
        {
            return new ApplicationSignInManager(context.GetUserManager<ApplicationUserManager>(), context.Authentication);
        }
    }

    public static class RoleSeed
    {
        public static void EnsureRolesAndAdmin()
        {
            using (var db = ApplicationDbContext.Create())
            {
                var roleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(db));
                var userManager = new ApplicationUserManager(new UserStore<ApplicationUser>(db));
                foreach (var role in new[] { "Student", "Lecturer", "Administrator" })
                {
                    if (!roleManager.RoleExists(role))
                    {
                        roleManager.Create(new IdentityRole(role));
                    }
                }

                var adminEmail = "admin@unimanage.local";
                var admin = userManager.FindByEmail(adminEmail);
                if (admin == null)
                {
                    admin = new ApplicationUser
                    {
                        FullName = "System Administrator",
                        UserName = adminEmail,
                        Email = adminEmail,
                        EmailConfirmed = true
                    };

                    var createResult = userManager.Create(admin, "Admin@12345");
                    if (createResult.Succeeded)
                    {
                        userManager.AddToRole(admin.Id, "Administrator");
                    }
                }
            }
        }
    }
}
