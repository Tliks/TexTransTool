#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using net.rs64.TexTransCore;
using net.rs64.TexTransTool.Decal;
using net.rs64.TexTransTool.MultiLayerImage;
using net.rs64.TexTransTool.Utils;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine;

namespace net.rs64.TexTransTool.Editor.MultiLayerImage
{
    internal sealed class MultiLayerImagePreviewHover : IDisposable
    {
        const float UpdateUVThreshold = 0.002f;

        readonly Dictionary<Renderer, HoverMeshData> _meshDataCache = new();
        readonly HashSet<Renderer> _meshDataFailedCache = new();
        readonly List<RendererMaterialSlot> _targetSlots = new();

        MultiLayerImageCanvas? _canvas;
        Texture2D? _targetTexture;
        Vector2? _lastUV;
        Vector3? _lastWorldPosition;

        public void Dispose()
        {
            ClearCache();
            MultiLayerImagePreviewScenePointer.Clear(this);
        }

        public void Update(Rect previewArea, Texture previewTexture, MultiLayerImageCanvas? canvas)
        {
            EnableMouseMoveEvent(previewArea);

            if (canvas == null || previewTexture == null)
            {
                Hide();
                return;
            }

            var drawRect = GetScaleToFitRect(previewArea, previewTexture.width, previewTexture.height);
            var mousePosition = Event.current.mousePosition;
            if (drawRect.Contains(mousePosition) is false)
            {
                Hide();
                return;
            }

            var uv = MousePositionToUV(drawRect, mousePosition);
            if (_lastUV.HasValue && Vector2.Distance(_lastUV.Value, uv) < UpdateUVThreshold && _lastWorldPosition.HasValue)
            {
                MultiLayerImagePreviewScenePointer.Set(this, _lastWorldPosition.Value);
                return;
            }

            EnsureCache(canvas);
            if (TryResolveWorldPosition(uv, out var worldPosition))
            {
                _lastUV = uv;
                _lastWorldPosition = worldPosition;
                MultiLayerImagePreviewScenePointer.Set(this, worldPosition);
                SceneView.RepaintAll();
            }
            else
            {
                _lastUV = uv;
                _lastWorldPosition = null;
                MultiLayerImagePreviewScenePointer.Clear(this);
            }
        }

        static void EnableMouseMoveEvent(Rect previewArea)
        {
            var mouseOverWindow = EditorWindow.mouseOverWindow;
            if (mouseOverWindow == null || previewArea.Contains(Event.current.mousePosition) is false) { return; }

            mouseOverWindow.wantsMouseMove = true;
        }

        void Hide()
        {
            _lastUV = null;
            _lastWorldPosition = null;
            MultiLayerImagePreviewScenePointer.Clear(this);
        }

        void EnsureCache(MultiLayerImageCanvas canvas)
        {
            var targetTexture = canvas.TargetTexture.SelectTexture;
            if (_canvas == canvas && _targetTexture == targetTexture) { return; }

            ClearCache();
            _canvas = canvas;
            _targetTexture = targetTexture;

            if (targetTexture == null) { return; }

            foreach (var renderer in EnumerateSceneRenderers(canvas))
            {
                var materials = renderer.sharedMaterials;
                for (var materialSlot = 0; materialSlot < materials.Length; materialSlot += 1)
                {
                    var material = materials[materialSlot];
                    if (material == null || material.ReferencesTexture(targetTexture) is false) { continue; }
                    _targetSlots.Add(new(renderer, materialSlot));
                }
            }
        }

        void ClearCache()
        {
            _meshDataCache.Clear();
            _meshDataFailedCache.Clear();
            _targetSlots.Clear();
            _canvas = null;
            _targetTexture = null;
            _lastUV = null;
            _lastWorldPosition = null;
        }

        bool TryResolveWorldPosition(Vector2 uv, out Vector3 worldPosition)
        {
            var nearestDistance = float.PositiveInfinity;
            Vector3 nearestPosition = default;

            foreach (var targetSlot in _targetSlots)
            {
                if (targetSlot.Renderer == null) { continue; }
                if (TryGetMeshData(targetSlot.Renderer, out var meshData) is false) { continue; }
                if (targetSlot.MaterialSlot >= meshData.SubMeshCount) { continue; }

                var vertices = meshData.Vertices;
                var vertexUV = meshData.VertexUV;
                var triangles = meshData.TriangleVertexIndices[targetSlot.MaterialSlot];

                for (var triangleIndex = 0; triangleIndex < triangles.Length; triangleIndex += 1)
                {
                    var triangle = triangles[triangleIndex];
                    var uv0 = vertexUV[triangle.zero];
                    var uv1 = vertexUV[triangle.one];
                    var uv2 = vertexUV[triangle.two];

                    if (TryGetBarycentric(uv, uv0, uv1, uv2, out var barycentric))
                    {
                        worldPosition = vertices[triangle.zero] * barycentric.x + vertices[triangle.one] * barycentric.y + vertices[triangle.two] * barycentric.z;
                        return true;
                    }

                    var centerUV = (uv0 + uv1 + uv2) / 3f;
                    var distance = (centerUV - uv).sqrMagnitude;
                    if (distance >= nearestDistance) { continue; }

                    nearestDistance = distance;
                    nearestPosition = (vertices[triangle.zero] + vertices[triangle.one] + vertices[triangle.two]) / 3f;
                }
            }

            if (float.IsPositiveInfinity(nearestDistance))
            {
                worldPosition = default;
                return false;
            }

            worldPosition = nearestPosition;
            return true;
        }

