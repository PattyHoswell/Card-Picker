using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
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
        internal static bool InitializedConfigs { get; private set; }
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

            EnableOnChallenge = Config.Bind<bool>(new ConfigDefinition("Challenge Run", "Enable on Challenge Run"), true,
                                                  new ConfigDescription("Turn this on to start Challenge modes with the card you chose in the menu."));


            ShowAllCards = Config.Bind<bool>(
                new ConfigDefinition("Advanced", "Enable All Cards"),
                false,
                new ConfigDescription("Enable to show all cards including those that's not collectible (Some can be error). Proceed with caution",
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
            var factions = new List<string>
            {
                "Clanless",
                "Banished",
                "Pyreborne",
                "Luna Coven",
                "Underlegion",
                "Lazarus League",
                "Hellhorned",
                "Awoken",
                "Stygian Guard",
                "Umbra",
                "Melting Remnant",
            };
            var customFaction = GetAllGameData()
                               .GetAllClassDatas()
                               .Where(data => !factions.Contains(data.Cheat_GetNameEnglish()));
            factions.AddRange(customFaction.Select(data => data.Cheat_GetNameEnglish()));
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
