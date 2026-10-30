using Colossal.UI.Binding;

namespace ZoneBuilder.UI
{
    // IJsonWritable with a hand-written camelCase Write() - same reasoning
    // as ZoneBuildProgressEntry/CollectionLevel1WarningEntry's own comments.
    // This type used to be a bare struct with public PascalCase properties,
    // relying on GenericUIWriter<T>'s reflection fallback (ExtendedUISystemBase.
    // CreateBinding<T>(key, Func<T>) always uses that writer) - which writes
    // property names VERBATIM, so the client actually received "Active"/
    // "Message" while ShellFeature.tsx's UIErrorEntry interface (and every
    // consumer) reads lowercase "active"/"message". Confirmed in-game
    // (2026-10-26): WarnIfAlreadyBuilt's own diagnostic logging proved the
    // backend was correctly detecting placed instances and calling Report(),
    // yet no banner ever appeared - the only other getter-bound types in
    // this codebase both avoid this exact trap by implementing IJsonWritable
    // themselves, which this one never did. Likely means the banner never
    // worked for ANY failure (including the older TrySave/TryDelete disk-
    // full/permission paths), just never noticed since those are rare.
    public readonly struct UIErrorEntry : IJsonWritable
    {
        public bool Active { get; }
        public string Message { get; }

        public UIErrorEntry(bool active, string message)
        {
            Active = active;
            Message = message;
        }

        public static readonly UIErrorEntry Inactive = new UIErrorEntry(false, "");

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(GetType().FullName);
            writer.PropertyName("active");
            writer.Write(Active);
            writer.PropertyName("message");
            writer.Write(Message);
            writer.TypeEnd();
        }
    }

    // Phase 5 (UI_UPGRADE_GUIDE.md) - minimal failure surfacing. Shared
    // across CollectionUISystem and ZoneUISystem (registered as two
    // separate systems, Mod.cs) rather than each getting its own getter
    // binding, so the shell only needs the one banner slot the guide
    // actually calls for - the two systems could in principle both fail
    // in close succession, but reconciling two independently active
    // banners for an edge case this rare isn't worth the complexity.
    // CollectionUISystem exposes this as the "UIError" binding and
    // "DismissUIError" trigger the shell renders; ZoneUISystem just
    // writes to it the same way CollectionUISystem's own TrySave/
    // TryDelete do, via this static surface, without exposing anything
    // of its own.
    internal static class UIErrorState
    {
        private static UIErrorEntry s_Current = UIErrorEntry.Inactive;

        public static UIErrorEntry Current => s_Current;

        public static void Report(string message) => s_Current = new UIErrorEntry(true, message);

        public static void Dismiss() => s_Current = UIErrorEntry.Inactive;
    }
}
