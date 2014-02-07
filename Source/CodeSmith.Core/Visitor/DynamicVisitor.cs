using System;

#if PFX_LEGACY_3_5
using CodeSmith.Core.Collections;
#else
using System.Collections.Concurrent;
#endif

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

using CodeSmith.Core.Reflection;

namespace CodeSmith.Core.Visitor
{
    public abstract class DynamicVisitor<TBase>
    {
        private static readonly ConcurrentDictionary<Type, List<VisitorMethodInfo>> _typeVisitorMethodsCache =
            new ConcurrentDictionary<Type, List<VisitorMethodInfo>>();
        private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<Type, List<VisitorMethodInfo>>> _itemTypeVisitorsCache =
            new ConcurrentDictionary<Type, ConcurrentDictionary<Type, List<VisitorMethodInfo>>>();
        
        private readonly ConcurrentDictionary<Type, List<VisitorMethodInfo>> _itemTypeVisitors =
            new ConcurrentDictionary<Type, List<VisitorMethodInfo>>();
        private readonly List<VisitorMethodInfo> _typeVisitorMethods;

        protected DynamicVisitor()
        {
            ShouldCallBaseTypeVisitors = false;
            ShouldCallMultipleVisitors = false;

            _itemTypeVisitors = _itemTypeVisitorsCache.GetOrAdd(GetType(), new ConcurrentDictionary<Type, List<VisitorMethodInfo>>());
            
            // get and cache all the visitor methods for each visitor type
            _typeVisitorMethods = _typeVisitorMethodsCache.GetOrAdd(GetType(), t =>
            {
                var list = new List<VisitorMethodInfo>();
                
                foreach (var method in t.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => m.Name == "Visit"))
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length == 1)
                        list.Add(new VisitorMethodInfo(parameters[0].ParameterType, DelegateFactory.CreateMethod(method)));
                    if (parameters.Length == 2 && parameters[1].ParameterType == typeof(CancelEventArgs))
                        list.Add(new VisitorMethodInfo(parameters[0].ParameterType, DelegateFactory.CreateMethod(method), true));
                }

                return list;
            });
        }

        protected abstract IEnumerable<TBase> GetChildren(TBase item);

        /// <summary>
        /// Determines if the visitor should call the visitors of any registered delegates that are base types of the being visited.
        /// </summary>
        public bool ShouldCallBaseTypeVisitors { get; set; }

        /// <summary>
        /// Determines if the visiting should stop after one successful visit.
        /// </summary>
        public bool ShouldCallMultipleVisitors { get; set; }

        public void Visit<TSub>(TSub item) where TSub : TBase
        {
            var continueVisiting = new CancelEventArgs();
            Type concreteType = item.GetType();

            var visitors = GetTypeVisitors(concreteType);
            foreach (var visitor in visitors)
            {
                if (!ShouldCallBaseTypeVisitors && visitor.ItemType != concreteType)
                    continue;

                InvokeVisitor(visitor, item, continueVisiting);

                if (!ShouldCallMultipleVisitors)
                    break;

                if (continueVisiting.Cancel)
                    break;
            }

            if (!continueVisiting.Cancel)
                foreach (var child in GetChildren(item))
                    Visit(child);
        }

        private IEnumerable<VisitorMethodInfo> GetTypeVisitors(Type type)
        {
            return _itemTypeVisitors.GetOrAdd(type, t =>
                {
                    var list = new List<VisitorMethodInfo>();
                    foreach (var method in _typeVisitorMethods)
                    {
                        if (method.ItemType == type)
                            list.Add(method);
                        else if (method.ItemType.IsAssignableFrom(t))
                            list.Add(method);
                    }
                        
                    return list;
                });
        }

        private void InvokeVisitor(VisitorMethodInfo methodInfo, object item, CancelEventArgs visitChildren)
        {
            if (methodInfo.HasVisitChildrenFlag)
                methodInfo.Method(this, item, visitChildren);
            else
                methodInfo.Method(this, item);
        }

        private class VisitorMethodInfo
        {
            public VisitorMethodInfo(Type itemType, LateBoundMethod method, bool hasVisitChildrenFlag = false)
            {
                ItemType = itemType;
                Method = method;
                HasVisitChildrenFlag = hasVisitChildrenFlag;
            }

            public Type ItemType { get; private set; }
            public bool HasVisitChildrenFlag { get; private set; }
            public LateBoundMethod Method { get; private set; }
        }
    }  
}
