using System;

namespace Exceptionless.Core.Authentication {
	public interface IDomainLoginProvider {
		bool IsLoginValid(string username, string password);
		string GetEmailForLogin(string username);
		string GetNameForLogin(string username);
	}
}
