using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BackpropServer.Models {
    public class ResponseModel {
        public object data { get; set; }
        public List<string> errors { get; set; }
    }
}