#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.IO;
using System.Windows.Forms;
using Exceptionless.SampleWindows;

namespace Tester {
    public partial class FilterForm : Form {
        public FilterForm() {
            InitializeComponent();
        }

        private void FilterForm_Load(object sender, EventArgs e) {}

        private void configButton_Click(object sender, EventArgs e) {}

        private void ignoredButton_Click(object sender, EventArgs e) {
            string path = Path.GetRandomFileName();

            //try to open a file
            //simulate filenotfound exception
            string buffer = File.ReadAllText(path);
        }

        private void acceptedButton_Click(object sender, EventArgs e) {
            FilterTest.RunTest();
        }
    }
}

namespace Exceptionless.SampleWindows {
    public class FilterTest {
        public static void RunTest() {
            string path = Path.GetRandomFileName();

            //try to open a file
            //simulate filenotfound exception
            string buffer = File.ReadAllText(path);
        }
    }
}