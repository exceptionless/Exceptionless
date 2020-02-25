namespace Exceptionless.Web.Models {
    public class ResetPasswordModel {
        public string PasswordResetToken { get; set; }
        public string Password { get; set; }
    }
}