#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using net.rs64.TexTransCore;
using net.rs64.TexTransCoreEngineForUnity;
using net.rs64.TexTransTool.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Debug = UnityEngine.Debug;

namespace net.rs64.TexTransTool.NDMF
{
    internal class NodeExecuteDomain : IDomain, IDisposable
    {
        HashSet<UnityEngine.Object> _transferredObject = new();
        HashSet<ITTRenderTexture> _transferredRenderTextures = new();
        // HashSet<RenderTexture> _neededReleaseTempRt = new();
        protected readonly NDMFPreviewStackManager _textureStacks;
        // protected readonly ITextureManager _textureManager;
        private UnityDiskUtil _diskUtil;

        ComputeContext _ctx;
        private TexTransDomainFilter.NDMFGameObjectObservedWaker _NDMFObservedWaker;
        List<Renderer> _proxyDomainRenderers;
        Dictionary<Renderer, Renderer> _proxy2OriginRendererDict;
        readonly HashSet<UnityEngine.Object> _proxyObjects = new();
        readonly HashSet<UnityEngine.Object> _observedObjects = new();

        Dictionary<Renderer, Action<Renderer>> _rendererApplyRecaller = new();//origin 2 apply call
        private IObjectRegistry _objectRegistry;
        private TTCEUnityWithTTT4UnityOnNDMFPreview _ttce4U;

        public bool UsedTextureStack { get; private set; } = false;
        public bool UsedMaterialReplace { get; private set; } = false;
        public bool UsedSetMesh { get; private set; } = false;
        public RenderAspects Reads { get; private set; } = 0;


        public NodeExecuteDomain(Dictionary<Renderer, Renderer> o2pDict, ComputeContext computeContext, IObjectRegistry objectRegistry)
        {
            _proxy2OriginRendererDict = o2pDict.ToDictionary(i => i.Value, i => i.Key);
            _proxyDomainRenderers = o2pDict.Values.ToList();
            foreach (var proxy in _proxyDomainRenderers)
            {
                _proxyObjects.Add(proxy);
                _proxyObjects.Add(proxy.gameObject);
                _proxyObjects.Add(proxy.transform);
                if (proxy.TryGetComponent<MeshFilter>(out var meshFilter)) { _proxyObjects.Add(meshFilter); }
            }
            _diskUtil = new UnityDiskUtil(true);
            _ttce4U = new TTCEUnityWithTTT4UnityOnNDMFPreview(_diskUtil);
            _textureStacks = new(_ttce4U);
            _ctx = computeContext;
            _NDMFObservedWaker = new TexTransDomainFilter.NDMFGameObjectObservedWaker(_ctx);
            _objectRegistry = objectRegistry;
        }

        public void Observe(UnityEngine.Object obj)
        {
            if (obj == null || IsTemporaryAsset(obj)) { return; }
            if (obj is RenderTexture && _transferredRenderTextures.Any(rt => _ttce4U.GetReferenceRenderTexture(rt) == obj)) { return; }

            // 任意の読み取りを伴うObserveでは、proxyのどの値を使うか特定できない。
            if (_proxyObjects.Contains(obj))
            {
                Reads |= RenderAspects.Everything;
                return;
            }

            var aspects = obj switch
            {
                Material _ => RenderAspects.Material | RenderAspects.Texture,
                Texture _ => RenderAspects.Texture,
                Mesh _ => RenderAspects.Mesh,
                _ => (RenderAspects)0,
            };
            Reads |= aspects;

            // 上流の複製そのものは購読せず、編集元の変更と上流の変更通知を別々に追う。
            var original = _objectRegistry.GetReference(obj, false)?.Object;
            if (original != null && original != obj)
            {
                if (aspects == 0) { Reads |= RenderAspects.Everything; }
                obj = original;
            }

            if (_observedObjects.Add(obj)) { _ctx.Observe(obj); }
        }
        public TOut ObserveToGet<TObj, TOut>(TObj obj, Func<TObj, TOut> getAction, Func<TOut, TOut, bool>? comp = null)
                where TObj : UnityEngine.Object
        {
            // 値の抽出による監視には変更せず、Object単位の依存だけを登録する。
            Observe(obj);
            return getAction(obj);
        }
        public void ObserveToGetComponent<LookTargetComponent>(GameObject gameObject) where LookTargetComponent : Component
        {
            if (_proxyObjects.Contains(gameObject))
            {
                Reads |= RenderAspects.Everything;
                return;
            }
            _ctx.GetComponent<LookTargetComponent>(gameObject);
        }
        public void ObserveToChildeComponents<LookTargetComponent>(GameObject gameObject) where LookTargetComponent : Component
        {
            if (_proxyObjects.Contains(gameObject))
            {
                Reads |= RenderAspects.Everything;
                return;
            }
            _ctx.GetComponentsInChildren<LookTargetComponent>(gameObject, true);
        }

