using System.Collections.Generic;

namespace BackpropServer.Models {
    public class LayerModel {
        public string projectId { get; set; }
        public int layerNumber { get; set; }
        public int size { get; set; }
        public string activation { get; set; }
    }

    public class MultipleLayersModel {
        public string projectId { get; set; }
        public List<LayerModel> data { get; set; }
    }
}