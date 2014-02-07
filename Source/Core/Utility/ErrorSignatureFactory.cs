#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Models;

namespace Exceptionless.Core.Utility {
    public class ErrorSignatureFactory {
        private IProjectRepository _projectRepository;

        public ErrorSignatureFactory(IProjectRepository projectRepository = null) {
            _projectRepository = projectRepository;
        }

        public ErrorSignature GetSignature(Error error) {
            // TODO: Need to get our project settings for user namespaces and common methods.
            return new ErrorSignature(error, userCommonMethods: new[] { "DataContext.SubmitChanges", "Entities.SaveChanges" });
        }
    }
}