        public void AddTextureStack(Texture dist, TexTransCore.ITTRenderTexture addTex, TexTransCore.ITTBlendKey blendKey)
        {
            Observe(dist);
            Reads |= RenderAspects.Texture;
            _textureStacks.AddTextureStack(dist, addTex, blendKey);
            UsedTextureStack = true;
        }
        public IEnumerable<Renderer> EnumerateRenderers() { return _proxyDomainRenderers; }
        public Material?[] GetMaterials(Renderer renderer)
        {
            if (!_proxy2OriginRendererDict.ContainsKey(renderer)) { Observe(renderer); }
            Reads |= RenderAspects.Material | RenderAspects.Texture;
            var materials = renderer.sharedMaterials;
            foreach (var material in materials) { Observe(material); }
            return materials;
        }
        public Decal.MeshData GetMeshData(Renderer renderer)
        {
            Reads |= RenderAspects.Mesh | RenderAspects.Shapes;
            _ = GetMesh(renderer);
            return this.GetMeshDataDefault(renderer);
        }
        public Mesh? GetMesh(Renderer renderer)
        {
            if (!_proxy2OriginRendererDict.ContainsKey(renderer)) { Observe(renderer); }
            Reads |= RenderAspects.Mesh;
            var mesh = this.GetMeshDefault(renderer);
            Observe(mesh);
            return mesh;
        }
        public HashSet<Material> GetAllMaterials()
        {
            Reads |= RenderAspects.Material | RenderAspects.Texture;
            return this.GetAllMaterialsDefault();
        }
        public HashSet<Texture> GetAllTextures()
        {
            var textures = this.GetAllTexturesDefault();
            foreach (var texture in textures) { Observe(texture); }
            return textures;
        }
        public HashSet<Texture> GetMaterialTextures(Material mat)
        {
            Observe(mat);
            Reads |= RenderAspects.Material | RenderAspects.Texture;
            var textures = this.GetMaterialTexturesDefault(mat);
            foreach (var texture in textures) { Observe(texture); }
            return textures;
        }

        private void RegisterRecall(Renderer proxyRenderer, Action<Renderer> recall)
        {
            if (!_proxy2OriginRendererDict.ContainsKey(proxyRenderer)) { throw new InvalidOperationException($" {proxyRenderer.name} はプロキシーリストにないよ...?"); }

            if (_rendererApplyRecaller.ContainsKey(_proxy2OriginRendererDict[proxyRenderer])) { _rendererApplyRecaller[_proxy2OriginRendererDict[proxyRenderer]] += recall; }
            else { _rendererApplyRecaller[_proxy2OriginRendererDict[proxyRenderer]] = recall; }
        }
        public bool IsTemporaryAsset(UnityEngine.Object obj) { return _transferredObject.Contains(obj); }
        public void GetMutable(ref Material material)
        {
            if (IsTemporaryAsset(material)) { return; }
            var origin = material;
            Observe(origin);
            Reads |= RenderAspects.Material | RenderAspects.Texture;

            var mutableMat = NDMFPreviewMaterialPool.Get(origin);
            SaveAsset(mutableMat);
            RegisterReplacement(origin, mutableMat);
            ReplaceMaterials(new() { { origin, mutableMat } });

            mutableMat.name = origin.name + "(TTT GetMutable on NDMF Preview for pooled)";
            material = mutableMat;
        }
        public void ReplaceMaterials(Dictionary<Material, Material> mapping)
        {
            Reads |= RenderAspects.Material;
            foreach (var dr in _proxyDomainRenderers)
            {
                RegisterRecall(dr, i => RendererUtility.SwapMaterials(i, mapping));
                RendererUtility.SwapMaterials(dr, mapping);
            }
            UsedMaterialReplace = true;
        }

