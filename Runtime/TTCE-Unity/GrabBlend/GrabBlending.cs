using System;
using System.Collections.Generic;
using System.Linq;
using net.rs64.TexTransCore;
using net.rs64.TexTransCoreEngineForUnity;
using net.rs64.TexTransCoreEngineForUnity.Utils;
using UnityEngine;
namespace net.rs64.TexTransCoreEngineForUnity
{
    internal static class GrabBlending
    {
        public static Dictionary<string, TTGrabBlendingUnityObject> GrabBlendObjects;
        public static Dictionary<Type, IGrabBlendingExecuter> GrabBlendingExecuters;
        [TexTransInitialize]
        public static void GrabBlendingInit()
        {
            GrabBlendObjects = TexTransCoreRuntime.LoadAssetsAtType(typeof(TTGrabBlendingUnityObject)).Cast<TTGrabBlendingUnityObject>().ToDictionary(i => i.name, i => i);
            GrabBlendingExecuters = InterfaceUtility.GetInterfaceInstance<IGrabBlendingExecuter>().ToDictionary(i => i.ExecutionTarget, i => i);
        }

        //HSLAdjustment
        //LevelAdjustment
        //SelectiveColorAdjustment
        //LuminanceMapping
        //Colorize

    }

    internal interface IGrabBlendingExecuter
    {
        Type ExecutionTarget { get; }

        void GrabExecute(TTCoreEngineForUnity engin, RenderTexture rt, TTGrabBlending grabBlending);
    }
}