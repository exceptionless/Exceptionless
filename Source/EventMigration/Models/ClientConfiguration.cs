#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;

namespace Exceptionless.EventMigration.Models {
    public class ClientConfiguration {
        public ClientConfiguration() {
            Settings = new ConfigurationDictionary();
        }

        public int Version { get; set; } // TODO: Make this private once we have better patching support.
        public ConfigurationDictionary Settings { get; set; }
    }
}