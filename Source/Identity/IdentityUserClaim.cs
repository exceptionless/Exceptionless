using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoDB.AspNet.Identity
{


    /// <summary>
    /// Class IdentityUserClaim.
    /// </summary>
    public class IdentityUserClaim : IdentityUserClaim<string>
    {
      
    }

    /// <summary>
    /// Class IdentityUserClaim.
    /// </summary>
    public class IdentityUserClaim<TKey>
    {
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        /// <value>The identifier.</value>
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public virtual string Id { get; set; }
        /// <summary>
        /// Gets or sets the user identifier.
        /// </summary>
        /// <value>The user identifier.</value>
        public virtual TKey UserId { get; set; }
        /// <summary>
        /// Gets or sets the type of the claim.
        /// </summary>
        /// <value>The type of the claim.</value>
        public virtual string ClaimType { get; set; }
        /// <summary>
        /// Gets or sets the claim value.
        /// </summary>
        /// <value>The claim value.</value>
        public virtual string ClaimValue { get; set; }


    }
}