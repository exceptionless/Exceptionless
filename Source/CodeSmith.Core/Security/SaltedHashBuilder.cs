using CodeSmith.Core.Extensions;

namespace CodeSmith.Core.Security
{
    public class SaltedHashBuilder : HashBuilder
    {
        public SaltedHashBuilder(string salt)
        {
            if (salt.IsNullOrEmpty())
                return;

            Writer.Write(salt);
        }
    }
}
