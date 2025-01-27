using nadena.dev.ndmf;
using net.rs64.TexTransTool.NDMF;
using net.rs64.TexTransTool.Build;
using UnityEngine;
using System.Collections.Generic;
using nadena.dev.ndmf.preview;
using System;
using System.Linq;
using net.rs64.TexTransCoreEngineForUnity;
#if CONTAINS_AAO
using net.rs64.TexTransTool.NDMF.AAO;
#endif

[assembly: ExportsPlugin(typeof(NDMFPlugin))]

namespace net.rs64.TexTransTool.NDMF
{

    internal class NDMFPlugin : Plugin<NDMFPlugin>
    {
        public override string QualifiedName => "net.rs64.tex-trans-tool";
        public override string DisplayName => "TexTransTool";
        public override Texture2D LogoTexture => TTTImageAssets.Logo;
        protected override void Configure()
        {
            InPhase(BuildPhase.Resolving)
            .Run(PreviewCancelerPass.Instance);


            InPhase(BuildPhase.Transforming)
            .BeforePlugin("io.github.azukimochi.light-limit-changer")
            .BeforePlugin("net.narazaka.vrchat.floor_adjuster")
            .BeforePlugin("MantisLODEditor.ndmf")
#if CONTAINS_AAO
            .Run(NegotiateAAOPass.Instance).Then
#endif

            .Run(MaterialModificationPass.Instance).PreviewingWith(new TexTransDomainFilter(TexTransPhase.MaterialModification)).Then
            .Run(BeforeUVModificationPass.Instance).PreviewingWith(new TexTransDomainFilter(TexTransPhase.BeforeUVModification)).Then
            .Run(UVModificationPass.Instance).PreviewingWith(new TexTransDomainFilter(TexTransPhase.UVModification)).Then
            .Run(AfterUVModificationPass.Instance).PreviewingWith(new TexTransDomainFilter(TexTransPhase.AfterUVModification)).Then
            .Run(PostProcessingPass.Instance).PreviewingWith(new TexTransDomainFilter(TexTransPhase.PostProcessing)).Then
            .Run(UnDefinedPass.Instance).PreviewingWith(new TexTransDomainFilter(TexTransPhase.UnDefined));


            InPhase(BuildPhase.Optimizing)
            .BeforePlugin("com.anatawa12.avatar-optimizer")
            .BeforePlugin("MantisLODEditor.ndmf")

            .Run(ReFindRenderersPass.Instance).Then

            .Run(OptimizingPass.Instance).Then
            .Run(TTTSessionEndPass.Instance).PreviewingWith(new TexTransDomainFilter(TexTransPhase.Optimizing)).Then

            .Run(TTTComponentPurgePass.Instance);
        }
        internal static Dictionary<TexTransPhase, TogglablePreviewNode> s_togglablePreviewPhases = new() {
            { TexTransPhase.MaterialModification,  TogglablePreviewNode.Create(() => "MaterialModification-Phase", "MaterialModification", true) },
            { TexTransPhase.BeforeUVModification,  TogglablePreviewNode.Create(() => "BeforeUVModification-Phase", "BeforeUVModification", true) },
            { TexTransPhase.UVModification,  TogglablePreviewNode.Create(() => "UVModification-Phase", "UVModification",  true) },
            { TexTransPhase.AfterUVModification,  TogglablePreviewNode.Create(() => "AfterUVModification-Phase", "AfterUVModification",  true) },
            { TexTransPhase.PostProcessing,  TogglablePreviewNode.Create(() => "PostProcessing-Phase", "AfterUVModification",  true) },
            { TexTransPhase.UnDefined,  TogglablePreviewNode.Create(() => "UnDefined-Phase", "UnDefined",  true) },
            { TexTransPhase.Optimizing,  TogglablePreviewNode.Create(() => "Optimizing-Phase", "Optimizing", false) },
        };
    }

}
