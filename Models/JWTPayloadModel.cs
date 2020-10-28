using System;

namespace BackpropServer.Models {
    public class JWTPayloadModel {
        public string sub { get; set; }
        public DateTimeOffset exp { get; set; }
    }
}