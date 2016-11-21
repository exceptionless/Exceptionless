using System;
using System.Collections.Generic;
using System.Linq;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Processors {
    public class FieldAggregationProcessor {
        private static readonly HashSet<string> _allowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "value", "stack_id", "user.keyword", "is_first_occurrence"
        };
        
        private static readonly HashSet<string> _allowedTermFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "is_first_occurrence"
        };

        private static readonly HashSet<string> _allowedTermExcludesIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "f", "t"
        };

        private static readonly HashSet<string> _freeFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
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
                
                // TODO: This doesn't handle dates.
                if (field.StartsWith("data."))
                    field = $"idx.{field.Substring(5).ToLower()}-n";
                else if (field.StartsWith("ref."))
                    field = $"idx.{field.Substring(4).ToLower()}-r";

                var fieldType = GetFieldAggregationTypet(type);
                if (fieldType == null)
                    return new FieldAggregationsResult { Message = $"Invalid type: {type}" };
                
                string defaultValueOrIncludeExclude = parts.Length > 2 && !String.IsNullOrWhiteSpace(parts[2]) ? parts[2]?.Trim() : null;
                if (fieldType == FieldAggregationType.Term) {
                    var term = new TermFieldAggregation { Field = field };
                    if (defaultValueOrIncludeExclude != null) {
                        if (defaultValueOrIncludeExclude.StartsWith("-"))
                            term.ExcludePattern = defaultValueOrIncludeExclude.Substring(1).Trim();
                        else
                            term.IncludePattern = defaultValueOrIncludeExclude;
                    }

                    result.Aggregations.Add(term);
                } else {
                    result.Aggregations.Add(new FieldAggregation {
                        Type = fieldType.Value,
                        Field = field,
                        DefaultValue = ParseDefaultValue(defaultValueOrIncludeExclude)
                    });
                }
            }
            
            if (result.Aggregations.Any(a => !_freeFields.Contains(a.Field)))
                result.UsesPremiumFeatures = true;
            
            return applyRules ? ApplyRules(result, aggregations) : result;
        }

        private static FieldAggregationsResult ApplyRules(FieldAggregationsResult result, string[] aggregations) {
            if (result.Aggregations.Count > 10)
                return new FieldAggregationsResult { Message = "Aggregation count exceeded" };

            // Duplicate aggregations
            if (result.Aggregations.Count != aggregations.Length)
                return new FieldAggregationsResult { Message = "Duplicate aggregation detected" };

            // Distinct queries are expensive.
            if (result.Aggregations.Count(a => a.Type == FieldAggregationType.Distinct) > 1)
                return new FieldAggregationsResult { Message = "Distinct aggregation count exceeded" };

            // Term queries are expensive.
            var terms = result.Aggregations.Where(a => a.Type == FieldAggregationType.Term).OfType<TermFieldAggregation>().ToList();
            if (terms.Count > 1
                || terms.Any(a => !_allowedTermFields.Contains(a.Field))
                || terms.Any(a => a.ExcludePattern != null && !_allowedTermExcludesIncludes.Contains(a.ExcludePattern))
                || terms.Any(a => a.IncludePattern != null && !_allowedTermExcludesIncludes.Contains(a.IncludePattern)))
                return new FieldAggregationsResult { Message = "Terms aggregation count exceeded" };

            // Only allow fields that are numeric or have high commonality.
            if (result.Aggregations.Any(a => !_allowedFields.Contains(a.Field)))
                return new FieldAggregationsResult { Message = "Dissallowed field detected" };

            return result;
        }

        private static FieldAggregationType? GetFieldAggregationTypet(string type) {
            switch (type) {
                case "avg":
                    return FieldAggregationType.Average;
                case "distinct":
                    return FieldAggregationType.Distinct;
                case "sum":
                    return FieldAggregationType.Sum;
                case "min":
                    return FieldAggregationType.Min;
                case "max":
                    return FieldAggregationType.Max;
                case "last":
                    return FieldAggregationType.Last;
                case "term":
                    return FieldAggregationType.Term;
            }

            return null;
        }

        private static int? ParseDefaultValue(string defaultValue) {
            int value;
            if (Int32.TryParse(defaultValue, out value))
                return value;

            return null;
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
        public string SortOrder { get; set; }

        public string Key {
            get {
                string field;
                if (Type == FieldAggregationType.Average)
                    field = "avg_" + Field;
                else if (Type == FieldAggregationType.Distinct)
                    field = "distinct_" + Field;
                else if (Type == FieldAggregationType.Sum)
                    field = "sum_" + Field;
                else if (Type == FieldAggregationType.Min)
                    field = "min_" + Field;
                else if (Type == FieldAggregationType.Max)
                    field = "max_" + Field;
                else if (Type == FieldAggregationType.Last)
                    field = "last_" + Field;
                else if (Type == FieldAggregationType.Term)
                    field = "term_" + Field;
                else
                    field = Field;

                return field.Replace('.', '_');
            }
        }

        public int? DefaultValue { get; set; }

        public string DefaultValueScript => DefaultValue.HasValue ? $"doc['{Field}'].empty ? {DefaultValue.Value} : doc['{Field}'].value" : null;

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
            return ((int)Type * 397) ^ (Field?.GetHashCode() ?? 0) ^ (DefaultValue?.GetHashCode() ?? 0) ^ (SortOrder?.GetHashCode() ?? 0);
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
                hashCode = (hashCode * 397) ^ (ExcludePattern?.GetHashCode() ?? 0) ^ (IncludePattern?.GetHashCode() ?? 0);
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