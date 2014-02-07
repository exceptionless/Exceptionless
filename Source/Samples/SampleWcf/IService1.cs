#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace Exceptionless.SampleWcf {
    [ServiceContract]
    public interface IService1 {
        [OperationContract]
        string GetData(int value);

        [OperationContract]
        CompositeType GetDataUsingDataContract(CompositeType composite);
    }

    [DataContract]
    public class CompositeType {
        private bool _boolValue = true;
        private string _stringValue = "Hello World";

        [DataMember]
        public bool BoolValue { get { return _boolValue; } set { _boolValue = value; } }

        [DataMember]
        public string StringValue { get { return _stringValue; } set { _stringValue = value; } }
    }
}