#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using net.rs64.TexTransTool.Utils;
using UnityEditor;
using UnityEngine;

namespace net.rs64.TexTransTool
{
    public readonly struct TextureManager : ITextureManager
    {
        private readonly bool Previewing;
        private readonly List<Texture2D> DestroyList;
        private readonly Dictionary<Texture2D, TextureFormat> CompressDict;

        public TextureManager(bool previewing)
        {
            Previewing = previewing;
            if (!Previewing) { DestroyList = new List<Texture2D>(); }
            else { DestroyList = null; }
            if (!Previewing) { CompressDict = new Dictionary<Texture2D, TextureFormat>(); }
            else { CompressDict = null; }
        }

        public void DeferDestroyTexture2D(Texture2D texture2D)
        {
            DestroyList.Add(texture2D);
        }

        public void DeferTexDestroy()
        {
            if (DestroyList == null) { return; }
            foreach (var tex in DestroyList)
            {
                if (tex == null) { continue; }
                UnityEngine.Object.DestroyImmediate(tex);
            }
            DestroyList.Clear();
        }

        public Texture2D GetOriginalTexture2D(Texture2D texture2D)
        {
            if (Previewing)
            {
                return texture2D;
            }
            else
            {
                var originTex = texture2D.TryGetUnCompress();
                DeferDestroyTexture2D(originTex);
                return originTex;
            }
        }
        public void TextureCompressDelegation(TextureFormat CompressFormat, Texture2D Target)
        {
            if (CompressDict == null) { return; }
            CompressDict[Target] = CompressFormat;
        }
        public void ReplaceTextureCompressDelegation(Texture2D Souse, Texture2D Target)
        {
            if (CompressDict == null) { return; }
            if (Target == Souse) { return; }
            if (CompressDict.ContainsKey(Souse))
            {
                CompressDict[Target] = CompressDict[Souse];
                CompressDict.Remove(Souse);
            }
            else
            {
                CompressDict[Target] = Souse.format;
            }
        }

        public void TexCompressDelegationInvoke()
        {
            if (CompressDict == null) { return; }
            foreach (var texAndFormat in CompressDict)
            {
                EditorUtility.CompressTexture(texAndFormat.Key, texAndFormat.Value, 50);
            }
        }
    }
}
#endif