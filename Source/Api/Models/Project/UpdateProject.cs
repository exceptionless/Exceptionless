using System;

namespace Exceptionless.Api.Models {
    public class UpdateProject {
        public string Name { get; set; }
        public string CustomContent { get; set; }
        public bool DeleteBotDataEnabled { get; set; }
    }
}
