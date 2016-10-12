using System;
using System.Linq;
using Exceptionless.Core.Processors;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Search {
    public class FieldAggregationProcessorTests : TestBase {
        public FieldAggregationProcessorTests(ITestOutputHelper output) : base(output) {}

        [Theory]
        [InlineData(null, true)]
        [InlineData("avg", false)]
        [InlineData("avg:", false)]
        [InlineData("avg:val", false)]
        [InlineData("avg:value", true)]
        [InlineData("    avg    :    value", true)]
        [InlineData("avg:value,distinct:value,sum:value,min:value,max:value,last:value", true)]
        [InlineData("avg:value,avg:value", false)]
        [InlineData("avg:value,avg:value2,avg:value3,avg:value4,avg:value5,avg:value6,avg:value7,avg:value8,avg:value9,,avg:value10", false)]
        [InlineData("distinct:value,distinct:value1", false)]
        public void CanProcessQuery(string query, bool isValid) {
            var result = FieldAggregationProcessor.Process(query);
            Assert.Equal(isValid, result.IsValid);
        }

        [Fact]
        public void CanPreserveOrdering() {
            var result = FieldAggregationProcessor.Process("distinct:value,avg:value,sum:value");
            Assert.True(result.IsValid);
            Assert.Equal(3, result.Aggregations.Count);

            var aggregations = result.Aggregations.ToArray();
            Assert.Equal(new FieldAggregation { Type = FieldAggregationType.Distinct, Field = "value" }, aggregations[0]);
            Assert.Equal(new FieldAggregation { Type = FieldAggregationType.Average, Field = "value" }, aggregations[1]);
            Assert.Equal(new FieldAggregation { Type = FieldAggregationType.Sum, Field = "value" }, aggregations[2]);
        }
    }
}