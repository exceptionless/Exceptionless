using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeSmith.Core.Collections;
using NUnit.Framework;

namespace CodeSmith.Core.Tests.Collections {
    [TestFixture]
    public class ObservableHashSetTests {
        private readonly IEnumerable<string> _names = new List<string> { "Blake", "Eric", "Cody", "Marylou" };

        [Test]
        public void RemoveWhere() {
            var names = new ObservableHashSet<string>(_names);
            names.CollectionChanged += (sender, args) => Console.WriteLine(args.Action);
            for (int i = names.Count; i > 0; i--) {
                int length = i;
                names.RemoveWhere(n => n.Length > length);
            }

            Assert.AreEqual(0, names.Count);
        }

        [Test]
        public void RemoveWhereConcurrent() {
            var names = new ObservableHashSet<string>(_names);
            names.CollectionChanged += (sender, args) => Console.WriteLine(args.Action);
            for (int i = names.Count; i > 0; i--) {
                int length = i;
                var task = Task.Factory.StartNew(() => {
                    names.Add("Bob");
                    names.RemoveWhere(n => n.Equals("Bob"));
                });
                names.RemoveWhere(n => n.Length > length);
                task.WaitWithPumping();
            }

            Assert.AreEqual(0, names.Count);
        }
    }
}