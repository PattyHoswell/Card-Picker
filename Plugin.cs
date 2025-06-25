using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Patty_CardPicker_MOD
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource LogSource { get; private set; }
        internal static Harmony PluginHarmony { get; private set; }
        internal static Dictionary<CardData, ConfigEntry<int>> Entries { get; private set; } = new Dictionary<CardData, ConfigEntry<int>>();
        internal static ConfigEntry<bool> ShowAllCards { get; private set; }
        internal static ConfigEntry<bool> EnableOnChallenge { get; private set; }
        internal static ConfigEntry<bool> RemoveStartingDeck { get; private set; }
        internal static HashSet<CardState> CardsAddedByThisMod { get; private set; } = new HashSet<CardState>();
        internal static bool InitializedConfigs { get; private set; }

        #region Translation
        internal static readonly OrderedDictionary ClanNameTranslationTerm = new OrderedDictionary
        {
            { "NonClass", "Clanless" },
            { "ClassData_titleLoc-604d44e6022d1c24-a3e4db5fc0afb9647906b33012f7b6e3-v2", "Banished" },
            { "ClassData_titleLoc-b946b201735c4048-fed615dbf2f84274ab8b72a7f7056fa8-v2", "Pyreborne" },
            { "ClassData_titleLoc-8338ffb122ab2e96-30528e09008d5c74fb51ff909ff75876-v2", "Luna Coven" },
            { "ClassData_titleLoc-9948d88fb75b25c9-d03f152bb38a72748891caa14769abd1-v2", "Underlegion" },
            { "ClassData_titleLoc-d85783c925521680-a6f5d6167ffd9dc4781b19278b89d2e1-v2", "Lazarus League" },
            { "ClassData_titleLoc-eb038694d9e044bb-b152a27f359a4e04cbcc29055c2f836b-v2", "Hellhorned" },
            { "ClassData_titleLoc-f76bea8450f06f67-55d2f9d7591683f4ca58a33311477d92-v2", "Awoken" },
            { "ClassData_titleLoc-37d27dbaadc5f40f-861c056fdeda9814284a85e9b3f034d0-v2", "Stygian Guard" },
            { "ClassData_titleLoc-2e445261f0cc3308-6f37f31f362b3c44e96df0656095657a-v2", "Umbra" },
            { "ClassData_titleLoc-1438fe314ad47795-95d25698eaac978488921909b1239bbc-v2", "Melting Remnant" }
        };

        internal static readonly OrderedDictionary CardTypeTranslationTerm = new OrderedDictionary
        {
            { "Compendium_Filter_CardType_All", "All" },
            { "Compendium_Filter_CardType_Monster", "Unit" },
            { "Compendium_Filter_CardType_Spell", "Spell" },
            { "Compendium_Filter_CardType_Equipment", "Equipment" },
            { "Compendium_Filter_CardType_TrainRoomAttachment", "Room" },
            { "Compendium_Filter_CardType_Blight", "Blight" },
            { "Compendium_Filter_CardType_Junk", "Scourge" }
        };

        internal static readonly OrderedDictionary CardRarityTranslationTerm = new OrderedDictionary()
        {
            { "Compendium_Filter_CardType_All", "All" },
            { "CardRarity_Champion", "Champion" },
            { "CardRarity_Common", "Common" },
            { "CardRarity_Uncommon", "Uncommon" },
            { "CardRarity_Rare", "Rare" }
        };

        internal static readonly HashSet<string> FactionNamesEnglishOnly = new HashSet<string>(ClanNameTranslationTerm.Values.Cast<string>());
        #endregion
        void Awake()
        {
            LogSource = Logger;
            try
            {
                PluginHarmony = Harmony.CreateAndPatchAll(typeof(PatchList), PluginInfo.GUID);
            }
            catch (HarmonyException ex)
            {
                LogSource.LogError((ex.InnerException ?? ex).Message);
            }

            EnableOnChallenge = Config.Bind<bool>(new ConfigDefinition("Basic", "Enable on Dimension Portal"), true,
                                                  new ConfigDescription("Enable to start Dimension Portal with the card you chose in the menu."));

            RemoveStartingDeck = Config.Bind<bool>(new ConfigDefinition("Basic", "Remove Starting Deck"), true,
                                                  new ConfigDescription("Enable to remove starting deck (Excluding the champions)."));

            ShowAllCards = Config.Bind<bool>(
                new ConfigDefinition("Advanced", "Show Uncollectible Cards"),
                false,
                new ConfigDescription("Enable to show all cards including those that's not collectible (Some can be error). Proceed with caution.",
                null,
                new ConfigurationManagerAttributes
                {
                    IsAdvanced = true,
                })
            );
            ShowAllCards.SettingChanged += ShowAllCards_SettingChanged;
        }

        private void ShowAllCards_SettingChanged(object sender, EventArgs e)
        {
            Entries.Clear();
            InitializedConfigs = false;
            InitializeConfigs();
        }

        internal static AllGameData GetAllGameData()
        {
            return AllGameManagers.Instance.GetAllGameData();
        }

        internal static void InitializeConfigs()
        {
            if (InitializedConfigs || AllGameManagers.Instance == null)
            {
                return;
            }
            InitializedConfigs = true;
            CardsAddedByThisMod.Clear();
            
            var factions = FactionNamesEnglishOnly;
            var customFaction = GetAllGameData()
                               .GetAllClassDatas()
                               .Where(data => !factions.Contains(data.Cheat_GetNameEnglish()));
            FactionNamesEnglishOnly.UnionWith(customFaction.Select(data => data.Cheat_GetNameEnglish()));
            Dictionary<ClassData, ConfigFile> configFilesDict = new Dictionary<ClassData, ConfigFile>();
            ConfigFile clanlessConfig = new ConfigFile(Path.Combine(Paths.ConfigPath, PluginInfo.Name, "Clanless.cfg"), true);
            foreach (var faction in factions)
            {
                switch (faction)
                {
                    case "Clanless":
                        break;
                    default:
                        ClassData classData = GetAllGameData().GetAllClassDatas()
                                                              .First(data => data.Cheat_GetNameEnglish() == faction);
                        configFilesDict[classData] = new ConfigFile(Path.Combine(Paths.ConfigPath, PluginInfo.Name, faction + ".cfg"), true);
                        break;
                }
            }
            foreach (CardData card in GetAllCardDatas())
            {
                ConfigFile config;
                if (card.GetLinkedClass() == null || 
                    !configFilesDict.TryGetValue(card.GetLinkedClass(), out config))
                {
                    config = clanlessConfig;
                }
                Entries[card] = config.Bind<int>(
                    new ConfigDefinition("Cards Amount", card.name),
                    0,
                    new ConfigDescription("", null, new ConfigurationManagerAttributes
                    {
                        Browsable = false
                    })
                );
            }
        }

        internal static IEnumerable<CardData> GetAllCardDatas()
        {
            AllGameData allGameData = AllGameManagers.Instance.GetAllGameData();
            IReadOnlyList<CardData> allCards;
            if ((bool)ShowAllCards.BoxedValue)
            {
                allCards = allGameData.GetAllCardData();
            }
            else
            {
                allCards = allGameData.CollectAllAccessibleCardDatas();
            }
            return allCards.Where(card => card != null &&
            AssetLoadingManager.IsOwnerInBaseGameOrInstalledDlc(card, AllGameManagers.Instance.GetSaveManager()));
        }
    }
}
