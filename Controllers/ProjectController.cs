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
    public class ProjectController : ControllerBase {
        private readonly ILogger<ProjectController> _logger;
        private readonly string _connectionString;

        public ProjectController(ILogger<ProjectController> logger) {
            _logger = logger;
            _connectionString = EnvironmentVariables.GetConnectionString();
        }

        [HttpPost]
        public async Task<ActionResult<ResponseModel>> CreateProject(ProjectModel newProjectData) {
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

            // Generate a projectId
            Guid projectId = Guid.NewGuid();

            await using (NpgsqlCommand cmd = new NpgsqlCommand(
                "INSERT INTO projects (projectid, userid, name, description, batchsize, optimiser, loss, shuffle, testsplit, excludecorrupt, outputactivation)" +
                "VALUES (@projectid, @userid, @name, @description, @batchsize, @optimiser, @loss, @shuffle, @testsplit, @excludecorrupt, @outputactivation)", conn
            )) {
                cmd.Parameters.AddWithValue("projectid", projectId);
                cmd.Parameters.AddWithValue("userid", Guid.Parse(payload.sub));
                cmd.Parameters.AddWithValue("name", newProjectData.name);
                cmd.Parameters.AddWithValue("description", newProjectData.description);

                // Reasonable default values
                cmd.Parameters.AddWithValue("batchsize", 32);
                cmd.Parameters.AddWithValue("optimiser", "adam,0.001,0.9,0.999");
                cmd.Parameters.AddWithValue("loss", "mse");
                cmd.Parameters.AddWithValue("shuffle", true);
                cmd.Parameters.AddWithValue("testsplit", 0.2);
                cmd.Parameters.AddWithValue("excludecorrupt", true);
                cmd.Parameters.AddWithValue("outputactivation", "linear");

                await cmd.ExecuteNonQueryAsync();
            }

            return new ResponseModel {
                data = projectId.ToString()
            };
        }

        [HttpPut]
        public async Task<ActionResult<ResponseModel>> UpdateProject(ProjectModel newProjectData) {
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

            // Generate a projectId
            Guid projectId = Guid.NewGuid();

            await using (NpgsqlCommand cmd = new NpgsqlCommand(
                "UPDATE projects SET name = @name, description = @description, batchsize = @batchsize, optimiser = @optimiser, " +
                "loss = @loss, shuffle = @shuffle, testsplit = @testsplit, excludecorrupt = @excludecorrupt, outputactivation = @outputactivation " +
                "WHERE projects.projectid = @projectid AND projects.userid = @userid", conn
            )) {
                cmd.Parameters.AddWithValue("name", newProjectData.name);
                cmd.Parameters.AddWithValue("description", newProjectData.description);
                cmd.Parameters.AddWithValue("batchsize", newProjectData.batchSize);
                cmd.Parameters.AddWithValue("optimiser", newProjectData.optimiser);
                cmd.Parameters.AddWithValue("loss", newProjectData.loss);
                cmd.Parameters.AddWithValue("shuffle", newProjectData.shuffle);
                cmd.Parameters.AddWithValue("testsplit", newProjectData.testSplit);
                cmd.Parameters.AddWithValue("excludecorrupt", newProjectData.excludeCorrupt);
                cmd.Parameters.AddWithValue("outputactivation", newProjectData.outputActivation);

                cmd.Parameters.AddWithValue("projectid", Guid.Parse(newProjectData.projectId));
                cmd.Parameters.AddWithValue("userid", Guid.Parse(payload.sub));

                await cmd.ExecuteNonQueryAsync();
            }

            return new ResponseModel {
                data = projectId.ToString()
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
                "SELECT projects.projectid, projects.name, projects.description " +
                "FROM projects WHERE projects.userid = @userid", conn
            )) {
                cmd.Parameters.AddWithValue("userid", Guid.Parse(payload.sub));
                await using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync()) {
                    List<ProjectModel> allProjects = new List<ProjectModel>();
                    // Read all records
                    while (await reader.ReadAsync()) {
                        Guid projectId = (Guid)reader.GetValue(0);
                        string name = (string)reader.GetValue(1);
                        string description = (string)reader.GetValue(2);

                        allProjects.Add(
                            new ProjectModel {
                                projectId = projectId.ToString(),
                                userId = payload.sub,
                                name = name,
                                description = description
                            }
                        );
                    }

                    return new ResponseModel {
                        data = allProjects
                    };
                }
            }
        }

        [HttpGet("{projectId}")]
        public async Task<ActionResult<ResponseModel>> GetProject(string projectId) {
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
                "SELECT projects.name, projects.description, projects.batchsize, projects.optimiser, " +
                "projects.loss, projects.shuffle, projects.testsplit, projects.excludecorrupt, projects.outputactivation " +
                "FROM projects WHERE projects.projectid = @projectid AND projects.userid = @userid", conn
            )) {
                cmd.Parameters.AddWithValue("projectid", Guid.Parse(projectId));
                cmd.Parameters.AddWithValue("userid", Guid.Parse(payload.sub));

                await using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync()) {
                    // Check if project exists
                    if (await reader.ReadAsync()) {
                        string name = (string)reader.GetValue(0);
                        string description = (string)reader.GetValue(1);
                        int batchSize = (int)reader.GetValue(2);
                        string optimiser = (string)reader.GetValue(3);
                        string loss = (string)reader.GetValue(4);
                        bool shuffle = (bool)reader.GetValue(5);
                        double testSplit = (double)reader.GetValue(6);
                        bool excludeCorrupt = (bool)reader.GetValue(7);
                        string outputActivation = (string)reader.GetValue(8);

                        return new ResponseModel {
                            data = new ProjectModel {
                                projectId = projectId,
                                name = name,
                                description = description,
                                batchSize = batchSize,
                                optimiser = optimiser,
                                loss = loss,
                                shuffle = shuffle,
                                testSplit = testSplit,
                                excludeCorrupt = excludeCorrupt,
                                outputActivation = outputActivation
                            }
                        };
                    } else {
                        return new ResponseModel {
                            errors = new List<string> { "PROJECT_NOT_FOUND" }
                        };
                    }
                }
            }
        }

        [HttpDelete("{projectId}")]
        public async Task<ActionResult<ResponseModel>> DeleteProject(string projectId) {
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
                "DELETE FROM datapreps WHERE datapreps.projectid IN (SELECT projects.projectid " +
                "FROM projects WHERE projects.projectid = @projectid AND projects.userid = @userid)", conn
            )) {
                cmd.Parameters.AddWithValue("projectid", Guid.Parse(projectId));
                cmd.Parameters.AddWithValue("userid", Guid.Parse(payload.sub));

                await cmd.ExecuteNonQueryAsync();
            }

            await using (NpgsqlCommand cmd = new NpgsqlCommand(
                "DELETE FROM layers WHERE layers.projectid = @projectid", conn
            )) {
                cmd.Parameters.AddWithValue("projectid", Guid.Parse(projectId));
                await cmd.ExecuteNonQueryAsync();
            }

            await using (NpgsqlCommand cmd = new NpgsqlCommand(
                "DELETE FROM projects WHERE projects.projectid = @projectid " +
                "AND projects.userid = @userid", conn
            )) {
                cmd.Parameters.AddWithValue("projectid", Guid.Parse(projectId));
                cmd.Parameters.AddWithValue("userid", Guid.Parse(payload.sub));

                await cmd.ExecuteNonQueryAsync();
            }

            return new ResponseModel { };
        }

        [HttpGet("{projectId}/dataprep")]
        public async Task<ActionResult<ResponseModel>> GetDatapreps(string projectId) {
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
                "SELECT datapreps.datasetid, datapreps.index, datapreps.usage, datapreps.normalise, datapreps.encoding, " +
                "datapreps.nodes FROM projects, datapreps WHERE datapreps.projectid = projects.projectid " +
                "AND projects.projectid = @projectid AND projects.userid = @userid", conn
            )) {
                cmd.Parameters.AddWithValue("projectid", Guid.Parse(projectId));
                cmd.Parameters.AddWithValue("userid", Guid.Parse(payload.sub));

                await using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync()) {
                    List<DataprepModel> allDatapreps = new List<DataprepModel>();
                    // Read all records
                    while (await reader.ReadAsync()) {
                        Guid datasetId = (Guid)reader.GetValue(0);
                        int index = (int)reader.GetValue(1);
                        string usage = (string)reader.GetValue(2);
                        string normalise = (string)reader.GetValue(3);
                        string encoding = (string)reader.GetValue(4);
                        int nodes = (int)reader.GetValue(5);

                        allDatapreps.Add(new DataprepModel {
                            datasetId = datasetId.ToString(),
                            index = index,
                            usage = usage,
                            normalise = normalise,
                            encoding = encoding,
                            nodes = nodes
                        });
                    }

                    allDatapreps = allDatapreps.OrderBy(c => c.index).ToList();

                    return new ResponseModel {
                        data = allDatapreps
                    };
                }
            }
        }

        [HttpDelete("{projectId}/dataprep")]
        public async Task<ActionResult<ResponseModel>> DeleteDatapreps(string projectId) {
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
                "DELETE FROM layers WHERE layers.projectid = @projectid", conn
            )) {
                cmd.Parameters.AddWithValue("projectid", Guid.Parse(projectId));
                await cmd.ExecuteNonQueryAsync();
            }

            // Delete all datapreps belonging to this project (checking that the project belongs
            // to the authenticated user)
            await using (NpgsqlCommand cmd = new NpgsqlCommand(
                "DELETE FROM datapreps WHERE datapreps.projectid IN (SELECT projects.projectid " +
                "FROM projects WHERE projects.projectid = @projectid AND projects.userid = @userid)", conn
            )) {
                cmd.Parameters.AddWithValue("projectid", Guid.Parse(projectId));
                cmd.Parameters.AddWithValue("userid", Guid.Parse(payload.sub));

                await cmd.ExecuteNonQueryAsync();
            }

            return new ResponseModel { };
        }

        [HttpPost("dataprep")]
        public async Task<ActionResult<ResponseModel>> CreateDatapreps(MultipleDataprepsModel newDatapreps) {
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

            // Aggregate SQL function - we only need to check that the user really does own this project
            await using (NpgsqlCommand cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM projects WHERE projects.userid = @userid AND projects.projectid = @projectid",
                conn
            )) {
                cmd.Parameters.AddWithValue("userid", Guid.Parse(payload.sub));
                cmd.Parameters.AddWithValue("projectid", Guid.Parse(newDatapreps.projectId));

                long count = (long)await cmd.ExecuteScalarAsync();
                if (count < 1) {
                    return new ResponseModel {
                        errors = new List<string> { "PROJECT_NOT_OWNED" }
                    };
                }
            }

            foreach (DataprepModel newDataprep in newDatapreps.data) {
                await using (NpgsqlCommand cmd = new NpgsqlCommand(
                    "INSERT INTO datapreps (projectid, datasetid, index, usage, normalise, encoding, nodes) " +
                    "VALUES (@projectid, @datasetid, @index, @usage, @normalise, @encoding, @nodes)", conn
                )) {
                    cmd.Parameters.AddWithValue("projectid", Guid.Parse(newDatapreps.projectId));
                    cmd.Parameters.AddWithValue("datasetid", Guid.Parse(newDataprep.datasetId));
                    cmd.Parameters.AddWithValue("index", newDataprep.index);
                    cmd.Parameters.AddWithValue("usage", newDataprep.usage);
                    cmd.Parameters.AddWithValue("normalise", newDataprep.normalise);
                    cmd.Parameters.AddWithValue("encoding", newDataprep.encoding);
                    cmd.Parameters.AddWithValue("nodes", newDataprep.nodes);

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return new ResponseModel { };
        }

        [HttpPut("dataprep")]
        public async Task<ActionResult<ResponseModel>> UpdateDatapreps(MultipleDataprepsModel newDatapreps) {
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

            foreach (DataprepModel newDataprep in newDatapreps.data) {
                // Cross-table parameterised SQL to ensure that the datapreps are being updated by
                // the author of the project.
                await using (NpgsqlCommand cmd = new NpgsqlCommand(
                    "UPDATE datapreps SET usage = @usage, normalise = @normalise, encoding = @encoding, nodes = @nodes " +
                    "FROM projects WHERE datapreps.projectid = projects.projectid AND datapreps.index = @index " +
                    "AND projects.projectid = @projectid AND projects.userid = @userid", conn
                )) {
                    cmd.Parameters.AddWithValue("usage", newDataprep.usage);
                    cmd.Parameters.AddWithValue("normalise", newDataprep.normalise);
                    cmd.Parameters.AddWithValue("encoding", newDataprep.encoding);
                    cmd.Parameters.AddWithValue("nodes", newDataprep.nodes);
                    cmd.Parameters.AddWithValue("projectid", Guid.Parse(newDatapreps.projectId));
                    cmd.Parameters.AddWithValue("index", newDataprep.index);
                    cmd.Parameters.AddWithValue("userid", Guid.Parse(payload.sub));

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return new ResponseModel { };
        }

        [HttpGet("{projectId}/layer")]
        public async Task<ActionResult<ResponseModel>> GetLayers(string projectId) {
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
                "SELECT layers.layernumber, layers.size, layers.activation FROM layers, projects " +
                "WHERE layers.projectid = projects.projectid AND projects.projectid = @projectid " +
                "AND projects.userid = @userid", conn
            )) {
                cmd.Parameters.AddWithValue("projectid", Guid.Parse(projectId));
                cmd.Parameters.AddWithValue("userid", Guid.Parse(payload.sub));

                await using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync()) {
                    List<LayerModel> allLayers = new List<LayerModel>();
                    // Read all records
                    while (await reader.ReadAsync()) {
                        int layerNumber = (int)reader.GetValue(0);
                        int size = (int)reader.GetValue(1);
                        string activation = (string)reader.GetValue(2);

                        allLayers.Add(new LayerModel {
                            projectId = projectId,
                            layerNumber = layerNumber,
                            size = size,
                            activation = activation
                        });
                    }

                    allLayers = allLayers.OrderBy(l => l.layerNumber).ToList();

                    return new ResponseModel {
                        data = allLayers
                    };
                }
            }
        }

        [HttpPost("layer")]
        public async Task<ActionResult<ResponseModel>> CreateLayers(MultipleLayersModel newLayers) {
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
                "SELECT COUNT(*) FROM projects WHERE projects.userid = @userid AND projects.projectid = @projectid",
                conn
            )) {
                cmd.Parameters.AddWithValue("userid", Guid.Parse(payload.sub));
                cmd.Parameters.AddWithValue("projectid", Guid.Parse(newLayers.projectId));

                long count = (long)await cmd.ExecuteScalarAsync();
                if (count < 1) {
                    return new ResponseModel {
                        errors = new List<string> { "PROJECT_NOT_OWNED" }
                    };
                }
            }

            foreach (LayerModel newLayer in newLayers.data) {
                await using (NpgsqlCommand cmd = new NpgsqlCommand(
                    "INSERT INTO layers (projectid, layernumber, size, activation) " +
                    "VALUES (@projectid, @layernumber, @size, @activation)", conn
                )) {
                    cmd.Parameters.AddWithValue("projectid", Guid.Parse(newLayers.projectId));
                    cmd.Parameters.AddWithValue("layernumber", newLayer.layerNumber);
                    cmd.Parameters.AddWithValue("size", newLayer.size);
                    cmd.Parameters.AddWithValue("activation", newLayer.activation);

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return new ResponseModel { };
        }

        [HttpPut("layer")]
        public async Task<ActionResult<ResponseModel>> UpdateLayers(MultipleLayersModel newLayers) {
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
                "SELECT COUNT(*) FROM projects WHERE projects.userid = @userid AND projects.projectid = @projectid",
                conn
            )) {
                cmd.Parameters.AddWithValue("userid", Guid.Parse(payload.sub));
                cmd.Parameters.AddWithValue("projectid", Guid.Parse(newLayers.projectId));

                long count = (long)await cmd.ExecuteScalarAsync();
                if (count < 1) {
                    return new ResponseModel {
                        errors = new List<string> { "PROJECT_NOT_OWNED" }
                    };
                }
            }

            await using (NpgsqlCommand cmd = new NpgsqlCommand(
                "DELETE FROM layers WHERE layers.projectid = @projectid", conn
            )) {
                cmd.Parameters.AddWithValue("projectid", Guid.Parse(newLayers.projectId));
                await cmd.ExecuteNonQueryAsync();
            }

            foreach (LayerModel newLayer in newLayers.data) {
                await using (NpgsqlCommand cmd = new NpgsqlCommand(
                    "INSERT INTO layers (projectid, layernumber, size, activation) " +
                    "VALUES (@projectid, @layernumber, @size, @activation)", conn
                )) {
                    cmd.Parameters.AddWithValue("projectid", Guid.Parse(newLayers.projectId));
                    cmd.Parameters.AddWithValue("layernumber", newLayer.layerNumber);
                    cmd.Parameters.AddWithValue("size", newLayer.size);
                    cmd.Parameters.AddWithValue("activation", newLayer.activation);

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            return new ResponseModel { };
        }
    }
}