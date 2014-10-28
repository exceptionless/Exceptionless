using System;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using Exceptionless.App.Hubs;
using Exceptionless.Core;
using ServiceStack.CacheAccess;

namespace Exceptionless.App.Controllers {
    public class StatusController : Controller {
        private readonly ICacheClient _cacheClient;
        private readonly NotificationSender _notificationSender;
        private readonly IUserRepository _userRepository;

        public StatusController(ICacheClient cacheClient, NotificationSender notificationSender, IUserRepository userRepository) {
            _cacheClient = cacheClient;
            _notificationSender = notificationSender;
            _userRepository = userRepository;
        }

        [HttpGet]
        public ActionResult Index() {
            try {
                if (_cacheClient.Get<string>("__PING__") != null)
                    return new HttpStatusCodeResult(HttpStatusCode.ServiceUnavailable, "Cache Not Working");
            } catch (Exception ex) {
                return new HttpStatusCodeResult(HttpStatusCode.ServiceUnavailable, "Cache Not Working: " + ex.Message);
            }

            try {
                if (!GlobalApplication.IsDbUpToDate())
                    return new HttpStatusCodeResult(HttpStatusCode.ServiceUnavailable, "Mongo DB Schema Outdated");

                var user = _userRepository.All().Take(1).FirstOrDefault();
            } catch (Exception ex) {
                return new HttpStatusCodeResult(HttpStatusCode.ServiceUnavailable, "Mongo Not Working: " + ex.Message);
            }

            //if (!_notificationSender.IsListening())
            //    return new HttpStatusCodeResult(HttpStatusCode.ServiceUnavailable, "Ping Not Received");

            return new ContentResult { Content = "All Systems Check" };
        }
    }
}