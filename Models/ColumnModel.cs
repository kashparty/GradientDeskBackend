using System.Collections.Generic;

namespace BackpropServer.Models {
    public class ColumnModel {
        public string datasetId { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public bool include { get; set; }
    }

    public class MultipleColumnsModel {
        public List<ColumnModel> data { get; set; }
    }
}