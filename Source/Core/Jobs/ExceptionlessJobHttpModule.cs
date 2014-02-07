#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Web;
using System.Web.Mvc;
using IDependencyResolver = CodeSmith.Core.Dependency.IDependencyResolver;

namespace CodeSmith.Core.Scheduler {
    public class ExceptionlessJobHttpModule : JobHttpModule {
        public override void Init(HttpApplication context) {
            var resolver = DependencyResolver.Current.GetService<IDependencyResolver>();
            JobManager.Current.SetDependencyResolver(resolver);
            base.Init(context);
        }
    }
}