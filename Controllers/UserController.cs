using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;

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

                await cmd.ExecuteNonQueryAsync();
            }

            return new ResponseModel {
                data = JWT.GenerateJWT(userId)
            };
        }

        [HttpPost("login")]
        public async Task<ActionResult<ResponseModel>> LoginUser(UserModel userData) {
            // Connect to database
            await using NpgsqlConnection conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Query database
            await using (NpgsqlCommand cmd = new NpgsqlCommand(
                "SELECT users.userid, users.salt, users.passwordhash FROM users " +
                "WHERE users.email = @email", conn
            )) {
                cmd.Parameters.AddWithValue("email", userData.email);
                await using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync()) {
                    // Check if data exists
                    if (await reader.ReadAsync()) {
                        Guid userid = (Guid)reader.GetValue(0);
                        byte[] salt = (byte[])reader.GetValue(1);
                        byte[] hash = (byte[])reader.GetValue(2);

                        // Check that the hashes are the same
                        byte[] calculatedHash = GenerateHash(userData.password, salt);

                        if (hash.SequenceEqual(calculatedHash)) {
                            return new ResponseModel {
                                data = JWT.GenerateJWT(userid)
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
    }
}