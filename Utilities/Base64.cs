using System;
using System.Text;

namespace BackpropServer {
    public static class Base64 {
        public static string UrlEncode(byte[] text) {
            return Convert.ToBase64String(text)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        public static string UrlDecode(string encoded) {
            string base64 = encoded.Replace('-', '+').Replace('_', '/');
            if (encoded.Length % 4 == 2) base64 += "==";
            else if (encoded.Length % 4 == 3) base64 += "=";

            return Encoding.UTF8.GetString(
                Convert.FromBase64String(base64)
            );
        }
    }
}