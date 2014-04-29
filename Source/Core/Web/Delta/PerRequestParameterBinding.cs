// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Net.Http.Formatting;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using System.Web.Http.Metadata;

namespace Exceptionless.Core.Web {
    /// <summary>
    /// A special HttpParameterBinding that uses a Per Request formatter instance with access to the Request.
    /// </summary>
    /// <remarks>
    /// This class is needed by the OData deserializers, since they actually need access to more than just the Request
    /// body; they also need to interrogate the RequestUri etc.
    /// </remarks>
    internal class PerRequestParameterBinding : HttpParameterBinding {
        private IEnumerable<MediaTypeFormatter> _formatters;

        public PerRequestParameterBinding(HttpParameterDescriptor descriptor,
            IEnumerable<MediaTypeFormatter> formatters)
            : base(descriptor) {
            if (formatters == null)
                throw new ArgumentNullException("formatters");

            _formatters = formatters;
        }

        public override Task ExecuteBindingAsync(ModelMetadataProvider metadataProvider, HttpActionContext actionContext, CancellationToken cancellationToken) {
            var perRequestFormatters = new List<MediaTypeFormatter>();

            foreach (MediaTypeFormatter formatter in _formatters) {
                MediaTypeFormatter perRequestFormatter = formatter.GetPerRequestFormatterInstance(Descriptor.ParameterType, actionContext.Request, actionContext.Request.Content.Headers.ContentType);
                perRequestFormatters.Add(perRequestFormatter);
            }

            HttpParameterBinding innerBinding = CreateInnerBinding(perRequestFormatters);
            Contract.Assert(innerBinding != null);

            return innerBinding.ExecuteBindingAsync(metadataProvider, actionContext, cancellationToken);
        }

        protected virtual HttpParameterBinding CreateInnerBinding(IEnumerable<MediaTypeFormatter> perRequestFormatters) {
            return Descriptor.BindWithFormatter(perRequestFormatters);
        }
    }
}