#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using net.rs64.TexTransCore;
using net.rs64.TexTransTool.TTMathUtil;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Color = net.rs64.TexTransCore.Color;

namespace net.rs64.TexTransCoreEngineForUnity
{
    internal class TTCEUnity : ITexTransCreateTexture
    , ITexTransCopyRenderTexture
    , ITexTransComputeKeyQuery
    , ITexTransGetComputeHandler
    , ITexTransDriveStorageBufferHolder
    {
        public ITTRenderTexture CreateRenderTexture(int width, int height, TexTransCoreTextureChannel channel = TexTransCoreTextureChannel.RGBA)
        {
            return new UnityRenderTexture(width, height, channel);
        }

        public void CopyRenderTexture(ITTRenderTexture target, ITTRenderTexture source)
        {
            if (source.Width != target.Width || source.Hight != target.Hight) { throw new ArgumentException("Texture size is not equal!"); }
            if (target.ContainsChannel != source.ContainsChannel) { throw new ArgumentException("Texture channel is not equal!"); }
            Graphics.CopyTexture(source.Unwrap(), target.Unwrap());
        }



        public ITTComputeHandler GetComputeHandler(ITTComputeKey computeKey)
        {
            switch (computeKey)
            {
                case TTGeneralComputeOperator generalComputeOperator: { return new TTUnityComputeHandler(generalComputeOperator.Compute); }
                case TTBlendingComputeShader blendingComputeShader: { return new TTUnityComputeHandler(blendingComputeShader.Compute); }
                case TTGrabBlendingComputeShader grabBlendingComputeShader: { return new TTUnityComputeHandler(grabBlendingComputeShader.Compute); }
                case ComputeKeyHolder holder: { return new TTUnityComputeHandler(holder.ComputeShader); }
            }
            throw new ArgumentException();
        }
        public ITTStorageBuffer AllocateStorageBuffer(int length, bool downloadable = false)
        { return new TTUnityComputeHandler.TTUnityStorageBuffer(length, downloadable); }
        public ITTStorageBuffer UploadStorageBuffer<T>(ReadOnlySpan<T> data, bool downloadable = false) where T : unmanaged
        {
            var length = data.Length * UnsafeUtility.SizeOf<T>();
            var paddedLength = TTMath.NormalizeOf4Multiple(length);
            var holder = new TTUnityComputeHandler.TTUnityStorageBuffer(paddedLength, downloadable);
#if UNITY_EDITOR
            if (paddedLength == length)
            {
                unsafe
                {
                    fixed (T* ptr = data)
                    {
                        var na = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(ptr, length, Allocator.None);
                        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref na, AtomicSafetyHandle.GetTempMemoryHandle());
                        holder._buffer!.SetData(na);
                    }
                }
            }
            else
#endif
            {
                using var na = new NativeArray<byte>(paddedLength, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                MemoryMarshal.Cast<T, byte>(data).CopyTo(na.AsSpan());
                na.AsSpan()[paddedLength..].Fill(0);
                holder._buffer!.SetData(na);
            }

            return holder;
        }

        public void DownloadBuffer<T>(Span<T> dist, ITTStorageBuffer takeToFrom) where T : unmanaged
        {
            var holder = (TTUnityComputeHandler.TTUnityStorageBuffer)takeToFrom;

            if (holder._buffer is null) { throw new NullReferenceException(); }
            if (holder._downloadable is false) { throw new InvalidOperationException(); }

            var length = dist.Length * UnsafeUtility.SizeOf<T>();
            var bufLen = holder._buffer.count * holder._buffer.stride;

#if UNITY_EDITOR
            if (length == bufLen)
            {
                unsafe
                {
                    fixed (T* ptr = dist)
                    {
                        var na = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(ptr, length, Allocator.None);
                        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref na, AtomicSafetyHandle.GetTempMemoryHandle());
                        var request = AsyncGPUReadback.RequestIntoNativeArray(ref na, holder._buffer);
                        request.WaitForCompletion();
                    }
                }
            }
            else
#endif
            {
                var req = AsyncGPUReadback.Request(holder._buffer);
                req.WaitForCompletion();
                req.GetData<T>().AsSpan().Slice(0, Math.Min(bufLen, dist.Length)).CopyTo(dist);
            }

            holder._buffer = null;
        }

        public static bool IsLinerRenderTexture = false;//基本的にガンマだ


