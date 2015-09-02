using System;

namespace Exceptionless.Core.Models.Data {
    public class UserDescription : IData {
        public UserDescription() {
            Data = new DataDictionary();
        }

        public UserDescription(string emailAddress, string description) : this() {
            if (!String.IsNullOrWhiteSpace(emailAddress))
                EmailAddress = emailAddress.Trim();

            if (!String.IsNullOrWhiteSpace(description))
                Description = description.Trim();
        }

        public string EmailAddress { get; set; }
        public string Description { get; set; }

        /// <summary>
        /// Extended data entries for this user description.
        /// </summary>
        public DataDictionary Data { get; set; }
    }
}