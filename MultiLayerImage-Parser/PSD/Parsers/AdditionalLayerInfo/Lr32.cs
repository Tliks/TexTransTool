using System;
using net.rs64.TexTransCore;

namespace net.rs64.MultiLayerImage.Parser.PSD.AdditionalLayerInfo
{

    [Serializable, AdditionalLayerInfoParser("Lr32")]
    internal class Lr32 : AdditionalLayerInfoBase
    {
        public LayerInformationParser.LayerInfo AdditionalLayerInformation;
        public override void ParseAddLY(bool isPSB, BinarySectionStream stream)
        {
            var layerInfo = AdditionalLayerInformation = new LayerInformationParser.LayerInfo();
            layerInfo.LayersInfoSection = stream.PeekToAddress(Address.Length);
            if (layerInfo.LayersInfoSection.Length == 0) { return; }
            LayerInformationParser.ParseLayerRecordAndChannelImage(isPSB, layerInfo, stream);
        }
    }
}
