using System;
using System.Collections.Generic;
using System.Linq;

namespace Exceptionless.Core.Filter {
    public class FieldAggregationProcessor {
        private static readonly HashSet<string> _allowedNumericFields = new HashSet<string> {
            "value"
        };

        public static FieldAggregationsResult Process(string query) {
            if (String.IsNullOrEmpty(query))
                return new FieldAggregationsResult();

            var result = new FieldAggregationsResult { IsValid = true };
            string[] aggregations = query.Split(new [] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string aggregation in aggregations) {
                string[] parts = aggregation.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                    return new FieldAggregationsResult { Message = $"Invalid aggregation: {aggregation}"};

                string type = parts[0]?.ToLower().Trim();
                string field = parts[1]?.ToLower().Trim();
                if (String.IsNullOrEmpty(type) || String.IsNullOrEmpty(field))
                    return new FieldAggregationsResult { Message = $"Invalid type: {type} or field: {field}" };

                // expand field here..
                //var processResult = QueryProcessor.Process(type);
                //if (!processResult.IsValid)
                //    return new FieldAggregationsResult { Message = $"Invalid field: {field}" };

                //field = processResult.ExpandedQuery;
                //result.UsesPremiumFeatures &= processResult.UsesPremiumFeatures;
                
                switch (type) {
                    case "avg":
                        result.Aggregations.Add(new FieldAggregation { Type = FieldAggregationType.Average, Field = field });
                        break;
                    case "distinct":
                        result.Aggregations.Add(new FieldAggregation { Type = FieldAggregationType.Distinct, Field = field });
                        break;
                    case "sum":
                        result.Aggregations.Add(new FieldAggregation { Type = FieldAggregationType.Sum, Field = field });
                        break;
                    case "min":
                        result.Aggregations.Add(new FieldAggregation { Type = FieldAggregationType.Min, Field = field });
                        break;
                    case "max":
                        result.Aggregations.Add(new FieldAggregation { Type = FieldAggregationType.Max, Field = field });
                        break;
                    case "last":
                        result.Aggregations.Add(new FieldAggregation { Type = FieldAggregationType.Last, Field = field });
                        break;
                    default:
                        return new FieldAggregationsResult { Message = $"Invalid type: {type} for aggregation: {aggregation}" };
                }
            }

            // Rules
            if (result.Aggregations.Count > 10)
                return new FieldAggregationsResult { Message = "Aggregation count exceeded" };

            // Duplicate aggregations
            if (result.Aggregations.Count != aggregations.Length)
                return new FieldAggregationsResult { Message = "Duplicate aggregation detected." };

            // Distinct queries are expensive.
            if (result.Aggregations.Count(a => a.Type == FieldAggregationType.Distinct) > 1)
                return new FieldAggregationsResult { Message = "Distinct aggregation count exceeded" };

            // Only allow fields that are numeric or have high commonality.
            if (result.Aggregations.Any(a => !_allowedNumericFields.Contains(a.Field)))
                return new FieldAggregationsResult { Message = "Dissallowed field detected." };
            
            return result;
        }
    }
    
    public class FieldAggregationsResult {
        public FieldAggregationsResult() {
            Aggregations = new HashSet<FieldAggregation>();
        }

        public bool UsesPremiumFeatures { get; set; }
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public HashSet<FieldAggregation> Aggregations { get; set; }
    }
    
    public enum FieldAggregationType {
        Average,
        Distinct,
        Sum,
        Min,
        Max,
        Last
    }

    public class FieldAggregation {
        public FieldAggregationType Type { get; set; }
        public string Field { get; set; }

        protected bool Equals(FieldAggregation other) {
            return Type == other.Type && String.Equals(Field, other.Field);
        }
        
        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((FieldAggregation)obj);
        }
        
        public override int GetHashCode() {
            return ((int)Type * 397) ^ (Field?.GetHashCode() ?? 0);
        }

        public static bool operator ==(FieldAggregation left, FieldAggregation right) {
            return Equals(left, right);
        }

        public static bool operator !=(FieldAggregation left, FieldAggregation right) {
            return !Equals(left, right);
        }
    }
}