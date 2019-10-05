using System.Text.Json;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Core.Serialization {
    public class SnakeCaseJsonNamingPolicy : JsonNamingPolicy {
        public override string ConvertName(string name) {
            return name.ToLowerUnderscoredWords();
        }
    }
}
