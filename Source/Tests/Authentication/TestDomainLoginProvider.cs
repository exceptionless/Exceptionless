using System;
using Exceptionless.Core.Authentication;

namespace Exceptionless.Api.Tests.Authentication
{
	internal class TestDomainLoginProvider : IDomainLoginProvider {
		public const string ValidUsername = "user1";
		public const string ValidPassword = "password1!!";
		
		public bool IsLoginValid(string username, string password) {
			return username == ValidUsername && password == ValidPassword;
		}

		public string GetEmailForLogin(string username) {
			return username + "@domain.com";
		}

		public string GetNameForLogin(string username) {
			return username + " " + username.ToUpper();
		}
	}
}
