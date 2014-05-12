#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Configuration;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using CodeSmith.Core.Scheduler;
using Exceptionless.Core;
using Exceptionless.Core.Migrations;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;
using Microsoft.AspNet.SignalR;
using MongoDB.Driver;
using NLog;
using NLog.Fluent;

namespace Exceptionless.App {
    public class GlobalApplication : HttpApplication {
        protected void Application_Start() {
            AreaRegistration.RegisterAllAreas();

            RedisConnectionInfo redisInfo = Settings.Current.RedisConnectionInfo;
            if (Settings.Current.EnableSignalR && redisInfo != null) {
                var config = new RedisScaleoutConfiguration(redisInfo.Host, redisInfo.Port, redisInfo.Password, "exceptionless.signalr");
                GlobalHost.DependencyResolver.UseRedis(config);
            }

            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            AuthConfig.RegisterAuth();

            BundleConfig.RegisterBundles(BundleTable.Bundles);

            AutoMapperConfig.CreateMappings();

            Trace.Listeners.Add(new NLogTraceListener());
            if (ExceptionlessClient.Current.Configuration.Enabled) {
                //ExceptionlessClient.Current.Log = new NLogExceptionlessLog();
                //ExceptionlessClient.Current.RegisterWebApi(GlobalConfiguration.Configuration);
                ExceptionlessClient.Current.UnhandledExceptionReporting += CurrentOnUnhandledExceptionReporting;
            }

            // startup the message queue
            JobManager.Current.JobManagerStarted += (sender, args) => JobManager.Current.RunJob("StartMq");

            // make the notification sender listen for messages
            var notificationSender = DependencyResolver.Current.GetService<NotificationSender>();
            notificationSender.Listen();
        }

        private void CurrentOnUnhandledExceptionReporting(object sender, UnhandledExceptionReportingEventArgs args) {
            if (args.Exception.GetType() == typeof(OperationCanceledException)
                || args.Exception.GetType() == typeof(TaskCanceledException)) {
                args.Cancel = true;
                return;
            }

            var p = Thread.CurrentPrincipal as ExceptionlessPrincipal;
            if (p == null)
                return;

            if (p.Project != null)
                args.Event.AddObject(p.Project, "Project");

            if (p.UserEntity != null)
                args.Event.AddObject(p.UserEntity, "User");
        }

        private static bool? _dbIsUpToDate;
        private static DateTime _lastDbUpToDateCheck;
        private static readonly object _dbIsUpToDateLock = new object();

        public static bool IsDbUpToDate() {
            lock (_dbIsUpToDateLock) {
                if (_dbIsUpToDate.HasValue && (_dbIsUpToDate.Value || DateTime.Now.Subtract(_lastDbUpToDateCheck).TotalSeconds < 10))
                    return _dbIsUpToDate.Value;

                _lastDbUpToDateCheck = DateTime.Now;

                string connectionString = ConfigurationManager.ConnectionStrings["MongoConnectionString"].ConnectionString;
                if (String.IsNullOrEmpty(connectionString))
                    throw new ConfigurationErrorsException("MongoConnectionString was not found in the Web.config.");

                var url = new MongoUrl(connectionString);
                string databaseName = url.DatabaseName;
                if (Settings.Current.AppendMachineNameToDatabase)
                    databaseName += String.Concat("-", Environment.MachineName.ToLower());

                _dbIsUpToDate = MongoMigrationChecker.IsUpToDate(connectionString, databaseName);
                if (_dbIsUpToDate.Value)
                    return true;

                // if enabled, auto upgrade the database
                if (Settings.Current.ShouldAutoUpgradeDatabase)
                    Task.Factory.StartNew(() => MongoMigrationChecker.EnsureLatest(connectionString, databaseName))
                        .ContinueWith(_ => { _dbIsUpToDate = false; });

                return false;
            }
        }

        private static bool _isDbDown = false;
        private static DateTime _lastDbCheck;
        private static bool _isCacheDown = false;
        private static DateTime _lastCacheCheck;