        bool TryGetMeshData(Renderer renderer, out HoverMeshData meshData)
        {
            if (_meshDataCache.TryGetValue(renderer, out meshData)) { return true; }
            if (_meshDataFailedCache.Contains(renderer))
            {
                meshData = null!;
                return false;
            }

            try
            {
                using var sourceMeshData = MeshData.GetMeshData(renderer);
                meshData = new HoverMeshData(sourceMeshData);
                _meshDataCache.Add(renderer, meshData);
                return true;
            }
            catch (Exception)
            {
                _meshDataFailedCache.Add(renderer);
                meshData = null!;
                return false;
            }
        }

        static IEnumerable<Renderer> EnumerateSceneRenderers(MultiLayerImageCanvas canvas)
        {
            var domainRoot = DomainMarkerFinder.FindMarker(canvas.gameObject);
            var root = domainRoot != null ? domainRoot.transform : canvas.transform.root;
            return root.GetComponentsInChildren<Renderer>(true);
        }

        static Rect GetScaleToFitRect(Rect area, int textureWidth, int textureHeight)
        {
            if (textureWidth <= 0 || textureHeight <= 0) { return area; }

            var textureAspect = textureWidth / (float)textureHeight;
            var areaAspect = area.width / area.height;
            if (areaAspect > textureAspect)
            {
                var width = area.height * textureAspect;
                return new Rect(area.x + (area.width - width) * 0.5f, area.y, width, area.height);
            }
            else
            {
                var height = area.width / textureAspect;
                return new Rect(area.x, area.y + (area.height - height) * 0.5f, area.width, height);
            }
        }

        static Vector2 MousePositionToUV(Rect drawRect, Vector2 mousePosition)
        {
            return new(
                Mathf.InverseLerp(drawRect.xMin, drawRect.xMax, mousePosition.x),
                1f - Mathf.InverseLerp(drawRect.yMin, drawRect.yMax, mousePosition.y)
            );
        }

        static bool TryGetBarycentric(Vector2 p, Vector2 a, Vector2 b, Vector2 c, out Vector3 barycentric)
        {
            var v0 = b - a;
            var v1 = c - a;
            var v2 = p - a;
            var d00 = Vector2.Dot(v0, v0);
            var d01 = Vector2.Dot(v0, v1);
            var d11 = Vector2.Dot(v1, v1);
            var d20 = Vector2.Dot(v2, v0);
            var d21 = Vector2.Dot(v2, v1);
            var denom = d00 * d11 - d01 * d01;
            if (Mathf.Abs(denom) < Mathf.Epsilon)
            {
                barycentric = default;
                return false;
            }

            var v = (d11 * d20 - d01 * d21) / denom;
            var w = (d00 * d21 - d01 * d20) / denom;
            var u = 1f - v - w;
            barycentric = new(u, v, w);
            return u >= 0f && v >= 0f && w >= 0f;
        }

        readonly struct RendererMaterialSlot
        {
            public readonly Renderer Renderer;
            public readonly int MaterialSlot;

            public RendererMaterialSlot(Renderer renderer, int materialSlot)
            {
                Renderer = renderer;
                MaterialSlot = materialSlot;
            }
        }

        sealed class HoverMeshData
        {
            public readonly Vector3[] Vertices;
            public readonly Vector2[] VertexUV;
            public readonly TriangleVertexIndices[][] TriangleVertexIndices;
            public readonly int SubMeshCount;

            public HoverMeshData(MeshData source)
            {
                Vertices = source.Vertices.ToArray();
                VertexUV = source.VertexUV.ToArray();
                SubMeshCount = source.SubMeshCount;
                TriangleVertexIndices = new TriangleVertexIndices[SubMeshCount][];
                for (var subMeshIndex = 0; subMeshIndex < SubMeshCount; subMeshIndex += 1)
                {
                    TriangleVertexIndices[subMeshIndex] = source.TriangleVertexIndices[subMeshIndex].ToArray();
                }
            }
        }
    }

    [InitializeOnLoad]
    internal static class MultiLayerImagePreviewScenePointer
    {
        static readonly UnityEngine.Color PointerColor = new(1f, 0.75f, 0.1f, 1f);

        static object? s_owner;
        static Vector3? s_position;

        static MultiLayerImagePreviewScenePointer()
        {
            SceneView.duringSceneGui += DuringSceneGui;
        }

        public static void Set(object owner, Vector3 position)
        {
            s_owner = owner;
            s_position = position;
        }

        public static void Clear(object owner)
        {
            if (s_owner != owner) { return; }
            s_owner = null;
            s_position = null;
            SceneView.RepaintAll();
        }

        static void DuringSceneGui(SceneView sceneView)
        {
            if (s_position.HasValue is false || sceneView.camera == null) { return; }

            var position = s_position.Value;
            var handleSize = HandleUtility.GetHandleSize(position);
            using (new Handles.DrawingScope(PointerColor))
            {
                Handles.zTest = CompareFunction.Always;
                Handles.SphereHandleCap(0, position, Quaternion.identity, handleSize * 0.12f, EventType.Repaint);
                Handles.DrawWireDisc(position, sceneView.camera.transform.forward, handleSize * 0.24f);
            }
        }
    }
}
