using net.rs64.TexTransTool.Utils;
using UnityEngine;
using net.rs64.TexTransCore.TransTextureCore.Utils;
using UnityEditor;
using static net.rs64.TexTransCore.BlendTexture.TextureBlend;
using net.rs64.TexTransCore.BlendTexture;

namespace net.rs64.TexTransTool.TextureStack
{
    internal class ImmediateTextureStack : TextureStack
    {
        RenderTexture renderTexture;
        public override void init(Texture2D firstTexture, ITextureManager textureManager)
        {
            base.init(firstTexture, textureManager);


            using (new RTActiveSaver())
            {
                renderTexture = RenderTexture.GetTemporary(FirstTexture.width, FirstTexture.height, 0);
                Graphics.Blit(TextureManager.GetOriginalTexture2D(FirstTexture), renderTexture);
            }
        }

        public override void AddStack(BlendTexturePair blendTexturePair)
        {
            renderTexture.BlendBlit(blendTexturePair.Texture, blendTexturePair.BlendTypeKey);

            if (blendTexturePair.Texture is RenderTexture rt && !AssetDatabase.Contains(rt))
            { RenderTexture.ReleaseTemporary(rt); }
        }

        public override Texture2D MergeStack()
        {
            renderTexture.name = FirstTexture.name + "_MergedStack";
            var resultTex = renderTexture.CopyTexture2D().CopySetting(FirstTexture, false);
            TextureManager.ReplaceTextureCompressDelegation(FirstTexture, resultTex);


            RenderTexture.ReleaseTemporary(renderTexture);
            return resultTex;
        }
    }
}