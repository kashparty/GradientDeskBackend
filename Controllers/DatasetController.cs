using System;
using System.Threading.Tasks;
using System.Collections.Generic;

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
                "INSERT INTO datasets (datasetid, userid, name, description, filetype, url)" +
                "VALUES (@datasetid, @userid, @name, @description, @filetype, @url)", conn
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
                "SELECT datasets.name, datasets.description, datasets.filetype, datasets.url " +
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

                        return new ResponseModel {
                            data = new DatasetModel {
                                datasetId = datasetId.ToString(),
                                name = name,
                                description = description,
                                filetype = filetype,
                                url = url
                            }
                        };
                    } else {
                        return new ResponseModel {
                            errors = new List<string>{ "DATASET_NOT_FOUND" }
                        };
                    }
                }
            }
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

        [HttpPost("/column")]
        public async Task<ActionResult<ResponseModel>> CreateColumn(List<ColumnModel> newColumns) {
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

            foreach (ColumnModel newColumnData in newColumns) {
                await using(NpgsqlCommand cmd = new NpgsqlCommand(
                "INSERT INTO columns (columnid, datasetid, name, type, include)" +
                "VALUES (@columnid, @datasetid, @name, @type, @include)", conn
                )) {
                    cmd.Parameters.AddWithValue(Guid.NewGuid());
                    cmd.Parameters.AddWithValue("datasetid", Guid.Parse(newColumnData.datasetId));
                    cmd.Parameters.AddWithValue("name", newColumnData.name);
                    cmd.Parameters.AddWithValue("type", newColumnData.type);
                    cmd.Parameters.AddWithValue("include", newColumnData.include);

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return new ResponseModel {

            };
        }
    }
}