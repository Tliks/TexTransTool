#nullable enable
using System;
using net.rs64.TexTransCore.MultiLayerImageCanvas;
using UnityEngine;

namespace net.rs64.TexTransTool.MultiLayerImage
{
    /// <summary>
    /// lilToon 風のシンプル HSV 調整を行うレイヤー。
    /// Gamma 処理は別コンポーネントに任せ、ここでは H/S/V のみを扱う。
    /// </summary>
    [AddComponentMenu(TexTransBehavior.TTTName + "/" + MenuPath)]
    public class HSVSimpleAdjustmentLayer : AbstractLayer
    {
        internal const string ComponentName = "TTT HSVSimpleAdjustmentLayer";
        internal const string MenuPath = MultiLayerImageCanvas.FoldoutName + "/" + ComponentName;

        [Range(-1, 1)] public float Hue;
        [Range(0, 2)] public float Saturation = 1f;
        [Range(0, 2)] public float Value = 1f;

        internal override LayerObject<ITexTransToolForUnity> GetLayerObject(GenerateLayerObjectContext ctx)
        {
            var domain = ctx.Domain;
            var engine = ctx.Engine;

            domain.Observe(this);
            domain.Observe(gameObject);

            var lm = GetAlphaMaskObject(ctx);
            var blKey = engine.QueryBlendKey(BlendTypeKey);

            var hsvsa = new HSVSimpleAdjustment(Hue, Saturation, Value);

            return new GrabBlendingAsLayer<ITexTransToolForUnity>(Visible, lm, Clipping, blKey, hsvsa);
        }
    }
}

