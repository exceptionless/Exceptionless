using System;

namespace Exceptionless.Core.Models.Data {
    public class UserInfo : IData {
        public UserInfo() {
            Data = new DataDictionary();
        }

        public UserInfo(string identity) : this() {
            if (!String.IsNullOrWhiteSpace(identity))
                Identity = identity.Trim();
        }
        
        public UserInfo(string identity, string name) : this(identity) {
            if (!String.IsNullOrWhiteSpace(name))
                Name = name.Trim();
        }

        /// <summary>
        /// Uniquely identifies the user.
        /// </summary>
        public string Identity { get; set; }

        /// <summary>
        /// The Friendly name of the user.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Extended data entries for this user.
        /// </summary>
        public DataDictionary Data { get; set; }
    }
}