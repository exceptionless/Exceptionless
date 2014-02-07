#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Web.UI;

namespace Exceptionless.SampleWeb {
    public partial class Default2 : Page {
        protected void Page_Load(object sender, EventArgs e) {
            try {
                throw new Exception("BLOW UP");
            } catch (Exception ex) {
                var t = new TestData {
                    Id = 1,
                    Name = "Blake"
                };
                TestData t1 = null;

                ToExceptionless(ex, 44, 55, 66, 2, e, t, t1);
                throw;
            }
        }

        private void ToExceptionless(Exception ex, params object[] paramArray) {
            ex.ToExceptionless().AddObject(paramArray).Submit();
        }
    }
}

public class TestData {
    public int Id { get; set; }
    public string Name { get; set; }

    public override string ToString() {
        return string.Format("Id = {0}, Name = {1}", Id, Name);
    }
}