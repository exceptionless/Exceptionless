#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Globalization;
using System.Windows.Controls;

namespace Exceptionless.Validation {
    public class RequiredValidationRule : ValidationRule {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
            if (value == null)
                return new ValidationResult(false, "Value can not be null.");

            if (value is string && String.IsNullOrEmpty(value as string))
                return new ValidationResult(false, "Value can not be empty.");

            return new ValidationResult(true, null);
        }
    }
}