        public void MarkDbDown() {
            _isDbDown = true;
            _lastDbCheck = DateTime.Now;
        }

        private void CheckDbOrCacheDown() {
            // make sure we are still listening for events
            var notificationSender = DependencyResolver.Current.GetService<NotificationSender>();
            notificationSender.EnsureListening();

            // check if the cache is down every 5 seconds or every request if it's currently marked as down
            if (_isCacheDown || DateTime.Now.Subtract(_lastCacheCheck).TotalSeconds > 5) {
                try {
                    var cache = DependencyResolver.Current.GetService<ICacheClient>();
                    var ping = cache.Get<string>("__PING__");
                    _isCacheDown = false;
                    _lastCacheCheck = DateTime.Now;
                } catch (RedisException) {
                    _isCacheDown = true;
                    _lastCacheCheck = DateTime.Now;
                }
            }

            if (!_isDbDown && !_isCacheDown)
                return;

            // check every 10 seconds to see if the db is back up
            if (_isDbDown && DateTime.Now.Subtract(_lastDbCheck).TotalSeconds > 10) {
                _lastDbCheck = DateTime.Now;
                try {
                    var userRepository = DependencyResolver.Current.GetService<IUserRepository>();
                    long userCount = userRepository.Count();
                    _isDbDown = false;
                } catch {}
            }

            if (_isDbDown || _isCacheDown)
                RedirectToMaintenancePage();
        }

        private void RedirectToMaintenancePage() {
            string path = Request.Path.ToLower();
            if (path.Equals("/status"))
                return;

            if (path.StartsWith("/api")) {
                Response.Clear();
                Response.StatusCode = 503;
                Response.End();
            }

            if (!path.Equals("/maintenance") && RequestRequiresAuth())
                Response.Redirect("/maintenance?src=" + Server.UrlEncode(Request.RawUrl));
        }

        private bool RequestRequiresAuth() {
            string path = Request.Path.ToLower();
            if (path.Equals("/maintenance")
                || path.StartsWith("/images")
                || path.StartsWith("/scripts")
                || path.StartsWith("/content")
                || path.StartsWith("/status")
                || path.Equals("/favicon.ico"))
                return false;

            return true;
        }

        protected void Application_BeginRequest(Object sender, EventArgs e) {
            if (!IsDbUpToDate())
                RedirectToMaintenancePage();

            if (RequestRequiresAuth())
                CheckDbOrCacheDown();
        }

        protected void Application_PostAuthenticateRequest(Object sender, EventArgs e) {
            if (!RequestRequiresAuth())
                return;

            if (!User.Identity.IsAuthenticated)
                return;

            if (User is ExceptionlessPrincipal)
                return;

            CheckDbOrCacheDown();

            try {
                var userRepository = DependencyResolver.Current.GetService<IUserRepository>();
                User user = userRepository.GetByEmailAddress(User.Identity.Name);
                if (user == null) {
                    FormsAuthentication.SignOut();
                    FormsAuthentication.RedirectToLoginPage();
                    return;
                }

                var principal = new ExceptionlessPrincipal(user);
                Thread.CurrentPrincipal = principal;
                if (HttpContext.Current != null)
                    HttpContext.Current.User = principal;
            } catch (MongoConnectionException ex) {
                Log.Error().Exception(ex).Message("Error getting user: {0}", ex.Message).Report().Write();
                MarkDbDown();
                RedirectToMaintenancePage();
            } catch (SocketException ex) {
                Log.Error().Exception(ex).Message("Error getting user: {0}", ex.Message).Report().Write();
                MarkDbDown();
                RedirectToMaintenancePage();
            }
        }

        protected void Application_Error(Object sender, EventArgs e) {
            Exception error = Server.GetLastError();
            if (error == null)
                return;

            if (error is HttpAntiForgeryException) {
                Server.ClearError();
                Response.Redirect("~/account/logoff", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            Log.Error().Exception(error).Message("Application error.").Write();
        }
    }
}