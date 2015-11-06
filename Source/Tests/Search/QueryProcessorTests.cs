using System;
using Exceptionless.Core.Filter;
using Xunit;

namespace Exceptionless.Api.Tests.Search {
    public class QueryProcessorTests {
        [Theory]
        [InlineData("string\"", null, true, true)]
        [InlineData("\"quoted string\"", null, true, true)]
        [InlineData("criteria", null, true, true)]
        [InlineData("(criteria)", null, true, true)]
        [InlineData("field:criteria", null, true, true)]
        [InlineData("-criteria", null, true, true)]
        [InlineData("criteria1 criteria2", null, true, true)]
        [InlineData("criteria1 +criteria2", null, true, true)]
        [InlineData("criteria1 OR criteria2", null, true, true)]
        [InlineData("criteria1 OR criteria2 OR criteria3", null, true, true)]
        [InlineData("criteria1 OR (criteria2 AND criteria3)", null, true, true)]
        [InlineData("field:[1 TO 2]", null, true, true)]
        [InlineData("field:{1 TO 2}", null, true, true)]
        [InlineData("field:[1 TO 2}", null, true, true)]
        [InlineData("field:(criteria1 criteria2)", null, true, true)]
        [InlineData("data.field:(now criteria2)", "idx.field-s:(now criteria2)", true, true)]
        [InlineData("field:(criteria1 OR criteria2)", null, true, true)]
        [InlineData("date:>now", null, true, true)]
        [InlineData("date:>now", null, true, true)]
        [InlineData("date:>now", null, true, true)]
        [InlineData("_exists_:title", null, true, true)]
        [InlineData("book.\\*:(quick brown)", null, true, true)]
        [InlineData("date:[now/d-4d TO now/d+1d}", null, true, true)]
        [InlineData("(date:[now/d-4d TO now/d+1d})", null, true, true)]
        [InlineData("data.date:>now", "idx.date-d:>now", true, true)]
        [InlineData("data.date:[now/d-4d TO now/d+1d}", "idx.date-d:[now/d-4d TO now/d+1d}", true, true)]
        [InlineData("data.date:[2012-01-01 TO 2012-12-31]", "idx.date-d:[2012-01-01 TO 2012-12-31]", true, true)]
        [InlineData("data.date:[* TO 2012-12-31]", "idx.date-d:[* TO 2012-12-31]", true, true)]
        [InlineData("data.date:[2012-01-01 TO *]", "idx.date-d:[2012-01-01 TO *]", true, true)]
        [InlineData("(data.date:[now/d-4d TO now/d+1d})", "(idx.date-d:[now/d-4d TO now/d+1d})", true, true)]
        [InlineData("criter~", null, true, true)]
        [InlineData("criter~1", null, true, true)]
        [InlineData("criter^2", null, true, true)]
        [InlineData("\"blah criter\"~1", null, true, true)]
        [InlineData("count:[1..5}", null, true, true)]
        [InlineData("data.count:[1..5}", "idx.count-n:[1..5}", true, true)]
        [InlineData("age:(>=10 AND < 20)", null, true, true)]
        [InlineData("data.Windows-identity:ejsmith", "idx.windows-identity-s:ejsmith", true, true)]
        [InlineData("data.age:(>30 AND <=40)", "idx.age-n:(>30 AND <=40)", true, true)]
        [InlineData("data.age:(+>=10 AND < 20)", "idx.age-n:(+>=10 AND < 20)", true, true)]
        [InlineData("data.age:(+>=10 +<20)", "idx.age-n:(+>=10 +<20)", true, true)]
        [InlineData("data.age:(->=10 AND < 20)", "idx.age-n:(->=10 AND < 20)", true, true)]
        [InlineData("data.age:[10 TO *]", "idx.age-n:[10 TO *]", true, true)]
        [InlineData("data.age:[* TO 10]", "idx.age-n:[* TO 10]", true, true)]
        [InlineData("hidden:true AND data.age:(>30 AND <=40)", "hidden:true AND idx.age-n:(>30 AND <=40)", true, true)]
        [InlineData("hidden:true", null, true, false)]
        [InlineData("fixed:true", null, true, false)]
        [InlineData("type:404", null, true, false)]
        public void CanProcessQuery(string query, string expected, bool isValid, bool usesPremiumFeatures) {
            var result = QueryProcessor.Process(query);
            Assert.Equal(expected ?? query, result.ExpandedQuery);
            Assert.Equal(isValid, result.IsValid);
            Assert.Equal(usesPremiumFeatures, result.UsesPremiumFeatures);
        }
    }
}
