using ZoneBuilder.Core;
using ZoneBuilder.CustomZones.GameAdapters;
using ZoneBuilder.UI;
using Colossal.Serialization.Entities;
using Game;
using System;
using System.Collections.Generic;

namespace ZoneBuilder.CustomZones
{
    // Owns the one timing requirement CustomZoneBuilder has: a
    // runtime-cloned prefab is only visible to the game's own
    // initialization systems during the exact frame it's added, one tick
    // before PrefabSystem's own update - see DECISIONS.md "A runtime-cloned
    // prefab is only visible to init systems for one frame" for the full
    // mechanism. Rather than trust every future caller (eventually a real
    // UI) to remember to call CustomZoneBuilder.BuildOrRebuild from the
    // right place, this system is registered UpdateBefore<_,
    // PrefabSystem>(MainLoop) and is the ONLY thing that ever calls it -
    // from its own OnUpdate. Everything else just calls Request(...), from
    // wherever it naturally runs (OnGameLoaded, a UI handler, anywhere),
    // and the actual build happens later, at the correct moment,
    // automatically. Also the one thing a caller needs for "create it when
    // I want, modify it when I want": requesting the same CustomZoneDefinition
    // again (e.g. after editing one of its Collections) rebuilds it in
    // place rather than creating a duplicate - see CustomZoneBuilder's own
    // comments for how.
    public partial class CustomZoneBuildRequestSystem : GameSystemBase
    {
        private readonly struct PendingRequest
        {
            public readonly CustomZoneDefinition Definition;
            public readonly IEnumerable<Collection> Collections;

            public PendingRequest(CustomZoneDefinition definition, IEnumerable<Collection> collections)
            {
                Definition = definition;
                Collections = collections;
            }
        }

        // How many collection entries BuildOrRebuildIncremental processes
        // per OnUpdate tick before yielding - see its own comment for why
        // this exists at all. Picked conservatively: even a single entry
        // can be expensive on first use this session (engine-side asset
        // loading, not this mod's own cost - see DECISIONS.md), so this
        // bounds worst-case per-tick cost to roughly
        // EntriesPerTick * (that one-time per-entry cost) rather than
        // eliminating it outright.
        private const int EntriesPerTick = 5;

        // Keyed by definition.Id, not a plain Queue<PendingRequest> - see
        // Request's own comment for why. m_PendingRequestOrder preserves
        // FIFO order across different zones; m_PendingRequestsById is
        // what actually gets looked up/overwritten/removed.
        private readonly Dictionary<Guid, PendingRequest> m_PendingRequestsById = new Dictionary<Guid, PendingRequest>();
        private readonly Queue<Guid> m_PendingRequestOrder = new Queue<Guid>();
        private readonly Queue<Guid> m_PendingRetirements = new Queue<Guid>();
        private CustomZoneBuilder m_Builder;
        private IEnumerator<ZoneBuildProgress> m_ActiveBuild;

        // null means no build in progress - ZoneUISystem polls this every
        // UI tick (see ExtendedUISystemBase.CreateBinding's getter
        // overload) to drive the "building zone..." indicator. Set
        // together with m_ActiveBuild, always - never let one change
        // without the other.
        public ZoneBuildProgress? CurrentProgress { get; private set; }

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Builder = new CustomZoneBuilder(World, new GameBuildingCatalog(World));
        }

