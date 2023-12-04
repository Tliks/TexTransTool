using System;
using System.Collections.Generic;
using UnityEngine;

namespace net.rs64.MultiLayerImageParser.LayerData
{
    [Serializable]
    public class LayerFolderData : AbstractLayerData
    {
        public bool PassThrough;
        public List<AbstractLayerData> Layers;
    }
}