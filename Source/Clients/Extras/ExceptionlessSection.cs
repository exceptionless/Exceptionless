#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System.Configuration;
using System;

namespace Exceptionless {
    internal class ExceptionlessSection : ConfigurationSection {
        [ConfigurationProperty("enabled", DefaultValue = true)]
        public bool Enabled { get { return (bool)base["enabled"]; } set { base["enabled"] = value; } }

        [ConfigurationProperty("apiKey", IsRequired = true)]
        public string ApiKey { get { return base["apiKey"] as string; } set { base["apiKey"] = value; } }
        
        [ConfigurationProperty("serverUrl")]
        public string ServerUrl { get { return base["serverUrl"] as string; } set { base["serverUrl"] = value; } }

        [ConfigurationProperty("enableSSL", DefaultValue = true)]
        public bool? EnableSSL { get { return (bool?)base["enableSSL"]; } set { base["enableSSL"] = value; } }

        [ConfigurationProperty("enableLogging", DefaultValue = null)]
        public bool? EnableLogging { get { return (bool?)base["enableLogging"]; } set { base["enableLogging"] = value; } }

        [ConfigurationProperty("logPath")]
        public string LogPath { get { return base["logPath"] as string; } set { base["logPath"] = value; } }

        [ConfigurationProperty("storagePath")]
        public string StoragePath { get { return base["storagePath"] as string; } set { base["storagePath"] = value; } }

        [ConfigurationProperty("tags")]
        public string Tags { get { return this["tags"] as string; } set { this["tags"] = value; } }

        [ConfigurationProperty("settings", IsDefaultCollection = false)]
        public NameValueConfigurationCollection Settings { get { return this["settings"] as NameValueConfigurationCollection; } set { this["settings"] = value; } }

        [ConfigurationProperty("data", IsDefaultCollection = false)]
        public NameValueConfigurationCollection ExtendedData { get { return this["data"] as NameValueConfigurationCollection; } set { this["data"] = value; } }

        [ConfigurationProperty("registrations", IsDefaultCollection = false, IsRequired = false)]
        [ConfigurationCollection(typeof(RegistrationCollection), AddItemName = "registration")]
        public RegistrationCollection Registrations { get { return this["registrations"] as RegistrationCollection; } set { this["registrations"] = value; } }
    }

    public class RegistrationCollection : ConfigurationElementCollection {
        protected override ConfigurationElement CreateNewElement() {
            return new RegistrationConfigElement();
        }

        protected override object GetElementKey(ConfigurationElement element) {
            return ((RegistrationConfigElement)element).Service;
        }
    }

    public class RegistrationConfigElement : ConfigurationElement {
        [ConfigurationProperty("service", IsRequired = true)]
        public string Service {
            get { return (string)this["service"]; }
            set { this["service"] = value; }
        }

        [ConfigurationProperty("type", IsRequired = true)]
        public string Type {
            get { return (string)this["type"]; }
            set { this["type"] = value; }
        }
    }
}