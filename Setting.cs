using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using System.Collections.Generic;
using ZoneBuilder.CustomZones;
using ZoneBuilder.UI;

namespace ZoneBuilder
{
    [FileLocation(nameof(ZoneBuilder))]
    [SettingsUIGroupOrder(kFeaturesGroup, kMaintenanceGroup)]
    [SettingsUIShowGroupName(kFeaturesGroup, kMaintenanceGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";
        public const string kFeaturesGroup = "Features";
        public const string kMaintenanceGroup = "Maintenance";

        public Setting(IMod mod) : base(mod)
        {
        }

        [SettingsUISection(kSection, kFeaturesGroup)]
        public bool EnableCustomZones { get; set; } = true;

        // Settings-panel counterpart to CollectionUISystem's "already
        // built" warning banner (see its own comment) - that one can only
        // warn, since it fires from a UI trigger with no business
        // deleting anything on its own. This is the actual fix a player
        // can reach for: a building whose collection no longer supplies
        // it, with no source to re-clone from, never resolves again on
        // its own (see CustomZoneBuilder.CleanUpOrphanedBuildings's own
        // comment for the full mechanism). Lives here, not in the main
        // panel, since it's a one-off maintenance action, not something
        // reached for during normal editing.
        //
        // [SettingsUIButton]/[SettingsUIConfirmation] match the exact
        // shape vanilla's own GeneralSettings.resetSettings and
        // GameplaySettings.resetTutorials use (confirmed via decompiled
        // source) - a write-only bool property whose setter does the
        // work, with the confirmation dialog it gets for free from the
        // attribute rather than anything custom, since this is
        // irreversible (plain Deleted-tag removal, the same mechanism
        // the game's own bulldoze tool uses - not reversible by undo).
        //
        // No active city (e.g. clicked from the main menu) means
        // DefaultGameObjectInjectionWorld has no CustomZoneBuildRequestSystem
        // to find - reported the same way as a real result rather than
        // thrown, since a missing world here is an ordinary case to
        // handle, not a bug.
        // No args - confirmed against decompiled source
        // (Game.UI.Menu.AutomaticSettings.GetConfirmationMessage): passing
        // an explicit first arg sets confirmMessageId, which this method
        // uses INSTEAD of auto-deriving a key, as a literal override
        // wrapped in "Options.WARNING[...]" with the attribute's own
        // (here, never-set) confirmMessageValue as fallback - so
        // `[SettingsUIConfirmation(nameof(CleanUpOrphanedBuildings))]`
        // looked up "Options.WARNING[CleanUpOrphanedBuildings]" with no
        // fallback, which never matched the key LocaleEN below actually
        // registers (GetOptionWarningLocaleID's own auto-derived format)
        // and rendered as the raw unresolved locale id in the dialog
        // (UAT, 2026-10-26). No-args (matching vanilla's own
        // GeneralSettings.resetSettings/GameplaySettings.resetTutorials,
        // both `[SettingsUIConfirmation(null, null)]`) takes the other
        // branch instead, auto-deriving the exact same key
        // GetOptionWarningLocaleID generates below.
        [SettingsUISection(kSection, kMaintenanceGroup)]
        [SettingsUIButton]
        [SettingsUIConfirmation]
        public bool CleanUpOrphanedBuildings
        {
            set
            {
                var system = Unity.Entities.World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<CustomZoneBuildRequestSystem>();
                if (system == null)
                {
                    UIErrorState.Report("Zone Builder: no active city - load a save first.");
                    return;
                }

                var removed = system.CleanUpOrphanedBuildings();
                UIErrorState.Report(removed > 0
                    ? $"Zone Builder: removed {removed} orphaned building(s) that could no longer be rebuilt. Empty lots will regrow naturally from each zone's current building list."
                    : "Zone Builder: no orphaned buildings found - nothing to clean up.");
            }
        }

        public override void SetDefaults()
        {
            EnableCustomZones = true;
        }
    }

    public class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;

        public LocaleEN(Setting setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Zone Builder" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kFeaturesGroup), "Features" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kMaintenanceGroup), "Maintenance" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableCustomZones)), "Custom Zones" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableCustomZones)), "Create new zone types backed by collections." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.CleanUpOrphanedBuildings)), "Clean Up Orphaned Buildings" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.CleanUpOrphanedBuildings)), "Removes buildings that can no longer be rebuilt - for example, ones removed from their collection, or whose source asset is no longer installed. The zone and cell stay; a new building from the zone's current list can grow there again if there's demand. This cannot be undone." },
                { m_Setting.GetOptionWarningLocaleID(nameof(Setting.CleanUpOrphanedBuildings)), "This will permanently delete every orphaned Zone Builder building in your city. This cannot be undone. Continue?" },
            };
        }

        public void Unload()
        {
        }
    }
}
