using System;

namespace Exceptionless.Core.Models {
    public class SessionInfo {
        /// <summary>
        /// The application version during the time of the session.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// A unique identifier for the user that the event happened to (ie. email address, user name or database id).
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// The IP address of the user that the event happened to.
        /// </summary>
        public string IpAddress { get; set; }

        /// <summary>
        /// A unique identifier for the machine that the event happened on (ie. machine name or ip address).
        /// </summary>
        public string MachineId { get; set; }

        /// <summary>
        /// A unique identifier for this installation of the Exceptionless client.
        /// </summary>
        public string InstallId { get; set; }
    }
}
