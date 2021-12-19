namespace BackpropServer.Models {
    public class ProjectModel {
        public string projectId { get; set; }
        public string userId { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public int batchSize { get; set; }
        public string optimiser { get; set; }
        public string loss { get; set; }
        public bool shuffle { get; set; }
        public double testSplit { get; set; }
        public bool excludeCorrupt { get; set; }
        public string outputActivation { get; set; }
    }
}