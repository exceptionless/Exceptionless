#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace Exceptionless.Mvc {
    public class ExceptionlessHandleErrorAttribute : HandleErrorAttribute {
        private readonly object _typeId = new object();

        public bool HasWrappedHandler { get { return WrappedHandler != null; } }

        public override object TypeId { get { return _typeId; } }

        public IExceptionFilter WrappedHandler { get; set; }

        public override void OnException(ExceptionContext filterContext) {
            if (HasWrappedHandler)
                WrappedHandler.OnException(filterContext);
            else
                base.OnException(filterContext);

            var result = filterContext.Result as ViewResult;
            if (result != null) {
                string id = ExceptionlessClient.Current.GetLastErrorId();
                if (!String.IsNullOrEmpty(id))
                    result.ViewBag.ExceptionlessIdentifier = id;
            }

            var contextData = new Dictionary<string, object> {
                { "HttpContext", filterContext.HttpContext }
            };

            ExceptionlessClient.Current.ProcessUnhandledException(filterContext.Exception, "HandleErrorAttribute", true, contextData);
        }
    }
}