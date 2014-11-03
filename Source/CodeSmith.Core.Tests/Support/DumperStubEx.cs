using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace CodeSmith.Core.Tests
{
    public class DumperStubEx : DumperStub
    {
        public DumperStubEx(string name, string descriptor, DumperStub stub, string test):base(name, descriptor, stub, test){}

        public List<IEnumerable> ListOfList
        {
            get 
            {
                List<IEnumerable> outer = new List<IEnumerable>();
                List<int> inner1 = new List<int>();
                List<int> inner2 = new List<int>();
                inner1.Add(100);
                inner1.Add(200);

                inner2.Add(1000);
                inner2.Add(2000);

                outer.Add(inner1);
                outer.Add(inner2);

                return outer;
            }
        }
    }
}
