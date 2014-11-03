using System;
using System.Collections.Generic;
using System.Text;

namespace CodeSmith.Core.Tests
{
    public class DumperStub
    {
        public string Test;
        public List<int> FieldIterator;

        public DumperStub(string name, string descriptor, DumperStub stub, string test)
        {
            _name = name;
            _descriptor = descriptor;
            _stub = stub;
            Test = test;
            FieldIterator = new List<int>();
            FieldIterator.Add(10);
            FieldIterator.Add(11);
            FieldIterator.Add(12);
        }

        public List<int> EmptyItems
        { 
            get{ return new List<int>(); }
        }

        public List<int> Items
        {
            get 
            {
                List<Int32> list = new List<Int32>();                
                list.Add(1);
                list.Add(2);
                list.Add(3);
                return list; 
            }
        }

        public DateTime MyDateTime
        {
            get { return DateTime.MaxValue; }
        }

        public Object MyNullObject
        {
            get { return null; }
        }

        private string _name;
        public string Name
        {
            get { return _name; }
        }

        private string _descriptor;
        public string Descriptor
        {
            get { return _descriptor; }
        }

        private DumperStub _stub;
        public DumperStub Stub
        {
            get { return _stub; }
        }
    }
}
