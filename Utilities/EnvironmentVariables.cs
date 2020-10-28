using System;
using System.Text;

namespace BackpropServer {
    public static class EnvironmentVariables {
        public static string GetConnectionString() {
            // Heroku provides this environment variable
            string databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
            string connectionString = "";
            
            if (string.IsNullOrEmpty(databaseUrl)) {
                // For development
                connectionString = Environment.GetEnvironmentVariable("DEV_CONN_STRING");
            } else {
                Uri uri = new Uri(databaseUrl);

                // Parse database URL. Format is postgres://<username>:<password>@<host>/<dbname>
                string username = uri.UserInfo.Split(':')[0];
                string password = uri.UserInfo.Split(':')[1];

                connectionString =
                    "Host=" + uri.Host +
                    "; Database=" + uri.AbsolutePath.Substring(1) +
                    "; Username=" + username +
                    "; Password=" + password +
                    "; Port=" + uri.Port +
                    "; SSL Mode=Require; Trust Server Certificate=true;";
            }

            return connectionString;
        }

        public static byte[] GetSecret() {
            string secret = Environment.GetEnvironmentVariable("SECRET");
            if (string.IsNullOrEmpty(secret)) {
                secret = "development";
            }
            return Encoding.UTF8.GetBytes(secret);
        }
    }
}