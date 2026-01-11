#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace net.rs64.TexTransTool
{
    public sealed class MaterialModifierAdditionalTarget : TexTransAnnotation
    {
        internal const string ComponentName = "TTT MaterialModifier Additional Target";
        internal const string MenuPath = TextureBlender.FoldoutName + "/" + ComponentName;

        [AffectVRAM] public bool AllMaterials = false;
        [AffectVRAM] public List<Material?> AdditionalTargets = new();

        internal IEnumerable<Material> GetAdditionalTargets(IDomainReferenceViewer referenceViewer)
        {
            if (referenceViewer.ObserveToGet(this, i => i.AllMaterials))
            {
                return referenceViewer.GetAllMaterials();
            }
            return referenceViewer.ObserveToGet(this, i => i.AdditionalTargets).SkipDestroyed();
        }
    }
}
