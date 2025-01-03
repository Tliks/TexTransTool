using System;
using System.Collections.Generic;

namespace net.rs64.TexTransTool.MultiLayerImage.LayerData
{
    [Serializable]
    public class LayerFolderData : AbstractLayerData
    {
        public bool PassThrough;
        public List<AbstractLayerData> Layers;
    }
}