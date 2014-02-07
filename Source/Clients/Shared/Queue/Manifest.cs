#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using Exceptionless.Extensions;
using Exceptionless.Models;

namespace Exceptionless.Queue {
    internal class Manifest : IIdentity {
        public Manifest() {
            LogMessages = new List<string>();
        }

        public static Manifest FromError(Error error) {
            return new Manifest {
                Id = error.Id,
                CanDiscard = String.IsNullOrEmpty(error.UserEmail)
            };
        }

        public string Id { get; set; }

        public string LastError { get; set; }

        public bool CanDiscard { get; set; }

        public DateTime? LastAttempt { get; set; }

        public int Attempts { get; set; }

        public List<string> LogMessages { get; private set; }

        public bool IsSent { get; set; }

        public void LogError(Exception exception) {
            LastError = "Error trying to process manifest.";
            if (exception == null)
                return;

            LogMessages.Add(String.Concat(DateTime.UtcNow.ToShortDateString(), " - ", exception.ToString()));
            LastError = exception.GetAllMessages();
        }

        public bool ShouldRetry() {
            if (Attempts == 0 || !LastAttempt.HasValue)
                return true;

            TimeSpan diff = DateTime.UtcNow.Subtract(LastAttempt.Value);

            // factor: Attempts < 2 and diff >= 10 min
            if (Attempts <= 2 && diff.TotalMinutes >= 10)
                return true;

            // factor: Attempts < 4 and diff >= 30 min
            if (Attempts <= 4 && diff.TotalMinutes >= 30)
                return true;

            // factor: diff >= 12 hours
            if (diff.TotalHours >= 12)
                return true;

            return false;
        }

        public bool BreakProcessing { get; set; }

        public bool IsComplete() {
            return IsSent;
        }

        public bool ShouldDiscard() {
            if (!CanDiscard && Attempts < 10)
                return false;

            return Attempts >= 5;
        }
    }
}