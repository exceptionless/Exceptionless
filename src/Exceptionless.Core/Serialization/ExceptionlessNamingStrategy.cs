using Exceptionless.Core.Extensions;
using Newtonsoft.Json.Serialization;

namespace Exceptionless.Core.Serialization
{
    public class ExceptionlessNamingStrategy : SnakeCaseNamingStrategy
    {
        protected override string ResolvePropertyName(string name)
        {
            return name.ToLowerUnderscoredWords();
        }
    }
}
