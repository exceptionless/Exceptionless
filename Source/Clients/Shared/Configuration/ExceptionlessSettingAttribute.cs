#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless.Configuration {
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class ExceptionlessSettingAttribute : Attribute {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExceptionlessSettingAttribute" /> class.
        /// </summary>
        /// <param name="name">The configuration item name.</param>
        /// <param name="value">The configuration value.</param>
        public ExceptionlessSettingAttribute(string name, string value) {
            Name = name;
            Value = value;
        }

        /// <summary>
        /// Gets or sets the name of the configuration item to set.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the value of the configuration item.
        /// </summary>
        /// <value>The value.</value>
        public string Value { get; set; }
    }
}