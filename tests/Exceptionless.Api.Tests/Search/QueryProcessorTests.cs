using System;
using System.Threading.Tasks;
using Exceptionless.Core.Processors;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Search {
    public class QueryProcessorTests : TestBase {
        public QueryProcessorTests(ITestOutputHelper output) : base(output) {}

        [Theory]
        [InlineData(null, null, true, false)]
        [InlineData("  ", null, true, false)]
        [InlineData("data.field:(now criteria2)", "idx.field-s:(now criteria2)", true, true)]
        [InlineData("data.date:>now", "idx.date-d:>now", true, true)]
        [InlineData("data.date:[now/d-4d TO now/d+1d}", "idx.date-d:[now/d-4d TO now/d+1d}", true, true)]
        [InlineData("data.date:[2012-01-01 TO 2012-12-31]", "idx.date-d:[2012-01-01 TO 2012-12-31]", true, true)]
        [InlineData("data.date:[* TO 2012-12-31]", "idx.date-d:[* TO 2012-12-31]", true, true)]
        [InlineData("data.date:[2012-01-01 TO *]", "idx.date-d:[2012-01-01 TO *]", true, true)]
        [InlineData("(data.date:[now/d-4d TO now/d+1d})", "(idx.date-d:[now/d-4d TO now/d+1d})", true, true)]
        [InlineData("data.count:[1..5}", "idx.count-n:[1..5}", true, true)]
        [InlineData("data.Windows-identity:ejsmith", "idx.windows-identity-s:ejsmith", true, true)]
        [InlineData("data.age:(>30 AND <=40)", "idx.age-n:(>30 AND <=40)", true, true)]
        [InlineData("data.age:(+>=10 AND < 20)", "idx.age-n:(+>=10 AND <20)", true, true)]
        [InlineData("data.age:(+>=10 +<20)", "idx.age-n:(+>=10 +<20)", true, true)]
        [InlineData("data.age:(->=10 AND < 20)", "idx.age-n:(->=10 AND <20)", true, true)]
        [InlineData("data.age:[10 TO *]", "idx.age-n:[10 TO *]", true, true)]
        [InlineData("data.age:[* TO 10]", "idx.age-n:[* TO 10]", true, true)]
        [InlineData("hidden:true AND data.age:(>30 AND <=40)", "hidden:true AND idx.age-n:(>30 AND <=40)", true, true)]
        [InlineData("hidden:true", "hidden:true", true, false)]
        [InlineData("fixed:true", "fixed:true", true, false)]
        [InlineData("type:404", "type:404", true, false)]
        [InlineData("ref.session:12345678", "idx.session-r:12345678", true, true)]
        public async Task CanProcessQueryAsync(string query, string expected, bool isValid, bool usesPremiumFeatures) {
            var result = await QueryProcessor.ProcessAsync(query);
            Assert.Equal(expected, result.ExpandedQuery);
            Assert.Equal(isValid, result.IsValid);
            Assert.Equal(usesPremiumFeatures, result.UsesPremiumFeatures);
        }
    }
}
