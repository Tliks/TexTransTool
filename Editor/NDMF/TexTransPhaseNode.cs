using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using net.rs64.TexTransTool.Build;
using UnityEngine;
using UnityEngine.Profiling;

namespace net.rs64.TexTransTool.NDMF
{
    internal class TexTransPhaseNode : IRenderFilterNode
    {
        public RenderAspects Reads => _nodeDomain.Reads;
        public RenderAspects WhatChanged { get; private set; }

        readonly NodeExecuteDomain _nodeDomain;
        readonly ComputeContext _selfContext;
        readonly ImmutableArray<TexTransBehavior> _orderedBehaviors;
        readonly TexTransPhase _targetPhase;
        readonly RenderAspects _writtenAspects;

        TexTransPhaseNode(NodeExecuteDomain nodeDomain, ComputeContext selfContext, ImmutableArray<TexTransBehavior> orderedBehaviors, TexTransPhase targetPhase)
        {
            _nodeDomain = nodeDomain;
            _selfContext = selfContext;
            _orderedBehaviors = orderedBehaviors;
            _targetPhase = targetPhase;

            if (nodeDomain.UsedMaterialReplace) { _writtenAspects |= RenderAspects.Material; }
            if (nodeDomain.UsedSetMesh) { _writtenAspects |= RenderAspects.Mesh; }
            if (nodeDomain.UsedTextureStack) { _writtenAspects |= RenderAspects.Material | RenderAspects.Texture; }
            WhatChanged = _writtenAspects;
        }

        internal static TexTransPhaseNode Create(ImmutableArray<TexTransBehavior> orderedBehaviors, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context, TexTransPhase targetPhase)
        {
            var selfContext = new ComputeContext("TexTransPhaseNode: " + targetPhase);
            NodeExecuteDomain domain = null;
            Profiler.BeginSample("TexTransPhaseNode.Create");
            try
            {
                var originalToProxy = proxyPairs.ToDictionary(pair => pair.Item1, pair => pair.Item2);
                domain = new NodeExecuteDomain(originalToProxy, selfContext, ObjectRegistry.ActiveRegistry);

                foreach (var behavior in orderedBehaviors)
                {
                    if (behavior == null) { continue; }
                    domain.Observe(behavior);

                    Profiler.BeginSample("apply-" + behavior.name);
                    try
                    {
                        behavior.Apply(domain);
                    }
                    finally
                    {
                        Profiler.EndSample();
                    }
                }

                domain.DomainFinish();

                var node = new TexTransPhaseNode(domain, selfContext, orderedBehaviors, targetPhase);
                selfContext.Invalidates(context);
                return node;
            }
            catch
            {
                selfContext.Invalidate();
                domain?.Dispose();
                throw;
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        public void OnFrame(Renderer original, Renderer proxy)
        {
            _nodeDomain.DomainRecaller(original, proxy);
        }

        public Task<IRenderFilterNode> Refresh(IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context, RenderAspects updatedAspects)
        {
            if (!_selfContext.IsInvalidated && (updatedAspects & Reads) == 0)
            {
                _selfContext.Invalidates(context);
                WhatChanged = 0;
                return Task.FromResult<IRenderFilterNode>(this);
            }

            var next = Create(_orderedBehaviors, proxyPairs, context, _targetPhase);
            // 前回の結果との差分なので、新旧どちらかが変更する種類を通知する。
            next.WhatChanged |= _writtenAspects;
            return Task.FromResult<IRenderFilterNode>(next);
        }

        void IDisposable.Dispose()
        {
            _selfContext.Invalidate();
            _nodeDomain.Dispose();
        }

        public override string ToString()
        {
            return base.ToString() + _targetPhase;
        }
    }
}
