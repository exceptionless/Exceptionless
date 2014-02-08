#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Web.Optimization;
using BundleTransformer.Core.Builders;
using BundleTransformer.Core.Orderers;
using BundleTransformer.Core.Transformers;

namespace Exceptionless.App {
    public class BundleConfig {
        // For more information on Bundling, visit http://go.microsoft.com/fwlink/?LinkId=254725
        public static void RegisterBundles(BundleCollection bundles) {
            bundles.IgnoreList.Clear();
            AddDefaultIgnorePatterns(bundles.IgnoreList);

            Bundle mainCSSBundle = new Bundle("~/content/main.css")
                .Include("~/Content/bootstrap.css")
                .Include("~/Content/bootstrap-responsive.css")
                .Include("~/Content/bootstrap-datepicker.css")
                .Include("~/Content/bootstrap-select.css")
                .Include("~/Content/css/font-awesome.css")
                .Include("~/Content/jquery.fancybox.css")
                .Include("~/Content/toastr.less")
                .Include("~/Content/exceptionless.less")
                .Include("~/Content/exceptionless-responsive.css");
            mainCSSBundle.Builder = new NullBuilder();
            mainCSSBundle.Transforms.Add(new CssTransformer());
            mainCSSBundle.Orderer = new NullOrderer();
            bundles.Add(mainCSSBundle);

            Bundle customCSSBundle = new Bundle("~/content/custom.css")
                .Include("~/Content/exceptionless.less")
                .Include("~/Content/exceptionless-responsive.css");
            customCSSBundle.Builder = new NullBuilder();
            customCSSBundle.Transforms.Add(new CssTransformer());
            customCSSBundle.Orderer = new NullOrderer();
            bundles.Add(customCSSBundle);

            Bundle mainJsBundle = new Bundle("~/scripts/main.js")
                .Include("~/Scripts/jquery-{version}.js")
                .Include("~/Scripts/jquery.validate.js")
                .Include("~/Scripts/jquery.unobtrusive-ajax.js")
                .Include("~/Scripts/jquery.validate.unobtrusive.js")
                .Include("~/Scripts/jquery.validate.unobtrusive-custom-for-bootstrap.js")
                .Include("~/Scripts/mvcfoolproof.unobtrusive.js")
                .Include("~/Scripts/bootstrap.js")
                .Include("~/Scripts/jquery.fancybox.pack.js");

            mainJsBundle.Builder = new NullBuilder();
            mainJsBundle.Transforms.Add(new JsTransformer());
            mainJsBundle.Orderer = new NullOrderer();
            bundles.Add(mainJsBundle);

            Bundle appCSSBundle = new Bundle("~/content/app.css")
                .Include("~/Content/bootstrap.css")
                .Include("~/Content/bootstrap-responsive.css")
                .Include("~/Content/bootstrap-datepicker.css")
                .Include("~/Content/bootstrap-select.css")
                .Include("~/Content/css/font-awesome.css")
                .Include("~/Content/rickshaw.css")
                .Include("~/Content/toastr.less")
                .Include("~/Content/exceptionless.less")
                .Include("~/Content/exceptionless-responsive.css");
            appCSSBundle.Builder = new NullBuilder();
            appCSSBundle.Transforms.Add(new CssTransformer());
            appCSSBundle.Orderer = new NullOrderer();
            bundles.Add(appCSSBundle);

            Bundle appJsBundle = new Bundle("~/scripts/app.js")
                .Include("~/Scripts/local.storage.js")
                .Include("~/Scripts/history.iegte8.js")
                .Include("~/Scripts/jquery-{version}.js")
                .Include("~/Scripts/jquery.validate.js")
                .Include("~/Scripts/jquery.unobtrusive-ajax.js")
                .Include("~/Scripts/jquery.validate.unobtrusive.js")
                .Include("~/Scripts/jquery.validate.unobtrusive-custom-for-bootstrap.js")
                .Include("~/Scripts/mvcfoolproof.unobtrusive.js")
                .Include("~/Scripts/jquery.payment.js")
                .Include("~/Scripts/jquery.scrollTo.js")
                .Include("~/Scripts/jquery.signalR-{version}.js")
                .Include("~/Scripts/moment.js")
                .Include("~/Scripts/twix.js")
                .Include("~/Scripts/livestamp.js")
                .Include("~/Scripts/bootstrap.js")
                .Include("~/Scripts/bootbox.js")
                .Include("~/Scripts/bootstrap-datepicker.js")
                .Include("~/Scripts/bootstrap-select.js")
                .Include("~/Scripts/handlebars.js")
                .Include("~/Scripts/knockout-{version}.js")
                .Include("~/Scripts/knockout-es5.js")
                .Include("~/Scripts/knockout.activity.js")
                .Include("~/Scripts/knockout.command.js")
                .Include("~/Scripts/knockout.dirtyFlag.js")
                .Include("~/Scripts/knockout.mapping-latest.js")
                .Include("~/Scripts/knockout.validation.js")
                .Include("~/Scripts/jstz.js")
                .Include("~/Scripts/d3.v3.js")
                .Include("~/Scripts/rickshaw.js")
                .Include("~/Scripts/rickshaw.custom.js")
                .Include("~/Scripts/spin.js")
                .Include("~/Scripts/numeral.min.js")
                .Include("~/Scripts/toastr.js")
                .Include("~/Scripts/trunk8.js")
                .Include("~/Scripts/underscore.js")
                .Include("~/Scripts/ZeroClipboard.js")
                .Include("~/Scripts/App/exceptionless.js");

            appJsBundle.Builder = new NullBuilder();
            appJsBundle.Transforms.Add(new JsTransformer());
            appJsBundle.Orderer = new NullOrderer();
            bundles.Add(appJsBundle);
        }

        private static void AddDefaultIgnorePatterns(IgnoreList ignoreList) {
            if (ignoreList == null)
                throw new ArgumentNullException("ignoreList");

            ignoreList.Ignore("*.intellisense.js");
            ignoreList.Ignore("*-vsdoc.js");
            ignoreList.Ignore("*.debug.js", OptimizationMode.WhenEnabled);
            //ignoreList.Ignore("*.min.js", OptimizationMode.WhenDisabled);
            ignoreList.Ignore("*.min.css", OptimizationMode.WhenDisabled);
        }
    }
}