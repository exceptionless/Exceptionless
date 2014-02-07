#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Web;
using System.Web.Mvc;

namespace Exceptionless.Web {
    public static class HtmlHelperExtentions {
        public static bool IsDebug(this HtmlHelper htmlHelper) {
#if DEBUG
            return true;
#else
            return false;
#endif
        }

        public static bool IsDebug<TModel>(this HtmlHelper<TModel> htmlHelper) {
#if DEBUG
            return true;
#else
            return false;
#endif
        }

        //public static IHtmlString ToJSON<TModel>(this HtmlHelper<TModel> htmlHelper, TModel value) {
        //    return htmlHelper.Raw(JsonConvert.SerializeObject(value)); // This serializes dates correctly as @Html.Raw(Json.Encode(Model)) fails todo so..
        //}

        public static bool IsActiveMenuItem(this HtmlHelper htmlHelper, string actionName, string controllerName) {
            string action = htmlHelper.ViewContext.RouteData.Values["action"].ToString();
            string controller = htmlHelper.ViewContext.RouteData.Values["controller"].ToString();

            if (String.Equals(actionName, action, StringComparison.OrdinalIgnoreCase) && String.Equals(controllerName, controller, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        public static IEnumerable<SelectListItem> GetEnumSelectList<T>(this HtmlHelper helper, T selectedValue = default(T)) where T : struct {
            return from name in Enum.GetNames(typeof(T))
                   let enumValue = Convert.ToString((T)Enum.Parse(typeof(T), name, true))
                   select new SelectListItem {
                       Text = GetEnumDescription(name, typeof(T)),
                       Value = enumValue,
                       Selected = name == selectedValue.ToString()
                   };
        }

        private static string GetEnumDescription(string value, Type enumType) {
            FieldInfo field = enumType.GetField(value.ToString());
            DisplayAttribute attribute = field.GetCustomAttributes(typeof(DisplayAttribute), false).OfType<DisplayAttribute>().FirstOrDefault();
            return attribute != null ? attribute.Name : value;
        }

        public static IHtmlString BeginControlGroupFor<T>(this HtmlHelper<T> html, Expression<Func<T, object>> modelProperty) {
            var controlGroupWrapper = new TagBuilder("div");
            controlGroupWrapper.AddCssClass("control-group");
            string partialFieldName = ExpressionHelper.GetExpressionText(modelProperty);
            string fullHtmlFieldName = html.ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(partialFieldName);
            if (!html.ViewData.ModelState.IsValidField(fullHtmlFieldName))
                controlGroupWrapper.AddCssClass("error");
            string openingTag = controlGroupWrapper.ToString(TagRenderMode.StartTag);
            return MvcHtmlString.Create(openingTag);
        }

        public static IHtmlString BeginControlGroupFor<T>(this HtmlHelper<T> html, string propertyName) {
            var controlGroupWrapper = new TagBuilder("div");
            controlGroupWrapper.AddCssClass("control-group");
            string partialFieldName = propertyName;
            string fullHtmlFieldName = html.ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(partialFieldName);
            if (!html.ViewData.ModelState.IsValidField(fullHtmlFieldName))
                controlGroupWrapper.AddCssClass("error");
            string openingTag = controlGroupWrapper.ToString(TagRenderMode.StartTag);
            return MvcHtmlString.Create(openingTag);
        }

        public static IHtmlString EndControlGroup(this HtmlHelper html) {
            return MvcHtmlString.Create("</div>");
        }

        public static MvcHtmlString BeginLabelFor<TModel, TValue>(this HtmlHelper<TModel> html, Expression<Func<TModel, TValue>> expression, object htmlAttributes = null) {
            ModelMetadata meta = ModelMetadata.FromLambdaExpression(expression, html.ViewData);
            string resolvedLabelText = meta.DisplayName ?? meta.PropertyName;
            if (String.IsNullOrEmpty(resolvedLabelText))
                return MvcHtmlString.Empty;

            string htmlFieldName = ExpressionHelper.GetExpressionText(expression);
            var tag = new TagBuilder("label");
            tag.Attributes.Add("for", TagBuilder.CreateSanitizedId(html.ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(htmlFieldName)));
            if (htmlAttributes != null && !(htmlAttributes is IDictionary<string, object>))
                htmlAttributes = HtmlHelper.AnonymousObjectToHtmlAttributes(htmlAttributes);
            if (htmlAttributes != null)
                tag.MergeAttributes((IDictionary<string, object>)htmlAttributes, replaceExisting: true);

            return MvcHtmlString.Create(tag.ToString(TagRenderMode.StartTag));
        }

        public static IHtmlString EndLabel(this HtmlHelper html) {
            return MvcHtmlString.Create("</label>");
        }
    }
}