        public void SetMesh(Renderer renderer, Mesh mesh)
        {
            RegisterRecall(renderer, i => i.SetMesh(mesh));
            renderer.SetMesh(mesh);
            UsedSetMesh = true;
        }
        public void SetMaterials(Renderer renderer, Material[] materials)
        {
            RegisterRecall(renderer, i => i.sharedMaterials = materials);
            renderer.sharedMaterials = materials;
            UsedMaterialReplace = true;
        }
        public TexTransToolTextureDescriptor GetTextureDescriptor(Texture texture) { return new(); }

        public void RegisterTextureDescription(ITTRenderTexture rt, TexTransToolTextureDescriptor textureDescriptor)
        {
            _transferredRenderTextures.Add(rt);

            if (textureDescriptor.AsLinear)
                TTCEUnityWithTTT4UnityOnNDMFPreview.s_AsLinearMarked.Add(_ttce4U.GetReferenceRenderTexture(rt));
        }

        public void RegisterReplacement(UnityEngine.Object oldObject, UnityEngine.Object nowObject)
        {
            _objectRegistry.RegisterReplacedObject(oldObject, nowObject);
        }
        public bool OriginalObjectEquals(UnityEngine.Object? l, UnityEngine.Object? r)
        {
            if (l == r) { return true; }
            if (l is Renderer lRen && r is Renderer rRen)
            {
                var originLRen = _proxy2OriginRendererDict.TryGetValue(lRen, out var oLRen) ? oLRen : lRen;
                var originRRen = _proxy2OriginRendererDict.TryGetValue(rRen, out var oRRen) ? oRRen : rRen;
                if (originLRen == originRRen) { return true; }
            }
            return _objectRegistry.GetReference(l).Equals(_objectRegistry.GetReference(r));
        }

        public void SaveAsset(UnityEngine.Object asset) { _transferredObject.Add(asset); }
        public bool IsActive(GameObject gameObject)
        {
            if (_proxyObjects.Contains(gameObject))
            {
                Reads |= RenderAspects.Everything;
                return TexTransBehaviorSearch.CheckIsActive(gameObject);
            }
            return TexTransBehaviorSearch.CheckIsActive(gameObject, _NDMFObservedWaker, null);
        }
        public bool IsEnable(Component component)
        {
            if (_proxyObjects.Contains(component))
            {
                Reads |= RenderAspects.Everything;
                return this.IsEnableDefault(component);
            }
            return component switch
            {
                Behaviour bh => _ctx.Observe(bh, b => b.enabled),
                Renderer bh => _ctx.Observe(bh, b => b.enabled),
                _ => this.IsEnableDefault(component),
            };
        }

        private void MargeStack()
        {
            foreach (var mergeResult in _textureStacks.StackDict)
            {
                if (mergeResult.Key == null || mergeResult.Value == null) continue;
                this.ReplaceTexture(mergeResult.Key, _ttce4U.GetReferenceRenderTexture(mergeResult.Value));
                _transferredRenderTextures.Add(mergeResult.Value);

                if (mergeResult.Key is Texture2D || (mergeResult.Key is RenderTexture drt && TTRt2.Contains(drt) is false))
                    if (GraphicsFormatUtility.IsSRGBFormat(mergeResult.Key.graphicsFormat) is false)
                        TTCEUnityWithTTT4UnityOnNDMFPreview.s_AsLinearMarked.Add(_ttce4U.GetReferenceRenderTexture(mergeResult.Value));
            }
        }
        public void DomainFinish()
        {
            MargeStack();
            foreach (var tfRT in _transferredRenderTextures)
                if (TTCEUnityWithTTT4UnityOnNDMFPreview.s_AsLinearMarked.Contains(_ttce4U.GetReferenceRenderTexture(tfRT)) is false)
                    _ttce4U.GammaToLinear(tfRT);
        }


