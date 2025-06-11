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
                (bool)Plugin.EnableOnChallenge.BoxedValue == false)
            {
                return;
            }
            foreach (KeyValuePair<CardData, ConfigEntry<int>> pickedCard in Plugin.Entries)
            {
                if ((int)pickedCard.Value.BoxedValue <= 0)
                {
                    continue;
                }
                LoadingScreen.AddTask(new LoadAdditionalCards(pickedCard.Key, true, LoadingScreen.DisplayStyle.Spinner, delegate
                {
                    AllGameManagers.Instance.GetSaveManager().AddCardToDeck(pickedCard.Key, null, true, (int)pickedCard.Value.BoxedValue - 1);
                }));
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(LoadScreen), "StartLoadingScreen")]
        public static void StartLoadingScreen(LoadScreen __instance, ref ScreenManager.ScreenActiveCallback ___screenActiveCallback)
        {
            Plugin.InitializeConfigs();
            if (__instance.name == ScreenName.RunSetup)
            {
                ___screenActiveCallback += delegate (IScreen screen)
                {
                    var runSetupScreen = UnityEngine.Object.FindObjectOfType<RunSetupScreen>();
                    var mutatorSelectionDialog = (MutatorSelectionDialog)AccessTools.Field(typeof(RunSetupScreen), "mutatorSelectionDialog")
                                                                                    .GetValue(runSetupScreen);
                    if (mutatorSelectionDialog == null)
                    {
                        Plugin.LogSource.LogError("Oh no!");
                        return;
                    }

                    var clonedDialog = UnityEngine.Object.Instantiate(mutatorSelectionDialog, mutatorSelectionDialog.transform.parent.parent);
                    CardSelectionDialog.Instance = clonedDialog.gameObject.AddComponent<CardSelectionDialog>();
                    CardSelectionDialog.Instance.SetupScreen = runSetupScreen;
                    CardSelectionDialog.Instance.name = nameof(CardSelectionDialog);
                    CardSelectionDialog.Instance.Setup();
                    UnityEngine.Object.DestroyImmediate(clonedDialog.gameObject.GetComponent<MutatorSelectionDialog>());
                };
            }
        }
    }
}
