﻿#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using net.rs64.MultiLayerImageParser.PSD;
using net.rs64.MultiLayerImageParser.LayerData;
using UnityEditor;
using UnityEditor.Experimental;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using System.Linq;
using net.rs64.TexTransTool.ReferenceResolver.MLIResolver;
namespace net.rs64.TexTransTool.MultiLayerImage.Importer
{
    internal class TexTransToolPSDImporter
    {
        [MenuItem("Assets/TexTransTool/TTT PSD Importer", false)]
        public static void ImportPSD()
        {

            if (!EditorUtility.DisplayDialog(
                "TexTransTool PSD Importer",
@"PSDインポーターは、実験的機能で予告なく変更や削除される可能性があり、
PSDのインポートは非常に長い時間がかかる可能性があります。

本当にインポートしますか？".GetLocalize(),
                 "する".GetLocalize(), "しない".GetLocalize())) { return; }

            foreach (var select in Selection.objects)
            {
                var souseTex2D = select as Texture2D;
                if (souseTex2D == null) { continue; }
                var targetPSDPath = AssetDatabase.GetAssetPath(souseTex2D);
                if (string.IsNullOrWhiteSpace(targetPSDPath)) { continue; }
                if (Path.GetExtension(targetPSDPath) != ".psd") { continue; }

                EditorUtility.DisplayProgressBar("Parse PSD", "LowLevelParser", 0);
                var lowPSDData = PSDLowLevelParser.Parse(targetPSDPath);
                EditorUtility.DisplayProgressBar("Parse PSD", "HighLevelParser", 0.5f);
                var pSDData = PSDHighLevelParser.Parse(lowPSDData);
                EditorUtility.DisplayProgressBar("Parse PSD", "End", 1);


                MultiLayerImageImporter.ImportCanvasData(
                    new MultiLayerImageImporter.HandlerForFolderSaver(targetPSDPath.Replace(".psd", "")), (CanvasData)pSDData,
                    multiLayerImageCanvas => multiLayerImageCanvas.gameObject.AddComponent<AbsoluteTextureResolver>().Texture = souseTex2D
                    );


                EditorUtility.ClearProgressBar();
            }
        }


    }
}
#endif