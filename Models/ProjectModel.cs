namespace BackpropServer.Models {
    public class ProjectModel {
        public string projectId { get; set; }
        public string datasetId { get; set; }
        public string name { get; set; }
        public int batchSize { get; set; }
        public double learningRate { get; set; }
        public string loss { get; set; }
    }
}