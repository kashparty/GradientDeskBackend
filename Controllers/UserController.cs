using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Net;
using System.Net.Mail;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.Extensions.Logging;
using Npgsql;

using BackpropServer.Models;

namespace BackpropServer.Controllers {
    [ApiController]
    [Route("[controller]")]
    public class UserController : ControllerBase {
        private readonly ILogger<UserController> _logger;
        private readonly string _connectionString;

        public UserController(ILogger<UserController> logger) {
            _logger = logger;
            _connectionString = EnvironmentVariables.GetConnectionString();
        }

        private byte[] GenerateHash(string password, byte[] salt) {
            // Generate a 256-bit hash using the Pbkdf2 algorithm
            return KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA1,
                iterationCount: 10000,
                numBytesRequested: 256 / 8
            );
        }

        private string RandomAlphanumericString(int length) {
            string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            string result = "";
            Random random = new Random();
            for (int i = 0; i < length; i++) {
                result += chars[random.Next(chars.Length)];
            }
            return result;
        }

        private void SendPasswordEmail(string toAddress, string newPassword) {
            string fromAddress = "gradientdeskhelp@gmail.com";
            SmtpClient client = new SmtpClient {
                Port = 587,
                Credentials = new NetworkCredential(fromAddress, "HesaraBatterbee17"),
                EnableSsl = true,
                Host = "smtp.gmail.com"
            };
            MailMessage message = new MailMessage(fromAddress, toAddress);
            message.Subject = "Password reset";
            message.Body = "Dear user,<br><br>";
            message.Body += "We've reset your password so that you can get back to using GradientDesk. ";
            message.Body += $"Your temporary password is:<pre style=\"font: monospace\">{newPassword}</pre>";
            message.Body += "You'll need to use that password to sign in. Then you can go to your account settings to create a new password. ";
            message.Body += "You can reply to this email if you need any further support.<br><br>";
            message.Body += "Thank you!";
            message.IsBodyHtml = true;

            Guid uniqueMessageIdentifier = Guid.NewGuid(); // required by SendAsync but not used in this case
            client.SendAsync(message, uniqueMessageIdentifier);
        }

        [HttpPost]
        public async Task<ActionResult<ResponseModel>> CreateUser(UserModel newUserData) {
            // Connect to database
            await using NpgsqlConnection conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Generate salt and hash
            byte[] salt = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            byte[] hash = GenerateHash(newUserData.password, salt);

            // Generate a user id
            Guid userId = Guid.NewGuid();

            // Insert new user data into users table
            await using (NpgsqlCommand cmd = new NpgsqlCommand(
                "INSERT INTO users (userid, username, salt, passwordhash, email)" +
                "VALUES (@userid, @username, @salt, @passwordhash, @email)", conn
            )) {
                cmd.Parameters.AddWithValue("userid", userId);
                cmd.Parameters.AddWithValue("username", newUserData.username);
                cmd.Parameters.AddWithValue("salt", salt);
                cmd.Parameters.AddWithValue("passwordhash", hash);
                cmd.Parameters.AddWithValue("email", newUserData.email);

                try {
                    await cmd.ExecuteNonQueryAsync();
                } catch (Npgsql.PostgresException) {
                    return new ResponseModel {
                        errors = new List<string>() { "EMAIL_TAKEN" }
                    };
                }
            }

            return new ResponseModel {
                data = new AuthorizedUserModel {
                    username = newUserData.username,
                    email = newUserData.email,
                    jwt = JWT.GenerateJWT(userId)
                }
            };
        }

