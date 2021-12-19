using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Npgsql;

using BackpropServer.Models;

namespace BackpropServer.Controllers {
    [ApiController]
    [Route("[controller]")]
    public class DatasetController : ControllerBase {
        private readonly ILogger<DatasetController> _logger;
        private readonly string _connectionString;
        public DatasetController(ILogger<DatasetController> logger) {
            _logger = logger;
            _connectionString = EnvironmentVariables.GetConnectionString();
        }

        [HttpPost]
        public async Task<ActionResult<ResponseModel>> CreateDataset(DatasetModel newDatasetData) {
            // Check if user is authorized
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

            // Generate a datasetId
            Guid datasetId = Guid.NewGuid();

            await using (NpgsqlCommand cmd = new NpgsqlCommand(
                "INSERT INTO datasets (datasetid, userid, name, description, filetype, url, readheaders)" +
                "VALUES (@datasetid, @userid, @name, @description, @filetype, @url, false)", conn
            )) {
                cmd.Parameters.AddWithValue("datasetid", datasetId);
                cmd.Parameters.AddWithValue("userid", Guid.Parse(payload.sub));
                cmd.Parameters.AddWithValue("name", newDatasetData.name);
                cmd.Parameters.AddWithValue("description", newDatasetData.description);
                cmd.Parameters.AddWithValue("filetype", newDatasetData.filetype);
                cmd.Parameters.AddWithValue("url", newDatasetData.url);

                await cmd.ExecuteNonQueryAsync();
            }

            // The website will need the datasetId immediately
            // so that the dataset can be loaded and edited
            return new ResponseModel {
                data = datasetId.ToString()
            };
        }

