using System;

namespace Exceptionless.Core.Models {
    public interface IData {
        DataDictionary Data { get; set; }
    }
}