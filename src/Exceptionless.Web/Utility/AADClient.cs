using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OAuth2.Client;
using OAuth2.Configuration;
using OAuth2.Infrastructure;
using OAuth2.Models;
using RestSharp;
using RestSharp.Authenticators;
using Endpoint = OAuth2.Client.Endpoint;

namespace Exceptionless.Web.Utility
{
    public class AADClient : OAuth2Client
    {
        private const string BaseURI = "https://login.microsoftonline.com";
        private static string? TenentId;

        private readonly IRequestFactory _factory;
        /// <summary>
        /// Initializes a new instance of the <see cref="AADClient"/> class.
        /// </summary>
        /// <param name="factory">The factory.</param>
        /// <param name="configuration">The configuration.</param>
        public AADClient(IRequestFactory factory, IClientConfiguration configuration)
            : base(factory, configuration)
        {
            _factory = factory;
            TenentId = "tenantID";
        }

        protected override void BeforeGetAccessToken(BeforeAfterRequestArgs args)
        {
            args.Request.AddObject(new
            {
                code = args.Parameters.GetOrThrowUnexpectedResponse("code"),
                client_id = Configuration.ClientId,
                client_secret = Configuration.ClientSecret,
                redirect_uri = Configuration.RedirectUri,
                grant_type = "authorization_code",
                resource = "https://graph.microsoft.com/"
            });
        }

        /// <summary>
        /// Should return parsed <see cref="UserInfo"/> from content received from third-party service.
        /// </summary>
        /// <param name="content">The content which is received from third-party service.</param>
        protected override UserInfo ParseUserInfo(string content)
		{
			var response = JObject.Parse(content);

			return new UserInfo
			{
				Id = response["id"]?.Value<string>() ?? string.Empty, // Ensure null safety with null-coalescing operator
				Email = response["userPrincipalName"]?.SafeGet(x => x.Value<string>()) ?? string.Empty, // Ensure null safety
				FirstName = response["givenName"]?.Value<string>() ?? string.Empty, // Ensure null safety
				LastName = response["surname"]?.Value<string>() ?? string.Empty // Ensure null safety
			};
		}

        /// <summary>
        /// Friendly name of provider (OAuth2 service).
        /// </summary>
        public override string Name
        {
            get { return "AAD"; }
        }

        /// <summary>
        /// Defines URI of service which issues access code.
        /// </summary>
        protected override Endpoint AccessCodeServiceEndpoint
        {
            get { return new Endpoint { BaseUri = BaseURI, Resource = "/" + TenentId + "/oauth2/authorize" }; }
        }

        /// <summary>
        /// Defines URI of service which issues access token.
        /// </summary>
        protected override Endpoint AccessTokenServiceEndpoint
        {
            get { return new Endpoint { BaseUri = BaseURI, Resource = "/" + TenentId + "/oauth2/token" }; }
        }

        /// <summary>
        /// Defines URI of service which allows to obtain information about user which is currently logged in.
        /// </summary>
        protected override Endpoint UserInfoServiceEndpoint
        {
            get { return new Endpoint { BaseUri = "https://graph.microsoft.com", Resource = "/v1.0/me" }; }
        }

        /// <summary>
        /// Encoding the user input
        /// </summary>
        public static string HtmlEncode(string name)
        {
            StringBuilder sbName = new StringBuilder();
            sbName.Append(HttpUtility.HtmlEncode(name));
            name = sbName.ToString();
            return name;
        }
		protected override async Task<UserInfo> GetUserInfoAsync(CancellationToken cancellationToken = default)
		{
			var client = _factory.CreateClient(UserInfoServiceEndpoint);
			var request = _factory.CreateRequest(UserInfoServiceEndpoint);
			request.AddHeader("Authorization", string.Format("bearer {0}", AccessToken));

			BeforeGetUserInfo(new BeforeAfterRequestArgs
			{
				Client = client,
				Request = request,
				Configuration = Configuration
			});

			try
			{
				var response = await client.ExecuteAsync(request, Method.GET, cancellationToken);
				var result = ParseUserInfo(response.Content);
				result.ProviderName = Name;

				return result;
			}
			catch (Exception)
			{
				return new UserInfo();
			}
		}
    }
}
