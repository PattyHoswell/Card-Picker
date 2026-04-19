using HarmonyLib;
using I2.Loc;
using ShinyShoe;
using ShinyShoe.Loading;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Patty_CardPicker_MOD
{
    internal class PatchList
    {
        internal static bool loadToCreatePrefab = false;
        internal static CardSelectionDialog cardSelectionDialogPrefab;
        internal static GameObject setCardsButton;
        internal static ModOptionDialog modOptionDialogPrefab;
        internal static ModButtonUI modButtonPrefab;
        internal static TextMeshProUGUI championNameLabelPrefab;

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
            Plugin.CardsAddedByThisMod.Clear();

            AddCards();
        }

        private static void AddCards()
        {
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
                    Plugin.CardsAddedByThisMod.Add(cardState);

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
                            cardState.ApplyPermanentUpgrade(cardUpgrade, saveManager, true);
                        }
                    }

                    relicManager.ApplyCardStateModifiers(cardState);
                }
            }
        }

        public static IEnumerator LoadRunSetupScreenCoroutine(SoulSaviorRunSetupScreen saviourRunScreen)
        {
            yield return null;
            if (SceneManager.GetSceneByName("run_setup").isLoaded == false)
            {
                SceneManager.LoadScene("run_setup", LoadSceneMode.Additive);
            }
            yield return null;
            yield return null;

            RunSetupScreen runSetupScreen = (RunSetupScreen)AllGameManagers.Instance.GetScreenManager().GetScreen(ScreenName.RunSetup);

            var mutatorSelectionDialog = (MutatorSelectionDialog)AccessTools.Field(typeof(RunSetupScreen), "mutatorSelectionDialog")
                                                                            .GetValue(runSetupScreen);
            if (mutatorSelectionDialog == null)
            {
                Plugin.LogSource.LogError("Oh no!");
                yield break;
            }

            var clonedDialog = UnityEngine.Object.Instantiate(mutatorSelectionDialog, saviourRunScreen.transform);
            CardSelectionDialog.Instance = clonedDialog.gameObject.AddComponent<CardSelectionDialog>();
            CardSelectionDialog.Instance.name = nameof(CardSelectionDialog);
            CardSelectionDialog.Instance.Setup();
            UnityEngine.Object.DestroyImmediate(clonedDialog.gameObject.GetComponent<MutatorSelectionDialog>());

            yield return null;
            yield return SceneManager.UnloadSceneAsync("run_setup");
            yield return null;
            AllGameManagers.Instance.GetScreenManager().SetScreenActive(ScreenName.SoulSaviorRunSetup, true);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(LoadScreen), "StartLoadingScreen")]
        public static void StartLoadingScreen(LoadScreen __instance, ref ScreenManager.ScreenActiveCallback ___screenActiveCallback)
        {
            Plugin.InitializeConfigs();
            if (cardSelectionDialogPrefab == null && __instance.name == ScreenName.MainMenu)
            {
                ___screenActiveCallback += delegate (IScreen screen)
                {
                    AllGameManagers.Instance.GetSaveManager().SetActiveRunType(RunType.Class);
                    AllGameManagers.Instance.GetGameStateManager().StartNewRun(RunType.Class, "", null, true);
                    loadToCreatePrefab = true;
                };
            }
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

                    if (championNameLabelPrefab == null)
                    {
                        var championNameRoot = (TextMeshProUGUI)AccessTools.Field(typeof(RunSetupScreen), "championNameLabel").GetValue(runSetupScreen);
                        championNameLabelPrefab = GameObject.Instantiate(championNameRoot, null);
                        championNameLabelPrefab.name = "Label (Prefab)";
                        GameObject.DontDestroyOnLoad(championNameLabelPrefab);
                    }
                    if (cardSelectionDialogPrefab == null)
                    {
                        var prefab = GameObject.Instantiate(mutatorSelectionDialog, null);
                        cardSelectionDialogPrefab = prefab.gameObject.AddComponent<CardSelectionDialog>();
                        cardSelectionDialogPrefab.name = nameof(CardSelectionDialog) + " (Prefab)";
                        cardSelectionDialogPrefab.Setup();
                        GameObject.DestroyImmediate(prefab.gameObject.GetComponent<MutatorSelectionDialog>());
                        GameObject.DontDestroyOnLoad(cardSelectionDialogPrefab);
                        cardSelectionDialogPrefab.gameObject.SetActive(false);
                    }

                    CardSelectionDialog.Instance = GameObject.Instantiate(cardSelectionDialogPrefab, mutatorSelectionDialog.transform.parent.parent);
                    CardSelectionDialog.Instance.name = nameof(CardSelectionDialog);
                    CardSelectionDialog.Instance.Setup();

                    if (modOptionDialogPrefab == null)
                    {
                        var prefab = GameObject.Instantiate(mutatorSelectionDialog, null);
                        modOptionDialogPrefab = prefab.gameObject.AddOrGetComponent<ModOptionDialog>();
                        modOptionDialogPrefab.Setup();
                        GameObject.DontDestroyOnLoad(modOptionDialogPrefab);
                    }

                    if (modButtonPrefab == null)
                    {
                        modButtonPrefab = ModButtonUI.CreateModButtonUI(null, new ModButtonInfo());
                        modButtonPrefab.name = nameof(ModButtonUI) + " (Prefab)";
                        GameObject.DontDestroyOnLoad(modButtonPrefab.gameObject);
                    }

                    CardSelectionDialog.Instance.SetupAdvancedButton();

                    var uiFooter = runSetupScreen.transform.Find("UIFooter");
                    if (uiFooter == null)
                    {
                        Plugin.LogSource.LogError("UI footer is null");
                        loadToCreatePrefab = false;
                        return;
                    }
                    if (setCardsButton == null)
                    {
                        Transform swapChampButton = uiFooter.Find("Swap Champion Button");
                        setCardsButton = GameObject.Instantiate(swapChampButton.gameObject, null);
                        setCardsButton.gameObject.SetActive(false);
                        GameObject.DontDestroyOnLoad(setCardsButton);

                        var starterLabel = setCardsButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
                        GameObject.DestroyImmediate(starterLabel.GetComponent<Localize>());
                        starterLabel.fontSizeMin = starterLabel.fontSizeMax;
                        starterLabel.fontSize = starterLabel.fontSizeMax;
                        starterLabel.text = "Set Starter Cards";
                    }

                    var newSetCardsButton = GameObject.Instantiate(setCardsButton, uiFooter.transform);
                    newSetCardsButton.name = "SetCardsButton";
                    newSetCardsButton.SetActive(true);
                    newSetCardsButton.GetComponent<GameUISelectableButton>().onClick.AddListener(() =>
                    {
                        SoundManager.PlaySfxSignal.Dispatch("UI_Click");
                        CardSelectionDialog.Instance.enabled = true;
                        CardSelectionDialog.Instance.gameObject.SetActive(true);
                    });

                    if (loadToCreatePrefab)
                    {
                        AllGameManagers.Instance.GetScreenManager().SetScreenActive(ScreenName.MainMenu, true);
                        loadToCreatePrefab = false;
                    }
                };
            }
            else if (__instance.name == ScreenName.SoulSaviorRunSetup)
            {
                ___screenActiveCallback += delegate (IScreen screen)
                {
                    var saviourRunScreen = (SoulSaviorRunSetupScreen)screen;
                    var startButton = (GameUISelectableButton)AccessTools.Field(typeof(SoulSaviorRunSetupScreen), "startButton")
                                                                             .GetValue(saviourRunScreen);
                    if (cardSelectionDialogPrefab == null)
                    {
                        Plugin.LogSource.LogError("There is no prefab to create the menu? Please report to Patty because this is not supposed to happend");
                        return;
                    }
                    if (setCardsButton == null)
                    {
                        Plugin.LogSource.LogError("There is no prefab to create the set card buttons? Please report to Patty because this is not supposed to happend");
                        return;
                    }
                    CardSelectionDialog.Instance = GameObject.Instantiate(cardSelectionDialogPrefab, saviourRunScreen.transform);
                    CardSelectionDialog.Instance.name = nameof(CardSelectionDialog);
                    CardSelectionDialog.Instance.Setup();

                    var newSetCardsButton = GameObject.Instantiate(setCardsButton, saviourRunScreen.transform.Find("UIFooter"));
                    newSetCardsButton.name = "SetCardsButton";
                    newSetCardsButton.SetActive(true);
                    newSetCardsButton.GetComponent<GameUISelectableButton>().onClick.AddListener(() =>
                    {
                        SoundManager.PlaySfxSignal.Dispatch("UI_Click");
                        CardSelectionDialog.Instance.enabled = true;
                        CardSelectionDialog.Instance.gameObject.SetActive(true);
                    });
                };
            }
            else if (__instance.name == ScreenName.Map || __instance.name == ScreenName.SoulSaviorMap)
            {
                SaveManager saveManager = AllGameManagers.Instance.GetSaveManager();
                if (!Plugin.RemoveStartingDeck.Value ||
                    saveManager.GetGameSequence() != SaveData.GameSequence.Initial)
                {
                    Plugin.CardsAddedByThisMod.Clear();
                    return;
                }
                ___screenActiveCallback += delegate (IScreen screen)
                {
                    foreach (CardState cardState in saveManager.GetDeckState().ToList())
                    {
                        if (Plugin.CardsAddedByThisMod.Contains(cardState) ||
                            cardState.IsChampionCard())
                        {
                            continue;
                        }
                        saveManager.RemoveCardFromDeck(cardState);
                    }
                    Plugin.CardsAddedByThisMod.Clear();
                };
            }
        }
    }
}