        [HttpPost("login")]
        public async Task<ActionResult<ResponseModel>> LoginUser(UserModel userData) {
            // Connect to database
            await using NpgsqlConnection conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Query database
            await using (NpgsqlCommand cmd = new NpgsqlCommand(
                "SELECT users.userid, users.username, users.salt, users.passwordhash FROM users " +
                "WHERE users.email = @email", conn
            )) {
                cmd.Parameters.AddWithValue("email", userData.email);
                await using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync()) {
                    // Check if data exists
                    if (await reader.ReadAsync()) {
                        Guid userId = (Guid)reader.GetValue(0);
                        string username = (string)reader.GetValue(1);
                        byte[] salt = (byte[])reader.GetValue(2);
                        byte[] hash = (byte[])reader.GetValue(3);

                        // Check that the hashes are the same
                        byte[] calculatedHash = GenerateHash(userData.password, salt);

                        if (hash.SequenceEqual(calculatedHash)) {
                            return new ResponseModel {
                                data = new AuthorizedUserModel {
                                    username = username,
                                    email = userData.email,
                                    jwt = JWT.GenerateJWT(userId)
                                }
                            };
                        } else {
                            return new ResponseModel {
                                errors = new List<string>() { "WRONG_PASSWORD" }
                            };
                        }
                    } else {
                        return new ResponseModel {
                            errors = new List<string>() { "USER_NOT_FOUND" }
                        };
                    }
                }
            }
        }

        [HttpPatch("edituser")]
        public async Task<ActionResult<ResponseModel>> EditUser(EditUserModel editData) {
            // Check if user is authenticated
            string jwt;
            if (!Request.Headers.ContainsKey("Authorization")) {
                return new ResponseModel {
                    errors = new List<string> { "NO_JWT" }
                };
            }

            jwt = Request.Headers["Authorization"];

            JWTPayloadModel payload;
            if (!JWT.TryParse(jwt, out payload)) {
                return new ResponseModel {
                    errors = new List<string> { "JWT_INVALID" }
                };
            }

            // Generate salt and hash
            byte[] salt = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            byte[] hash = GenerateHash(editData.password, salt);

            // Connect to database
            await using NpgsqlConnection conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            string cmdString = "UPDATE users SET ";
            if (editData.usernameChanged) {
                cmdString += "username = @username";
                if (editData.passwordChanged) {
                    cmdString += ", passwordhash = @passwordhash, salt = @salt";
                }
            } else {
                if (editData.passwordChanged) {
                    cmdString += "passwordhash = @passwordhash, salt = @salt";
                }
            }

            await using (NpgsqlCommand cmd = new NpgsqlCommand(
                cmdString + " WHERE users.email = @email", conn
            )) {
                if (editData.usernameChanged) {
                    cmd.Parameters.AddWithValue("username", editData.username);
                }
                if (editData.passwordChanged) {
                    cmd.Parameters.AddWithValue("passwordhash", hash);
                    cmd.Parameters.AddWithValue("salt", salt);
                }
                cmd.Parameters.AddWithValue("email", editData.email);

                int changed = await cmd.ExecuteNonQueryAsync();
                if (changed == 0) {
                    return new ResponseModel {
                        errors = new List<string>() { "USER_NOT_FOUND" }
                    };
                }
            }

            return new ResponseModel { };
        }

        [HttpDelete]
        public async Task<ActionResult<ResponseModel>> DeleteUser(ResetOrDeleteUserModel userData) {
            // Check if user is authenticated
            string jwt;
            if (!Request.Headers.ContainsKey("Authorization")) {
                return new ResponseModel {
                    errors = new List<string> { "NO_JWT" }
                };
            }

            jwt = Request.Headers["Authorization"];

            JWTPayloadModel payload;
            if (!JWT.TryParse(jwt, out payload)) {
                return new ResponseModel {
                    errors = new List<string> { "JWT_INVALID" }
                };
            }

            // Connect to database
            await using NpgsqlConnection conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            await using (NpgsqlCommand cmd = new NpgsqlCommand(
                "DELETE FROM users WHERE users.email = @email", conn
            )) {
                cmd.Parameters.AddWithValue("email", userData.email);

                await cmd.ExecuteNonQueryAsync();
            }

            return new ResponseModel { };
        }

        [HttpPost("resetpassword")]
        public async Task<ActionResult<ResponseModel>> ResetPassword(ResetOrDeleteUserModel resetData) {

            // Create random password of that is sent to the user
            const int passwordLength = 16;
            string randomPassword = RandomAlphanumericString(passwordLength);

            // Generate salt and hash
            byte[] salt = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            byte[] hash = GenerateHash(randomPassword, salt);

            // Connect to database
            await using NpgsqlConnection conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            await using (NpgsqlCommand cmd = new NpgsqlCommand(
                "UPDATE users SET passwordhash = @passwordhash, salt = @salt WHERE users.email = @email",
                conn
            )) {
                cmd.Parameters.AddWithValue("passwordhash", hash);
                cmd.Parameters.AddWithValue("salt", salt);
                cmd.Parameters.AddWithValue("email", resetData.email);

                int changed = await cmd.ExecuteNonQueryAsync();
                if (changed == 0) {
                    return new ResponseModel {
                        errors = new List<string>() { "USER_NOT_FOUND" }
                    };
                }
            }

            // Send user email containing new random password
            SendPasswordEmail(resetData.email, randomPassword);

            return new ResponseModel { };
        }
    }
}