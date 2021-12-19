using System.Collections.Generic;

namespace BackpropServer.Models {
    public class ColumnModel {
        public string datasetId { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public bool include { get; set; }
        public int index { get; set; }
    }

    public class MultipleColumnsModel {
        public List<ColumnModel> data { get; set; }
    }

    public class DataprepModel {
        public string projectId { get; set; }
        public string datasetId { get; set; }
        public int index { get; set; }
        public string usage { get; set; }
        public string normalise { get; set; }
        public string encoding { get; set; }
        public int nodes { get; set; }
    }

    public class MultipleDataprepsModel {
        public string projectId { get; set; }
        public List<DataprepModel> data { get; set; }
    }
}