        public void Dispose()
        {
            foreach (var obj in _transferredObject)
            {
                if (obj is Material mat) { _ = NDMFPreviewMaterialPool.Ret(mat); continue; }
                UnityEngine.Object.DestroyImmediate(obj, true);
            }
            foreach (var rt in _transferredRenderTextures)
            {
                TTCEUnityWithTTT4UnityOnNDMFPreview.s_AsLinearMarked.Remove(_ttce4U.GetReferenceRenderTexture(rt));
                rt.Dispose();
            }
            _transferredRenderTextures.Clear();
            _transferredObject.Clear();
            _observedObjects.Clear();
            _textureStacks.Dispose();

            _ctx = null!;
        }

        internal void DomainRecaller(Renderer original, Renderer proxy)
        {
            if (_rendererApplyRecaller.Any() is false) { return; }
            if (!_rendererApplyRecaller.ContainsKey(original))
            {
#if TTT_DISPLAY_RUNTIME_LOG
                Debug.Log($"{original.name} is can not Recall");
#endif
                return;
            }

            _rendererApplyRecaller[original].Invoke(proxy);
        }
        public ITexTransToolForUnity GetTexTransCoreEngineForUnity() => _ttce4U;

        DomainPreviewCtx DomainPreviewCtx = new(true);

        T? IDomainCustomContext.GetCustomContext<T>() where T : class
        {
            if (DomainPreviewCtx is T dpc) { return dpc; }
            return null;
        }

    }

    class NDMFPreviewStackManager : IDisposable
    {
        TTCEUnityWithTTT4UnityOnNDMFPreview _ttce4u;
        Dictionary<Texture, ITTRenderTexture> _stackDict = new();
        public IReadOnlyDictionary<Texture, ITTRenderTexture> StackDict => _stackDict;
        public NDMFPreviewStackManager(TTCEUnityWithTTT4UnityOnNDMFPreview ttce4u)
        {
            _ttce4u = ttce4u;
        }

        public void AddTextureStack(Texture dist, ITTRenderTexture addTex, ITTBlendKey blendKey)
        {
            if (_stackDict.ContainsKey(dist) is false)
            {
                var stackTexture = _ttce4u.CreateRenderTexture(dist.width, dist.height);
                stackTexture.Name = $"{dist.name}:StackTexture-{dist.width}x{dist.height}";
                _ttce4u.WrappingOrUploadToLoad(stackTexture, dist);
                _ttce4u.GetReferenceRenderTexture(stackTexture).CopyFilWrap(dist);

                _stackDict.Add(dist, stackTexture);
            }
            _ttce4u.BlendingWithAnySize(_stackDict[dist], addTex, blendKey);
        }

        public void Dispose()
        {
            foreach (var rt in _stackDict) { rt.Value.Dispose(); }
            _stackDict.Clear();
        }
    }

    class TTCEUnityWithTTT4UnityOnNDMFPreview : TTCEUnityWithTTT4Unity
    {
        // あまり嬉しくない ... だがそれ以外の良い手段が思いつかなかったがゆえ
        internal static HashSet<RenderTexture> s_AsLinearMarked = new();
        public TTCEUnityWithTTT4UnityOnNDMFPreview(ITexTransUnityDiskUtil diskUtil) : base(diskUtil) { }

        public override ITTRenderTexture UploadTexture(RenderTexture renderTexture)
        {
            var newRt = base.UploadTexture(renderTexture);
            if (TTRt2.Contains(renderTexture) && s_AsLinearMarked.Contains(renderTexture) is false)
                this.LinearToGamma(newRt);
            return newRt;
        }
    }
}
