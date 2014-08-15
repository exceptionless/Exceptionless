using Microsoft.AspNet.Identity;
using MongoDB.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;

namespace Exceptionless.Api {
    public class ApplicationUserManager : UserManager<IdentityUser> {
        public ApplicationUserManager(IUserStore<IdentityUser> store)
            : base(store) {
        }

        public static ApplicationUserManager Create(IdentityFactoryOptions<ApplicationUserManager> options, IOwinContext context) {
            //var manager = new ApplicationUserManager(new UserStore<IdentityUser, IdentityRole, >());
            //// Configure validation logic for usernames
            //manager.UserValidator = new UserValidator<IdentityUser>(manager) {
            //    AllowOnlyAlphanumericUserNames = false,
            //    RequireUniqueEmail = true
            //};
            //// Configure validation logic for passwords
            //manager.PasswordValidator = new PasswordValidator {
            //    RequiredLength = 6,
            //    RequireNonLetterOrDigit = true,
            //    RequireDigit = true,
            //    RequireLowercase = true,
            //    RequireUppercase = true,
            //};
            //var dataProtectionProvider = options.DataProtectionProvider;
            //if (dataProtectionProvider != null)
            //    manager.UserTokenProvider = new DataProtectorTokenProvider<IdentityUser>(dataProtectionProvider.Create("ASP.NET Identity"));

            //return manager;
            return null;
        }
    }
}
