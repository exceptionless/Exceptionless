#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

#if !PORTABLE40
using System.Configuration;
using System;

namespace Exceptionless.Configuration {
    internal class ExceptionlessSection : ConfigurationSection {
        [ConfigurationProperty("serverUrl")]
        public string ServerUrl { get { return base["serverUrl"] as string; } set { base["serverUrl"] = value; } }

        [ConfigurationProperty("apiKey", IsRequired = true)]
        public string ApiKey { get { return base["apiKey"] as string; } set { base["apiKey"] = value; } }

        [ConfigurationProperty("enabled", DefaultValue = true)]
        public bool Enabled { get { return (bool)base["enabled"]; } set { base["enabled"] = value; } }

        [ConfigurationProperty("enableSSL", DefaultValue = true)]
        public bool? EnableSSL { get { return (bool?)base["enableSSL"]; } set { base["enableSSL"] = value; } }

        [ConfigurationProperty("enableLogging", DefaultValue = null)]
        public bool? EnableLogging { get { return (bool?)base["enableLogging"]; } set { base["enableLogging"] = value; } }

        [ConfigurationProperty("logPath")]
        public string LogPath { get { return base["logPath"] as string; } set { base["logPath"] = value; } }

        [ConfigurationProperty("queuePath")]
        public string QueuePath { get { return base["queuePath"] as string; } set { base["queuePath"] = value; } }

        [ConfigurationProperty("tags")]
        public string Tags { get { return this["tags"] as string; } set { this["tags"] = value; } }

        [ConfigurationProperty("settings", IsDefaultCollection = false)]
        public NameValueConfigurationCollection Settings { get { return this["settings"] as NameValueConfigurationCollection; } set { this["settings"] = value; } }

        [ConfigurationProperty("extendedData", IsDefaultCollection = false)]
        public NameValueConfigurationCollection ExtendedData { get { return this["extendedData"] as NameValueConfigurationCollection; } set { this["extendedData"] = value; } }
    }
}

#endif