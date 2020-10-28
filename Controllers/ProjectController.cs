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
    public class ProjectController : ControllerBase {
        private readonly ILogger<ProjectController> _logger;
        private readonly string _connectionString;

        public ProjectController(ILogger<ProjectController> logger) {
            _logger = logger;
            _connectionString = EnvironmentVariables.GetConnectionString();
        }

        [HttpPost]
        public async Task<ActionResult<ResponseModel>> CreateModel(ProjectModel newProjectData) {
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
                "INSERT INTO projects (projectid, userid, datasetid, name, batchsize, learningrate, loss)" +
                "VALUES (@projectid, @userid, @datasetid, @name, @batchsize, @learningrate, @loss)", conn
            )) {
                cmd.Parameters.AddWithValue("projectid", Guid.NewGuid());
                cmd.Parameters.AddWithValue("userid", Guid.Parse(payload.sub));
                cmd.Parameters.AddWithValue("datasetid", Guid.Parse(newProjectData.datasetId));
                cmd.Parameters.AddWithValue("name", newProjectData.name);
                cmd.Parameters.AddWithValue("batchsize", newProjectData.batchSize);
                cmd.Parameters.AddWithValue("learningrate", newProjectData.learningRate);
                cmd.Parameters.AddWithValue("loss", newProjectData.loss);

                await cmd.ExecuteNonQueryAsync();
            }

            return new ResponseModel {
                data = "OK"
            };
        }

        [HttpGet("all")]
        public async Task<ActionResult<ResponseModel>> AllProjects() {
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
                "SELECT projects.projectid, projects.datasetid, projects.name, projects.batchsize, " + 
                "projects.learningrate, projects.loss FROM projects " + 
                "WHERE projects.userid = @userid", conn
            )) {
                cmd.Parameters.AddWithValue("userid", Guid.Parse(payload.sub));
                await using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync()) {
                    List<ProjectModel> allProjects = new List<ProjectModel>();
                    // Read all records
                    while (await reader.ReadAsync()) {
                        Guid projectId = (Guid)reader.GetValue(0);
                        Guid datasetId = (Guid)reader.GetValue(1);
                        string name = (string)reader.GetValue(2);
                        int batchSize = (int)reader.GetValue(3);
                        double learningRate = (double)reader.GetValue(4);
                        string loss = (string)reader.GetValue(5);

                        allProjects.Add(
                            new ProjectModel {
                                projectId = projectId.ToString(),
                                datasetId = datasetId.ToString(),
                                name = name,
                                batchSize = batchSize,
                                learningRate = learningRate,
                                loss = loss
                            }
                        );
                    }

                    return new ResponseModel {
                        data = allProjects
                    };
                }
            }
        }
    }
}