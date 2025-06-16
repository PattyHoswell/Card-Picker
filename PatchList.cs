using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ShinyShoe;
using ShinyShoe.Loading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Patty_CardPicker_MOD
{
    internal class PatchList
    {
        [HarmonyPostfix, HarmonyPatch(typeof(ShinyShoe.AppManager), "DoesThisBuildReportErrors")]
        public static void DisableErrorReportingPatch(ref bool __result)
        {
            __result = false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.StartGame))]
        public static void StartGame(RunType runType)
        {
            if (runType == RunType.MalickaChallenge && 
                !Plugin.EnableOnChallenge.Value)
            {
                return;
            }

            SaveManager saveManager = AllGameManagers.Instance.GetSaveManager();
            RelicManager relicManager = AllGameManagers.Instance.GetRelicManager();

            foreach (var cardEntry in Plugin.Entries)
            {
                if (cardEntry.Value.Value <= 0)
                {
                    continue;
                }

                CardData cardData = cardEntry.Key;

                /* You can enable this if you want. In my case I can't.
                 * Because what if it tries to add a card that can't be shown in the LogBook.
                 * There's too much to consider for this to be worth it for such little value.
                 */
                // saveManager.GetMetagameSave().MarkCardDiscovered(cardData);

                var mainClass = saveManager.GetMainClass();
                var subClass = saveManager.GetSubClass();
                var enhancerPool = mainClass.GetRandomDraftEnhancerPool() != null ? 
                                   mainClass.GetRandomDraftEnhancerPool() : 
                                   subClass.GetRandomDraftEnhancerPool();

                for (int i = 0; i < cardEntry.Value.Value; i++)
                {
                    CardState cardState = saveManager.AddCardToDeck(cardData, null, true);
                    relicManager.ApplyStartingUpgradeToDraftCard(cardState, false);

                    if (enhancerPool != null)
                    {
                        using (GenericPools.GetList(out List<EnhancerData> enhancers))
                        {
                            CardUpgradeData upgradeData = enhancerPool.GetAllChoices(enhancers)
                                                          .RandomElement(RngId.Rewards)
                                                          .GetEffects()[0]
                                                          .GetParamCardUpgradeData();

                            var cardUpgrade = new CardUpgradeState();
                            cardUpgrade.Setup(upgradeData);
                            cardState.Upgrade(cardUpgrade, saveManager, true);
                        }
                    }

                    relicManager.ApplyCardStateModifiers(cardState);
                }
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(SaveManager), nameof(SaveManager.AddCardToDeck))]
        public static void AddCardToDeck(CardData cardData, CardStateModifiers startingModifiers)
        {
            if (cardData == null ||
                startingModifiers == null)
            {
                return;
            }
            Plugin.LogSource.LogInfo(Environment.StackTrace);
        }
        [HarmonyPostfix, HarmonyPatch(typeof(LoadScreen), "StartLoadingScreen")]
        public static void StartLoadingScreen(LoadScreen __instance, ref ScreenManager.ScreenActiveCallback ___screenActiveCallback)
        {
            Plugin.InitializeConfigs();
            if (__instance.name == ScreenName.RunSetup)
            {
                ___screenActiveCallback += delegate (IScreen screen)
                {
                    var runSetupScreen = (RunSetupScreen)screen;
                    var mutatorSelectionDialog = (MutatorSelectionDialog)AccessTools.Field(typeof(RunSetupScreen), "mutatorSelectionDialog")
                                                                                    .GetValue(runSetupScreen);
                    if (mutatorSelectionDialog == null)
                    {
                        Plugin.LogSource.LogError("Oh no!");
                        return;
                    }

                    var clonedDialog = UnityEngine.Object.Instantiate(mutatorSelectionDialog, mutatorSelectionDialog.transform.parent.parent);
                    CardSelectionDialog.Instance = clonedDialog.gameObject.AddComponent<CardSelectionDialog>();
                    CardSelectionDialog.Instance.name = nameof(CardSelectionDialog);
                    CardSelectionDialog.Instance.Setup();
                    UnityEngine.Object.DestroyImmediate(clonedDialog.gameObject.GetComponent<MutatorSelectionDialog>());
                };
            }
        }
    }
}
