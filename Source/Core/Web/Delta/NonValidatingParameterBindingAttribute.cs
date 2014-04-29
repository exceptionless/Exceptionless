// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http.Formatting;
using System.Web.Http;
using System.Web.Http.Controllers;

namespace Exceptionless.Core.Web {
    /// <summary>
    /// An attribute to disable WebApi model validation for a particular type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    internal sealed class NonValidatingParameterBindingAttribute : ParameterBindingAttribute {
        public override HttpParameterBinding GetBinding(HttpParameterDescriptor parameter) {
            IEnumerable<MediaTypeFormatter> formatters = parameter.Configuration.Formatters;

            return new NonValidatingParameterBinding(parameter, formatters);
        }

        private sealed class NonValidatingParameterBinding : PerRequestParameterBinding {
            public NonValidatingParameterBinding(HttpParameterDescriptor descriptor,
                IEnumerable<MediaTypeFormatter> formatters)
                : base(descriptor, formatters) {}

            protected override HttpParameterBinding CreateInnerBinding(IEnumerable<MediaTypeFormatter> perRequestFormatters) {
                return Descriptor.BindWithFormatter(perRequestFormatters, bodyModelValidator: null);
            }
        }
    }
}