using System;
using System.Linq;
using Exceptionless.Core.Models;
using Nest;

namespace Exceptionless.Core.Repositories.Configuration {
    public class OrganizationIndex : ElasticSearchIndexBase<Organization> {
        public override string Name { get { return "organizations"; } }

        protected override PutMappingDescriptor<Organization> CreateMapping(PutMappingDescriptor<Organization> map){
            return map
                .Index(VersionedName)
                .Dynamic()
                .TimestampField(ts => ts.Enabled().Path(u => u.ModifiedUtc).IgnoreMissing(false))
                .Properties(p => p
                    .Object<Invite>(f => f.Name(o => o.Invites.First()).RootPath().Properties(ip => ip
                        .String(fu => fu.Name(i => i.Token).Index(FieldIndexOption.NotAnalyzed).IndexName(Fields.Organization.InviteToken))))
                );
        }

        public static class Fields {
            public class Organization {
                public const string InviteToken = "invite_token";
            }
        }
    }
}