        // The actual fix for "white box" buildings after a restart (found
        // 2026-10-21, alpha tester report "the entire zone disappeared and
        // all the buildings turned into white boxes" - confirmed via the
        // tester's own Player.log, then reproduced on our own machine with
        // a clean cold-boot test). Root cause, confirmed by reading the
        // decompiled Game.Serialization.LoadGameSystem/ResolvePrefabsSystem:
        // LoadGameSystem.OnUpdate runs the ENTIRE Deserialize phase (which
        // is where ResolvePrefabsSystem permanently marks any building/zone
        // prefab reference it can't resolve as "obsolete" - a vanilla
        // mechanism with no later retry anywhere in the base game) to
        // completion BEFORE it invokes onOnSaveGameLoaded, which is what
        // GameSystemBase.OnGameLoaded is wired to. Our old approach -
        // rebuilding everything from ZoneUISystem.OnGameLoaded - ran this
        // every single time, no exceptions. Since our zone/building
        // prefabs are ephemeral runtime clones (never serialized into the
        // save itself - see ZoneUISystem's own history), a save with
        // already-placed buildings from an earlier session has its
        // references resolved, and permanently marked obsolete, before our
        // code ever got a chance to re-clone them - not a race, a
        // deterministic miss, every first load of a save in a fresh
        // process.
        //
        // OnGamePreload fires per load attempt (not once per process),
        // before deserialization starts - GameSystemBase wires it straight
        // from GameManager.instance.onGamePreload, same pattern
        // Game.AutoSaveSystem itself uses. Rebuilding every zone here,
        // synchronously (not through the incremental Request() queue this
        // system normally uses for live, in-game edits), means our clones
        // already exist by the time ResolvePrefabsSystem runs, so it
        // resolves already-placed instances successfully instead of
        // marking them obsolete.
        //
        // Deliberately NOT going through the incremental/chunked path here
        // the way a live collection edit does - this runs during the
        // loading screen, before the game is interactive, so there's no
        // per-frame budget to protect the way BuildOrRebuildIncremental's
        // chunking exists for. Draining each zone's enumerator fully in one
        // tight loop instead. Confirmed safe in-game (2026-10-21) on a
        // 402-building zone (the same scale that, synchronously cloned
        // mid-gameplay rather than during a loading screen, previously
        // froze the game hard enough to need force-killing, with a
        // confirmed JobTempAlloc leak) - the loading screen has enough
        // slack that this doesn't reproduce that freeze.
        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);

            if (purpose != Purpose.LoadGame && purpose != Purpose.NewGame)
                return;

            foreach (var definition in Mod.CustomZoneDefinitionLibrary.GetAll())
            {
                foreach (var _ in m_Builder.BuildOrRebuildIncremental(definition, ZoneUISystem.ResolveCollections(definition), EntriesPerTick))
                {
                    // Discarding every yielded ZoneBuildProgress - draining
                    // synchronously on purpose, see this method's own
                    // comment above.
                }
            }
        }

        // Safe to call from anywhere, at any time, including OnGameLoaded -
        // this only ever enqueues. The actual clone+AddPrefab work happens
        // later, from this system's own OnUpdate. The caller resolves
        // definition.CollectionIds into real Collections itself (e.g. via
        // Mod.CollectionLibrary) - this system deliberately doesn't reach
        // for that global itself, so a test/debug caller can pass synthetic
        // Collections directly without touching real persisted data.
        //
        // Deduplicated by definition.Id, not a blind enqueue - the real
        // bug this fixes (user feedback, 2026-10-17): editing a Collection
        // triggers a rebuild of every zone using it (added so an edit
        // actually takes effect), but a UI action that fires many
        // individual mutations in a tight loop (e.g. the Assets tab's
        // "Select All") was calling this once per building, each queueing
        // its own full, redundant rebuild pass over the whole collection
        // before the previous one even started - a 321-entry collection
        // visibly "building" over and over. A second Request for a zone
        // already waiting in the queue just overwrites its pending
        // Collections snapshot in place instead of adding another entry;
        // only the single call that actually populates one zone into the
        // queue advances the order. A zone whose build is already
        // *actively* running isn't in this dictionary at all (removed the
        // moment OnUpdate dequeues it, see below) - a Request for it then
        // queues a fresh, separate pass for after the current one
        // finishes, which is correct: real new state arrived mid-build
        // and still needs picking up, not dropped.
        public void Request(CustomZoneDefinition definition, IEnumerable<Collection> collections)
        {
            if (!m_PendingRequestsById.ContainsKey(definition.Id))
                m_PendingRequestOrder.Enqueue(definition.Id);

            m_PendingRequestsById[definition.Id] = new PendingRequest(definition, collections);
        }

        // Unlike Request above, this is NOT queued - it's a pure,
        // immediate read (see CustomZoneBuilder.FindZoneTypesMissingLevel1's
        // own comment), safe to call directly from a UI trigger without
        // waiting for a frame.
        public IReadOnlyCollection<Core.ZoneType> FindZoneTypesMissingLevel1(Collection collection)
        {
            return m_Builder.FindZoneTypesMissingLevel1(collection);
        }

        // Same "pure, immediate read" reasoning as FindZoneTypesMissingLevel1
        // above - see CustomZoneBuilder.HasPlacedInstances's own comment.
        public bool HasPlacedInstances(Guid zoneDefinitionId, string templatePrefabId)
        {
            return m_Builder.HasPlacedInstances(zoneDefinitionId, templatePrefabId);
        }

        // Entry point for the "Clean Up Orphaned Buildings" settings
        // button (Setting.cs) - called directly via
        // World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged,
        // not queued through Request, same reasoning as HasPlacedInstances
        // above. Logs its own result since the Settings panel is a
        // separate UI context from the shell this system's other
        // bindings feed - Setting.cs surfaces the same count back to the
        // player via UIErrorState once it gets this call's return value.
        public int CleanUpOrphanedBuildings()
        {
            var removed = m_Builder.CleanUpOrphanedBuildings();
            Mod.log.Info($"[CustomZoneBuildRequestSystem] Cleaned up {removed} orphaned ZoneBuilder building(s).");
            return removed;
        }

        // Same "safe to call from anywhere, actually runs later at the
        // right moment" contract as Request above - deactivates (doesn't
        // remove) a deleted definition's already-built zone prefab, see
        // CustomZoneBuilder.RetireZone's own comment for why this goes
        // through the queue rather than calling PrefabSystem.AddOrUpdatePrefab
        // directly from wherever Delete happens to be called from.
        public void RequestRetirement(Guid definitionId)
        {
            m_PendingRetirements.Enqueue(definitionId);
        }

        protected override void OnUpdate()
        {
            // Advance the in-progress build exactly one step (one entry
            // chunk) this tick, or start the next queued one - never
            // both in the same tick, which is the entire point: bound
            // how much of this tick a build can cost.
            if (m_ActiveBuild != null)
            {
                AdvanceActiveBuild();
            }
            else if (m_PendingRequestOrder.Count > 0)
            {
                // Removed from the dictionary here, not just dequeued from
                // the order - this is what makes the next Request(...) for
                // this same zone (if one arrives while this build is
                // actively running) queue a fresh pass instead of silently
                // overwriting a snapshot nothing will ever read again.
                var id = m_PendingRequestOrder.Dequeue();
                var request = m_PendingRequestsById[id];
                m_PendingRequestsById.Remove(id);
                m_ActiveBuild = m_Builder.BuildOrRebuildIncremental(request.Definition, request.Collections, EntriesPerTick).GetEnumerator();
                AdvanceActiveBuild();
            }

            // Cheap (one RemovePrefab each) and unrelated to any
            // in-progress build - no reason to make these wait.
            while (m_PendingRetirements.Count > 0)
            {
                m_Builder.RetireZone(m_PendingRetirements.Dequeue());
            }
        }

        private void AdvanceActiveBuild()
        {
            if (m_ActiveBuild.MoveNext())
            {
                CurrentProgress = m_ActiveBuild.Current;
            }
            else
            {
                m_ActiveBuild = null;
                CurrentProgress = null;
            }
        }
    }
}
