#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Web;

namespace Exceptionless.SampleWcf {
    [ExceptionlessWcfHandleError]
    public class Service1 : IService1 {
        public string GetData(int value) {
            throw new Exception(Guid.NewGuid().ToString());
        }

        public CompositeType GetDataUsingDataContract(CompositeType composite) {
            if (composite == null)
                throw new ArgumentNullException("composite");

            if (composite.BoolValue)
                composite.StringValue += "Suffix";

            return composite;
        }
    }
}