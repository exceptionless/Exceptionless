using System;
using System.Collections.Generic;
using System.Linq;

namespace Exceptionless.Core.Filter {
    public class FieldAggregationProcessor {
        private static readonly HashSet<string> _allowedFields = new HashSet<string> {
            "value", "stack_id", "user.raw", "is_first_occurrence"
        };
        
        private static readonly HashSet<string> _allowedTermFields = new HashSet<string> {
            "is_first_occurrence"
        };

        private static readonly HashSet<string> _freeFields = new HashSet<string> {
            "value", "stack_id", "is_first_occurrence"
        };

        public static FieldAggregationsResult Process(string query, bool applyRules = true) {
            if (String.IsNullOrEmpty(query))
                return new FieldAggregationsResult { IsValid = true };

            var result = new FieldAggregationsResult { IsValid = true };
            string[] aggregations = query.Split(new [] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string aggregation in aggregations) {
                string[] parts = aggregation.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || parts.Length > 3)
                    return new FieldAggregationsResult { Message = $"Invalid aggregation: {aggregation}"};

                string type = parts[0]?.ToLower().Trim();
                string field = parts[1]?.ToLower().Trim();
                if (String.IsNullOrEmpty(type) || String.IsNullOrEmpty(field))
                    return new FieldAggregationsResult { Message = $"Invalid type: {type} or field: {field}" };
                
                if (field.StartsWith("data."))
                    field = $"idx.{field.Substring(5)}-n";
                else if (field.StartsWith("ref."))
                    field = $"idx.{field.Substring(4)}-r";

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
                    case "term":
                        var term = new TermFieldAggregation { Field = field };
                        if (parts.Length > 2 && !String.IsNullOrWhiteSpace(parts[2])) {
                            if (parts[2].StartsWith("-"))
                                term.ExcludePattern = parts[2].Substring(1).Trim();
                            else
                                term.IncludePattern = parts[2].Trim();
                        }

                        result.Aggregations.Add(term);
                        break;
                    default:
                        return new FieldAggregationsResult { Message = $"Invalid type: {type} for aggregation: {aggregation}" };
                }
            }
            
            if (result.Aggregations.Any(a => !_freeFields.Contains(a.Field)))
                result.UsesPremiumFeatures = true;
            
            if (applyRules) {
                if (result.Aggregations.Count > 10)
                    return new FieldAggregationsResult { Message = "Aggregation count exceeded" };

                // Duplicate aggregations
                if (result.Aggregations.Count != aggregations.Length)
                    return new FieldAggregationsResult { Message = "Duplicate aggregation detected" };

                // Distinct queries are expensive.
                if (result.Aggregations.Count(a => a.Type == FieldAggregationType.Distinct) > 1)
                    return new FieldAggregationsResult { Message = "Distinct aggregation count exceeded" };
                
                // Term queries are expensive.
                var terms = result.Aggregations.Where(a => a.Type == FieldAggregationType.Term).ToList();
                if (terms.Count > 1 || terms.Any(a => !_allowedTermFields.Contains(a.Field)))
                    return new FieldAggregationsResult { Message = "Terms aggregation count exceeded" };

                // Only allow fields that are numeric or have high commonality.
                if (result.Aggregations.Any(a => !_allowedFields.Contains(a.Field)))
                    return new FieldAggregationsResult { Message = "Dissallowed field detected" };
            }
            
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
        Last,
        Term
    }

    public class FieldAggregation {
        public FieldAggregationType Type { get; set; }
        public string Field { get; set; }

        public string Key {
            get {
                switch (Type) {
                    case FieldAggregationType.Average:
                        return "avg_" + Field;
                    case FieldAggregationType.Distinct:
                        return "distinct_" + Field;
                    case FieldAggregationType.Sum:
                        return "sum_" + Field;
                    case FieldAggregationType.Min:
                        return "min_" + Field;
                    case FieldAggregationType.Max:
                        return "max_" + Field;
                    case FieldAggregationType.Last:
                        return "last_" + Field;
                    case FieldAggregationType.Term:
                        return "term_" + Field;
                }

                return Field;
            }
        }

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

    public class TermFieldAggregation: FieldAggregation {
        public TermFieldAggregation() {
            Type = FieldAggregationType.Term;
        }

        public string ExcludePattern { get; set; }
        public string IncludePattern { get; set; }

        protected bool Equals(TermFieldAggregation other) {
            return base.Equals(other) && String.Equals(ExcludePattern, other.ExcludePattern) && String.Equals(IncludePattern, other.IncludePattern);
        }
        
        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((TermFieldAggregation)obj);
        }
        public override int GetHashCode() {
            unchecked {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (ExcludePattern?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (IncludePattern?.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        public static bool operator ==(TermFieldAggregation left, TermFieldAggregation right) {
            return Equals(left, right);
        }

        public static bool operator !=(TermFieldAggregation left, TermFieldAggregation right) {
            return !Equals(left, right);
        }
    }
}