namespace BackpropServer.Models {
    public class DatasetModel {
        public string datasetId { get; set; }
        public string userId { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string filetype { get; set; }
        public string url { get; set; }
    }
}