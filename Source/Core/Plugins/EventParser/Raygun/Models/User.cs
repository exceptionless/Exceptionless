using System;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Models {
    public class User {
        public string Identifier { get; set; }

        public bool IsAnonymous { get; set; }

        public string Email { get; set; }

        public string FullName { get; set; }

        public string FirstName { get; set; }

        public string Uuid { get; set; }
    }
}
