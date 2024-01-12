

namespace net.rs64.TexTransTool
{
    internal static class TexTransBehaviorUtility
    {
        public static void Apply(this TexTransBehavior texTransBehavior, IEditorCallDomain domain)
        {
            switch (texTransBehavior)
            {
                case TexTransRuntimeBehavior texTransRuntime:
                    { texTransRuntime.Apply(domain); break; }
                    // case TexTransCallEditorBehavior texTransCallEditorBehavior:
                    // { texTransCallEditorBehavior.Apply(domain); break; }
            }
        }
    }
}