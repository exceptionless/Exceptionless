using System;
using System.Security.Claims;
using Exceptionless.Api.Providers;
using Microsoft.AspNet.Identity;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Google;
using Microsoft.Owin.Security.OAuth;
using Owin;

namespace Exceptionless.Api {
    public partial class Startup2 {
        // For more information on configuring authentication, please visit http://go.microsoft.com/fwlink/?LinkId=301864
        public void ConfigureAuth(IAppBuilder app) {
            // Configure the db context and user manager to use a single instance per request
            //app.CreatePerOwinContext<ApplicationUserManager>(ApplicationUserManager.Create);

            // Enable the application to use bearer tokens to authenticate users
            //app.UseOAuthAuthorizationServer(new OAuthAuthorizationServerOptions {
            //    TokenEndpointPath = new PathString("/token"),
            //    AuthorizeEndpointPath = new PathString("/account/authorize"),
            //    Provider = new ExceptionlessOAuthProvider(),
            //    AccessTokenExpireTimeSpan = TimeSpan.FromDays(14),
            //    AllowInsecureHttp = true,
            //    AccessTokenProvider = new ExceptionlessTokenProvider(),
            //    RefreshTokenProvider = new ExceptionlessTokenProvider(),
            //    AuthorizationCodeProvider = new ExceptionlessTokenProvider()
            //});

            //app.UseOAuthBearerAuthentication(new OAuthBearerAuthenticationOptions {
            //    Provider = new ExceptionlessAuthenticationProvider(),
            //    AccessTokenProvider = new ExceptionlessTokenProvider(),
            //    AuthenticationMode = AuthenticationMode.Active
            //});
            // Uncomment the following lines to enable logging in with third party login providers
            //app.UseMicrosoftAccountAuthentication(
            //    clientId: "",
            //    clientSecret: "");

            //app.UseTwitterAuthentication(
            //    consumerKey: "",
            //    consumerSecret: "");

            //app.UseFacebookAuthentication(
            //    appId: "",
            //    appSecret: "");

            //app.UseGoogleAuthentication(new GoogleOAuth2AuthenticationOptions {
            //    Provider = new ExceptionlessGoogleOAuth2AuthenticationProvider()
            //});
        }
    }
}
