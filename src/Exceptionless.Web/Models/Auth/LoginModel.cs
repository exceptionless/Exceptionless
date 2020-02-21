namespace Exceptionless.Web.Models {
    public class LoginModel {
        public string Email { get; set; }
        public string Password { get; set; }
        public string InviteToken { get; set; }
    }
}