        public ITexTransStandardComputeKey StandardComputeKey => ComputeObjectUtility.UStdHolder;
        public ITexTransTransTextureComputeKey TransTextureComputeKey => ComputeObjectUtility.UStdHolder;
        public ITexTransComputeKeyDictionary<string> GrabBlend { get; } = new GrabBlendQuery();
        public ITexTransComputeKeyDictionary<ITTBlendKey> BlendKey { get; } = new BlendKeyUnWrapper();
        public ITexTransComputeKeyDictionary<string> GenealCompute { get; } = new GenealComputeQuery();
        public IKeyValueStore<string, ITTSamplerKey> SamplerKey { get; } = new SamplerKeyQuery();
        public ITexTransComputeKeyDictionary<ITTSamplerKey> ResizingSamplerKey { get; } = new SamplerKeyToResizing();
        public ITexTransComputeKeyDictionary<ITTSamplerKey> TransSamplerKey { get; } = new SamplerKeyToTransSampler();

        class BlendKeyUnWrapper : ITexTransComputeKeyDictionary<ITTBlendKey> { public ITTComputeKey this[ITTBlendKey key] => key.Unwrap(); }
        class GrabBlendQuery : ITexTransComputeKeyDictionary<string> { public ITTComputeKey this[string key] => ComputeObjectUtility.GrabBlendObjects[key]; }
        class GenealComputeQuery : ITexTransComputeKeyDictionary<string> { public ITTComputeKey this[string key] => ComputeObjectUtility.GeneralComputeObjects[key]; }
        class SamplerKeyQuery : IKeyValueStore<string, ITTSamplerKey> { public ITTSamplerKey this[string key] => ComputeObjectUtility.SamplerComputeShaders[key]; }
        class SamplerKeyToResizing : ITexTransComputeKeyDictionary<ITTSamplerKey> { public ITTComputeKey this[ITTSamplerKey key] => ((TTSamplerComputeShader)key).GetResizingComputeKey; }
        class SamplerKeyToTransSampler : ITexTransComputeKeyDictionary<ITTSamplerKey> { public ITTComputeKey this[ITTSamplerKey key] => ((TTSamplerComputeShader)key).GetTransSamplerComputeKey; }
    }


    internal class UnityRenderTexture : ITTRenderTexture
    {
        internal RenderTexture RenderTexture;
        internal TexTransCoreTextureChannel Channel;
        public UnityRenderTexture(int width, int height, TexTransCoreTextureChannel channel = TexTransCoreTextureChannel.RGBA)
        {
            Channel = channel;
            RenderTexture = TTRt2.Get(width, height, channel);
        }
        public bool IsDepthAndStencil => RenderTexture.depth != 0;

        public int Width => RenderTexture.width;

        public int Hight => RenderTexture.height;

        public string Name { get => RenderTexture.name; set => RenderTexture.name = value; }

        public TexTransCoreTextureChannel ContainsChannel => Channel;

        public void Dispose() { TTRt2.Rel(RenderTexture); }
    }
    internal static class TTCoreEnginTypeUtil
    {
        public static UnityEngine.Color ToUnity(this Color color) { return new(color.R, color.G, color.B, color.A); }
        public static Color ToTTCore(this UnityEngine.Color color) { return new(color.r, color.g, color.b, color.a); }
        public static UnityEngine.Color ToUnity(this ColorWOAlpha color, float alpha = 1f) { return new(color.R, color.G, color.B, alpha); }
        public static UnityEngine.Vector2 ToUnity(this System.Numerics.Vector2 vec) { return new(vec.X, vec.Y); }
        public static UnityEngine.Vector3 ToUnity(this System.Numerics.Vector3 vec) { return new(vec.X, vec.Y, vec.Z); }
        public static UnityEngine.Vector4 ToUnity(this System.Numerics.Vector4 vec) { return new(vec.X, vec.Y, vec.Z, vec.W); }
        public static System.Numerics.Vector4 ToTTCore(this UnityEngine.Vector4 vec) { return new(vec.x, vec.y, vec.z, vec.w); }
        public static System.Numerics.Vector3 ToTTCore(this UnityEngine.Vector3 vec) { return new(vec.x, vec.y, vec.z); }
        public static RayIntersect.Ray ToTTCore(this UnityEngine.Ray ray) { return new() { Position = ray.origin.ToTTCore(), Direction = ray.direction.ToTTCore() }; }

        public static RenderTexture Unwrap(this ITTRenderTexture renderTexture) { return ((UnityRenderTexture)renderTexture).RenderTexture; }
        public static TTBlendingComputeShader Unwrap(this ITTBlendKey key) { return (TTBlendingComputeShader)key; }
        public static TTBlendingComputeShader Wrapping(this string key) { return ComputeObjectUtility.BlendingObject[key]; }
        public static float[] ToArray(this System.Numerics.Vector4 vec) { return new[] { vec.X, vec.Y, vec.Z, vec.W }; }
        public static float[] ToArray(this System.Numerics.Vector3 vec) { return new[] { vec.X, vec.Y, vec.Z }; }



        internal static TextureFormat ToUnityTextureFormat(this TexTransCoreTextureFormat format, TexTransCoreTextureChannel channel = TexTransCoreTextureChannel.RGBA)
        {
            switch (format, channel)
            {
                default: throw new ArgumentOutOfRangeException(format.ToString() + "-" + channel.ToString());
                case (TexTransCoreTextureFormat.Byte, TexTransCoreTextureChannel.R): return TextureFormat.R8;
                case (TexTransCoreTextureFormat.Byte, TexTransCoreTextureChannel.RG): return TextureFormat.RG16;
                case (TexTransCoreTextureFormat.Byte, TexTransCoreTextureChannel.RGBA): return TextureFormat.RGBA32;

                case (TexTransCoreTextureFormat.UShort, TexTransCoreTextureChannel.R): return TextureFormat.R16;
                case (TexTransCoreTextureFormat.UShort, TexTransCoreTextureChannel.RG): return TextureFormat.RG32;
                case (TexTransCoreTextureFormat.UShort, TexTransCoreTextureChannel.RGBA): return TextureFormat.RGBA64;

                case (TexTransCoreTextureFormat.Half, TexTransCoreTextureChannel.R): return TextureFormat.RHalf;
                case (TexTransCoreTextureFormat.Half, TexTransCoreTextureChannel.RG): return TextureFormat.RGHalf;
                case (TexTransCoreTextureFormat.Half, TexTransCoreTextureChannel.RGBA): return TextureFormat.RGBAHalf;

                case (TexTransCoreTextureFormat.Float, TexTransCoreTextureChannel.R): return TextureFormat.RFloat;
                case (TexTransCoreTextureFormat.Float, TexTransCoreTextureChannel.RG): return TextureFormat.RGFloat;
                case (TexTransCoreTextureFormat.Float, TexTransCoreTextureChannel.RGBA): return TextureFormat.RGBAFloat;
            }
        }
        internal static (TexTransCoreTextureFormat format, TexTransCoreTextureChannel channel) ToTTCTextureFormat(this TextureFormat format)
        {
            switch (format)
            {
                default: throw new ArgumentOutOfRangeException(format.ToString());
                case TextureFormat.R8: return (TexTransCoreTextureFormat.Byte, TexTransCoreTextureChannel.R);
                case TextureFormat.RG16: return (TexTransCoreTextureFormat.Byte, TexTransCoreTextureChannel.RG);
                case TextureFormat.RGBA32: return (TexTransCoreTextureFormat.Byte, TexTransCoreTextureChannel.RGBA);

                case TextureFormat.R16: return (TexTransCoreTextureFormat.UShort, TexTransCoreTextureChannel.R);
                case TextureFormat.RG32: return (TexTransCoreTextureFormat.UShort, TexTransCoreTextureChannel.RG);
                case TextureFormat.RGBA64: return (TexTransCoreTextureFormat.UShort, TexTransCoreTextureChannel.RGBA);

                case TextureFormat.RHalf: return (TexTransCoreTextureFormat.Half, TexTransCoreTextureChannel.R);
                case TextureFormat.RGHalf: return (TexTransCoreTextureFormat.Half, TexTransCoreTextureChannel.RG);
                case TextureFormat.RGBAHalf: return (TexTransCoreTextureFormat.Half, TexTransCoreTextureChannel.RGBA);

                case TextureFormat.RFloat: return (TexTransCoreTextureFormat.Float, TexTransCoreTextureChannel.R);
                case TextureFormat.RGFloat: return (TexTransCoreTextureFormat.Float, TexTransCoreTextureChannel.RG);
                case TextureFormat.RGBAFloat: return (TexTransCoreTextureFormat.Float, TexTransCoreTextureChannel.RGBA);
            }
        }
        internal static GraphicsFormat ToUnityGraphicsFormat(this TexTransCoreTextureFormat format, TexTransCoreTextureChannel channel = TexTransCoreTextureChannel.RGBA)
        {
            switch (format, channel)
            {
                default: throw new ArgumentOutOfRangeException(format.ToString() + "-" + channel.ToString());
                case (TexTransCoreTextureFormat.Byte, TexTransCoreTextureChannel.R): return GraphicsFormat.R8_UNorm;
                case (TexTransCoreTextureFormat.Byte, TexTransCoreTextureChannel.RG): return GraphicsFormat.R8G8_UNorm;
                case (TexTransCoreTextureFormat.Byte, TexTransCoreTextureChannel.RGBA): return GraphicsFormat.R8G8B8A8_UNorm;

                case (TexTransCoreTextureFormat.UShort, TexTransCoreTextureChannel.R): return GraphicsFormat.R16_UNorm;
                case (TexTransCoreTextureFormat.UShort, TexTransCoreTextureChannel.RG): return GraphicsFormat.R16G16_UNorm;
                case (TexTransCoreTextureFormat.UShort, TexTransCoreTextureChannel.RGBA): return GraphicsFormat.R16G16B16A16_UNorm;

                case (TexTransCoreTextureFormat.Half, TexTransCoreTextureChannel.R): return GraphicsFormat.R16_SFloat;
                case (TexTransCoreTextureFormat.Half, TexTransCoreTextureChannel.RG): return GraphicsFormat.R16G16_SFloat;
                case (TexTransCoreTextureFormat.Half, TexTransCoreTextureChannel.RGBA): return GraphicsFormat.R16G16B16A16_SFloat;

                case (TexTransCoreTextureFormat.Float, TexTransCoreTextureChannel.R): return GraphicsFormat.R32_SFloat;
                case (TexTransCoreTextureFormat.Float, TexTransCoreTextureChannel.RG): return GraphicsFormat.R32G32_SFloat;
                case (TexTransCoreTextureFormat.Float, TexTransCoreTextureChannel.RGBA): return GraphicsFormat.R32G32B32A32_SFloat;
            }
        }
        internal static (TexTransCoreTextureFormat format, TexTransCoreTextureChannel channel) ToTTCTextureFormat(this GraphicsFormat format)
        {
            switch (format)
            {
                default: throw new ArgumentOutOfRangeException(format.ToString());
                case GraphicsFormat.R8_UNorm: return (TexTransCoreTextureFormat.Byte, TexTransCoreTextureChannel.R);
                case GraphicsFormat.R8G8_UNorm: return (TexTransCoreTextureFormat.Byte, TexTransCoreTextureChannel.RG);
                case GraphicsFormat.R8G8B8A8_UNorm: return (TexTransCoreTextureFormat.Byte, TexTransCoreTextureChannel.RGBA);

                case GraphicsFormat.R16_UNorm: return (TexTransCoreTextureFormat.UShort, TexTransCoreTextureChannel.R);
                case GraphicsFormat.R16G16_UNorm: return (TexTransCoreTextureFormat.UShort, TexTransCoreTextureChannel.RG);
                case GraphicsFormat.R16G16B16A16_UNorm: return (TexTransCoreTextureFormat.UShort, TexTransCoreTextureChannel.RGBA);

                case GraphicsFormat.R16_SFloat: return (TexTransCoreTextureFormat.Half, TexTransCoreTextureChannel.R);
                case GraphicsFormat.R16G16_SFloat: return (TexTransCoreTextureFormat.Half, TexTransCoreTextureChannel.RG);
                case GraphicsFormat.R16G16B16A16_SFloat: return (TexTransCoreTextureFormat.Half, TexTransCoreTextureChannel.RGBA);

                case GraphicsFormat.R32_SFloat: return (TexTransCoreTextureFormat.Float, TexTransCoreTextureChannel.R);
                case GraphicsFormat.R32G32_SFloat: return (TexTransCoreTextureFormat.Float, TexTransCoreTextureChannel.RG);
                case GraphicsFormat.R32G32B32A32_SFloat: return (TexTransCoreTextureFormat.Float, TexTransCoreTextureChannel.RGBA);
            }
        }


    }
}