#region Copyright (c) CodeSmith Tools, LLC.  All rights reserved.

// ------------------------------------------------------------------------------
// 
//  Copyright (c) 2002-2014 CodeSmith Tools, LLC.  All rights reserved.
//  
//  The terms of use for this software are contained in the file
//  named sourcelicense.txt, which can be found in the root of this distribution.
//  By using this software in any fashion, you are agreeing to be bound by the
//  terms of this license.
// 
//  You must not remove this notice, or any other, from this software.
// 
// ------------------------------------------------------------------------------

#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace CodeSmith.Core.Helpers
{
    public struct SortExpression
    {
        public PropertyDescriptor PropertyDescriptor;
        public string PropertyName;
        public ListSortDirection SortDirection;
        private Hashtable _propertyValueCache;

        public SortExpression(string propertyName)
        {
            PropertyName = propertyName;
            SortDirection = ListSortDirection.Ascending;
            PropertyDescriptor = null;
            _propertyValueCache = new Hashtable();
        }

        public SortExpression(string propertyName, ListSortDirection sortDirection)
        {
            PropertyName = propertyName;
            SortDirection = sortDirection;
            PropertyDescriptor = null;
            _propertyValueCache = new Hashtable();
        }

        public object GetPropertyValue(object obj)
        {
            if (PropertyDescriptor == null) return null;
            if (_propertyValueCache == null) _propertyValueCache = new Hashtable(100);

            if (_propertyValueCache.Contains(obj))
                return _propertyValueCache[obj];

            // since the value for a specific object will be asked for multiple times
            // during a bubble sort, we will cache the value for each object.
            object value = PropertyDescriptor.GetValue(obj);
            _propertyValueCache.Add(obj, value);
            return value;
        }
    }

    public class PropertyComparer : IComparer, IEqualityComparer
    {
        private SortExpression[] _sortExpressions = new SortExpression[] { };

        public PropertyComparer(string orderByClause)
        {
            BuildSortExpressions(orderByClause);
        }

        public PropertyComparer(string propertyName, ListSortDirection sortDirection)
        {
            _sortExpressions = new[] { new SortExpression(propertyName, sortDirection) };
        }

        public PropertyComparer(SortExpression[] sortExpressions)
        {
            _sortExpressions = sortExpressions;
        }

        public SortExpression[] SortExpressions
        {
            get { return _sortExpressions; }
            set { _sortExpressions = value; }
        }

        public int Compare(object a, object b)
        {
            for (int i = 0; i < SortExpressions.Length; i++)
            {
                if (SortExpressions[i].PropertyDescriptor == null)
                {
                    PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(a);
                    SortExpressions[i].PropertyDescriptor = properties.Find(SortExpressions[i].PropertyName, true);
                }

                object value1 = SortExpressions[i].GetPropertyValue(a);
                object value2 = SortExpressions[i].GetPropertyValue(b);

                if (!(value1 is IComparable))
                    throw new ArgumentException("Property type must implement IComparable.");

                var comparable = (IComparable)value1;
                int result = comparable.CompareTo(value2);

                if (result == 0)
                    continue;

                if (SortExpressions[i].SortDirection == ListSortDirection.Ascending)
                    return result;

                return ReverseResult(result);
            }

            return 0;
        }

        private void BuildSortExpressions(string orderByClause)
        {
            string[] sortExpressions = orderByClause.Split(',');
            _sortExpressions = new SortExpression[sortExpressions.Length];

            for (int i = 0; i < sortExpressions.Length; i++)
            {
                string[] expressionParts = sortExpressions[i].Trim().Split(' ');
                _sortExpressions[i].PropertyName = expressionParts[0].Trim();

                if (expressionParts.Length == 2
                    && (expressionParts[1].Trim().ToUpperInvariant() == "DESC"
                        || expressionParts[1].Trim().ToUpperInvariant() == "DESCENDING"))
                    _sortExpressions[i].SortDirection = ListSortDirection.Descending;
                else
                    _sortExpressions[i].SortDirection = ListSortDirection.Ascending;
            }
        }

        private int ReverseResult(int result)
        {
            switch (result)
            {
                case -1:
                    return 1;
                case 0:
                    return 0;
                case 1:
                    return -1;
                default:
                    throw new Exception("Invalid compare result.");
            }
        }

        public new bool Equals(object x, object y)
        {
            return Compare(x, y) == 0;
        }

        public int GetHashCode(object obj)
        {
            int hashCode = 7381;

            for (int i = 0; i < SortExpressions.Length; i++)
            {
                if (SortExpressions[i].PropertyDescriptor == null)
                {
                    PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(obj);
                    SortExpressions[i].PropertyDescriptor = properties.Find(SortExpressions[i].PropertyName, true);
                }

                object value = SortExpressions[i].GetPropertyValue(obj);
                hashCode = ((hashCode << 5) + hashCode) ^ value.GetHashCode();
            }

            return hashCode;
        }
    }

    public class PropertyComparer<T> : PropertyComparer, IComparer<T>, IEqualityComparer<T>
    {
        public PropertyComparer(string orderByClause)
            : base(orderByClause) { }

        public PropertyComparer(string propertyName, ListSortDirection sortDirection)
            : base(propertyName, sortDirection) { }

        public PropertyComparer(SortExpression[] sortExpressions)
            : base(sortExpressions) { }

        public int Compare(T x, T y)
        {
            return base.Compare(x, y);
        }

        public bool Equals(T x, T y)
        {
            return base.Equals(x, y);
        }

        public int GetHashCode(T obj)
        {
            return base.GetHashCode(obj);
        }
    }
}