        [HttpGet("{datasetId}")]
        public async Task<ActionResult<ResponseModel>> GetDataset(string datasetId) {
            // Check if user is authorized
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
                "SELECT datasets.name, datasets.description, datasets.filetype, datasets.url, datasets.readheaders " +
                "FROM datasets WHERE datasets.userid = @userid AND datasets.datasetid = @datasetid", conn
            )) {
                cmd.Parameters.AddWithValue("userid", Guid.Parse(payload.sub));
                cmd.Parameters.AddWithValue("datasetid", Guid.Parse(datasetId));

                await using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync()) {
                    // Check if dataset exists
                    if (await reader.ReadAsync()) {
                        string name = (string)reader.GetValue(0);
                        string description = (string)reader.GetValue(1);
                        string filetype = (string)reader.GetValue(2);
                        string url = (string)reader.GetValue(3);
                        bool readHeaders = (bool)reader.GetValue(4);

                        return new ResponseModel {
                            data = new DatasetModel {
                                datasetId = datasetId.ToString(),
                                name = name,
                                description = description,
                                filetype = filetype,
                                url = url,
                                readHeaders = readHeaders,
                            }
                        };
                    } else {
                        return new ResponseModel {
                            errors = new List<string> { "DATASET_NOT_FOUND" }
                        };
                    }
                }
            }
        }

        [HttpPut]
        public async Task<ActionResult<ResponseModel>> UpdateDataset(DatasetModel newDatasetData) {
            // Check if user is authorized
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
                "UPDATE datasets SET name = @name, description = @description, readheaders = @readheaders " +
                "WHERE datasets.datasetid = @datasetid AND datasets.userid = @userid", conn
            )) {
                cmd.Parameters.AddWithValue("name", newDatasetData.name);
                cmd.Parameters.AddWithValue("description", newDatasetData.description);
                cmd.Parameters.AddWithValue("readheaders", newDatasetData.readHeaders);
                cmd.Parameters.AddWithValue("datasetid", Guid.Parse(newDatasetData.datasetId));
                cmd.Parameters.AddWithValue("userid", Guid.Parse(payload.sub));

                await cmd.ExecuteNonQueryAsync();
            }

            return new ResponseModel { };
        }

        [HttpGet("all")]
        public async Task<ActionResult<ResponseModel>> AllDatasets() {
            // Check if user is authorized
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
                "SELECT datasets.datasetid, datasets.name, datasets.description " +
                "FROM datasets WHERE datasets.userid = @userid", conn
            )) {
                cmd.Parameters.AddWithValue("userid", Guid.Parse(payload.sub));
                await using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync()) {
                    List<DatasetModel> allDatasets = new List<DatasetModel>();
                    // Read all records
                    while (await reader.ReadAsync()) {
                        Guid datasetId = (Guid)reader.GetValue(0);
                        string name = (string)reader.GetValue(1);
                        string description = (string)reader.GetValue(2);

                        allDatasets.Add(new DatasetModel {
                            datasetId = datasetId.ToString(),
                            name = name,
                            description = description,
                        });
                    }

                    return new ResponseModel {
                        data = allDatasets
                    };
                }
            }
        }

        [HttpGet("{datasetId}/columns")]
        public async Task<ActionResult<ResponseModel>> GetDatasetColumns(string datasetId) {
            // Check if user is authorized
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
                "SELECT columns.name, columns.type, columns.include, columns.index " +
                "FROM columns WHERE columns.datasetid = @datasetid", conn
            )) {
                cmd.Parameters.AddWithValue("datasetid", Guid.Parse(datasetId));

                await using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync()) {
                    List<ColumnModel> allColumns = new List<ColumnModel>();
                    // Read all records
                    while (await reader.ReadAsync()) {
                        string name = (string)reader.GetValue(0);
                        string type = (string)reader.GetValue(1);
                        bool include = (bool)reader.GetValue(2);
                        int index = (int)reader.GetValue(3);

                        allColumns.Add(new ColumnModel {
                            datasetId = datasetId,
                            name = name,
                            type = type,
                            include = include,
                            index = index
                        });
                    }

                    allColumns = allColumns.OrderBy(c => c.index).ToList();

                    return new ResponseModel {
                        data = allColumns
                    };
                }
            }
        }

        [HttpPost("columns")]
        public async Task<ActionResult<ResponseModel>> CreateColumns(MultipleColumnsModel newColumns) {
            // Check if user is authorized
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
            foreach (ColumnModel newColumnData in newColumns.data) {
                await using (NpgsqlCommand cmd = new NpgsqlCommand(
                "INSERT INTO columns (datasetid, name, type, include, index)" +
                "VALUES (@datasetid, @name, @type, @include, @index)", conn
                )) {
                    cmd.Parameters.AddWithValue("datasetid", Guid.Parse(newColumnData.datasetId));
                    cmd.Parameters.AddWithValue("name", newColumnData.name);
                    cmd.Parameters.AddWithValue("type", newColumnData.type);
                    cmd.Parameters.AddWithValue("include", newColumnData.include);
                    cmd.Parameters.AddWithValue("index", newColumnData.index);

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return new ResponseModel { };
        }

        [HttpPut("columns")]
        public async Task<ActionResult<ResponseModel>> UpdateColumns(MultipleColumnsModel newColumns) {
            // Check if user is authorized
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
            foreach (ColumnModel newColumnData in newColumns.data) {
                await using (NpgsqlCommand cmd = new NpgsqlCommand(
                    "UPDATE columns SET name = @name, type = @type, include = @include " +
                    "WHERE columns.datasetid = @datasetid AND columns.index = @index", conn
                )) {
                    cmd.Parameters.AddWithValue("name", newColumnData.name);
                    cmd.Parameters.AddWithValue("type", newColumnData.type);
                    cmd.Parameters.AddWithValue("include", newColumnData.include);
                    cmd.Parameters.AddWithValue("datasetid", Guid.Parse(newColumnData.datasetId));
                    cmd.Parameters.AddWithValue("index", newColumnData.index);

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return new ResponseModel { };
        }

        [HttpDelete("{datasetId}")]
        public async Task<ActionResult<ResponseModel>> DeleteDataset(string datasetId) {
            // Check if user is authorized
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
                "DELETE FROM columns WHERE columns.datasetid IN (SELECT datasets.datasetid FROM datasets " + 
                "WHERE datasets.datasetid = @datasetid AND datasets.userid = @userid)", conn
            )) {
                cmd.Parameters.AddWithValue("datasetid", Guid.Parse(datasetId));
                cmd.Parameters.AddWithValue("userid", Guid.Parse(payload.sub));

                await cmd.ExecuteNonQueryAsync();
            }

            await using (NpgsqlCommand cmd = new NpgsqlCommand(
                "DELETE FROM datasets WHERE datasets.datasetid = @datasetid " + 
                "AND datasets.userid = @userid", conn
            )) {
                cmd.Parameters.AddWithValue("datasetid", Guid.Parse(datasetId));
                cmd.Parameters.AddWithValue("userid", Guid.Parse(payload.sub));

                await cmd.ExecuteNonQueryAsync();
            }
            
            return new ResponseModel { };
        }
    }
}