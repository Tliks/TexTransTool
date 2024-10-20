using net.rs64.PSDParser;
using net.rs64.TexTransCoreEngineForUnity;
using net.rs64.TexTransTool.MultiLayerImage;
using net.rs64.TexTransTool.PSDParser;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace net.rs64.TexTransTool.PSDImporter
{
    public class PSDImportedRasterMaskImage : TTTImportedImage
    {
        [SerializeField] public PSDImportedRasterMaskImageData MaskImageData;

        protected override Vector2Int Pivot => new Vector2Int(MaskImageData.RectTangle.Left, CanvasDescription.Height - MaskImageData.RectTangle.Bottom);

        protected override JobResult<NativeArray<Color32>> LoadImage(byte[] importSource, NativeArray<Color32>? writeTarget = null)
        {
            Profiler.BeginSample("Init");
            var native2DArray = writeTarget ?? new NativeArray<Color32>(CanvasDescription.Width * CanvasDescription.Height, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            TexTransCoreEngineForUnity.Unsafe.UnsafeNativeArrayUtility.ClearMemoryOnColor(native2DArray, MaskImageData.DefaultValue);

            var canvasSize = new int2(CanvasDescription.Width, CanvasDescription.Height);
            var sourceTexSize = new int2(MaskImageData.RectTangle.GetWidth(), MaskImageData.RectTangle.GetHeight());

            Profiler.EndSample();

            JobHandle offsetJobHandle;
            if ((MaskImageData.RectTangle.GetWidth() * MaskImageData.RectTangle.GetHeight()) == 0) { return new(native2DArray); }

            Profiler.BeginSample("RLE");

            var psdCanvasDesc = CanvasDescription as PSDImportedCanvasDescription;
            var data = new NativeArray<byte>(ChannelImageDataParser.ChannelImageData.GetImageByteCount(MaskImageData.RectTangle, psdCanvasDesc.BitDepth), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var buffer = new NativeArray<byte>((int)MaskImageData.MaskImage.ImageDataAddress.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            MaskImageData.MaskImage.GetImageData(importSource, MaskImageData.RectTangle, buffer, data);

            Profiler.EndSample();
            Profiler.BeginSample("OffsetMoveAlphaJobSetUp");

            var offset = new PSDImportedRasterImage.OffsetMoveAlphaJob()
            {
                Target = native2DArray,
                R = data,
                G = data,
                B = data,
                A = data,
                Offset = new int2(Pivot.x, Pivot.y),
                SourceSize = sourceTexSize,
                TargetSize = canvasSize,
            };
            offsetJobHandle = offset.Schedule(data.Length, 64);

            Profiler.EndSample();
            return new(native2DArray, offsetJobHandle, () => { data.Dispose(); });
        }
        protected override void LoadImage(byte[] importSource, RenderTexture WriteTarget)
        {
            var isZeroSize = (MaskImageData.RectTangle.GetWidth() * MaskImageData.RectTangle.GetHeight()) == 0;
            if (PSDImportedRasterImage.s_tempMat == null) { PSDImportedRasterImage.s_tempMat = new Material(PSDImportedRasterImage.MergeColorAndOffsetShader); }
            var mat = PSDImportedRasterImage.s_tempMat;

            var psdCanvasDesc = CanvasDescription as PSDImportedCanvasDescription;
            var format = PSDImportedRasterImage.BitDepthToTextureFormat(psdCanvasDesc.BitDepth);

            var texR = new Texture2D(MaskImageData.RectTangle.GetWidth(), MaskImageData.RectTangle.GetHeight(), format, false);
            texR.filterMode = FilterMode.Point;

            TextureBlend.FillColor(WriteTarget, new Color32(MaskImageData.DefaultValue, MaskImageData.DefaultValue, MaskImageData.DefaultValue, MaskImageData.DefaultValue));

            if (!isZeroSize)
            {
                using (var data = new NativeArray<byte>(ChannelImageDataParser.ChannelImageData.GetImageByteCount(MaskImageData.RectTangle, psdCanvasDesc.BitDepth), Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
                using (var buffer = new NativeArray<byte>((int)MaskImageData.MaskImage.ImageDataAddress.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
                {
                    MaskImageData.MaskImage.GetImageData(importSource, MaskImageData.RectTangle, buffer, data);
                    texR.LoadRawTextureData(data); texR.Apply();
                }

                mat.SetTexture("_RTex", texR);
                mat.SetTexture("_GTex", texR);
                mat.SetTexture("_BTex", texR);
                mat.SetTexture("_ATex", texR);

                mat.SetVector("_Offset", new Vector4(Pivot.x / (float)CanvasDescription.Width, Pivot.y / (float)CanvasDescription.Height, MaskImageData.RectTangle.GetWidth() / (float)CanvasDescription.Width, MaskImageData.RectTangle.GetHeight() / (float)CanvasDescription.Height));
                Graphics.Blit(null, WriteTarget, mat);
            }

            UnityEngine.Object.DestroyImmediate(texR);
        }
    }
}