using System;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Security.Cryptography;

using BackpropServer.Models;

namespace BackpropServer {
    public static class JWT {
        public static string GenerateJWT(Guid userid) {
            // Header remains the same for all JWTs
            Dictionary<string, string> header = new Dictionary<string, string>() {
                { "alg", "HS256" },
                { "typ", "JWT" }
            };

            // Create the payload
            JWTPayloadModel payload = new JWTPayloadModel {
                sub = userid.ToString(),
                exp = DateTimeOffset.UtcNow.AddDays(7)
            };

            // Serialize, convert to byte array and encode
            string encodedHeader = Base64.UrlEncode(
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(header))
            );
            string encodedPayload = Base64.UrlEncode(
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))
            );


            string signature;

            // Calculate signature string using secret
            using (HMACSHA256 hash = new HMACSHA256(EnvironmentVariables.GetSecret())) {
                signature = Base64.UrlEncode(
                    hash.ComputeHash(
                        Encoding.UTF8.GetBytes(
                            encodedHeader + "." + encodedPayload
                        )
                    )
                );
            }

            return encodedHeader + "." + encodedPayload + "." + signature;
        }

        public static JWTPayloadModel DeserializePayload(string payload) {
            return JsonSerializer.Deserialize<JWTPayloadModel>(
                Base64.UrlDecode(payload)
            );
        }

        public static bool TryParse(string jwt, out JWTPayloadModel parsedPayload) {
            parsedPayload = null;
            
            // Split the jwt into its 3 sections
            string[] sections = jwt.Split('.');

            // Prevents IndexOutOfRangeException when jwt has the wrong format
            if (sections.Length != 3) return false;

            string header = sections[0];
            string payload = sections[1];
            string signature = sections[2];

            string calculatedSignature;

            // Calculate signature and check that it is the same
            using (HMACSHA256 hash = new HMACSHA256(EnvironmentVariables.GetSecret())) {
                calculatedSignature = Base64.UrlEncode(
                    hash.ComputeHash(
                        Encoding.UTF8.GetBytes(
                            header + "." + payload
                        )
                    )
                );
            }

            if (signature != calculatedSignature) return false;

            // Check that the token has not expired
            parsedPayload = DeserializePayload(payload);
            if (DateTimeOffset.Compare(parsedPayload.exp, DateTimeOffset.UtcNow) < 0) return false;

            // Token has a valid signature and has not expired
            return true;
        }
    }
}