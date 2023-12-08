#if UNITY_EDITOR
using UnityEngine;
using static net.rs64.TexTransCore.TransTextureCore.Utils.TextureUtility;
namespace net.rs64.TexTransTool.MultiLayerImage
{
    [AddComponentMenu("TexTransTool/MultiLayer/TTT SolidLayer")]
    internal class SolidLayer : AbstractImageLayer
    {
        public Color Color;
        public override Texture GetImage() => CreateColorTex(Color);
    }
}
#endif