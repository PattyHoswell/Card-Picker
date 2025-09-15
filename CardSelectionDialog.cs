using BepInEx.Configuration;
using HarmonyLib;
using I2.Loc;
using ShinyShoe;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Patty_CardPicker_MOD
{
    internal class CardSelectionDialog : MonoBehaviour
    {
        ScreenDialog dialog;
        CardButtonUI layout;
        Button closeButton;
        List<CardButtonUI> cardButtons = new List<CardButtonUI>();
        TextMeshProUGUI title, warning;
        bool initialized, initializedOptions;
        internal ScrollRect scrollRect;
        internal Image bg;
        internal static readonly Color BGColor = new Color(0, 0, 0, 0.902f);
        internal static CardSelectionDialog Instance { get; set; }
        internal CardButtonUI focusedButton;

        internal ClassData selectedFaction;
        internal CollectableRarity selectedRarity = CollectableRarity.Common;
        internal CardType selectedType = CardType.Monster;

        internal readonly HashSet<string> FactionNames = new HashSet<string>();
        internal readonly OrderedDictionary CardFactionOptions = new OrderedDictionary();
        internal readonly OrderedDictionary CardTypeOptions = new OrderedDictionary();
        internal readonly OrderedDictionary CardRarityOptions = new OrderedDictionary();
        void Awake()
        {
            Setup();
        }

        void OnEnable()
        {
            Open();
        }

        void OnDisable()
        {
            Close();
        }

        internal void Open()
        {
            Setup();

            if (!initializedOptions)
            {
                initializedOptions = true;
                StartCoroutine(CreateDropdownList());
            }
            else
            {
                ResetOrder();
            }

            transform.SetAsLastSibling();
            dialog.SetActive(true, gameObject);
        }
        internal void ResetOrder()
        {
            cardButtons = cardButtons.OrderByDescending(cardButton =>
                                      Plugin.Entries.TryGetValue(cardButton.Data, out ConfigEntry<int> value) ? (int)value.BoxedValue : 0)
                                     .ThenByDescending(card =>
                                     {
                                         var spawnCharacter = card.Data.GetSpawnCharacterData();
                                         return spawnCharacter != null && spawnCharacter.IsChampion();
                                     }).ThenBy(card => card.Data.GetCost())
                                     .ThenByDescending(card => card.Data.GetCostType() == CardData.CostType.ConsumeRemainingEnergy)
                                     .ThenBy(card => card.Data.Cheat_GetNameEnglish()
                                     ).ToList();
            for (int i = 0; i < cardButtons.Count; i++)
            {
                var cardButton = cardButtons[i];
                cardButton.transform.SetSiblingIndex(i);
                cardButton.tooltipSide = i % 4 >= 2 ? TooltipSide.Left : TooltipSide.Right;
                cardButton.cardUI.SetTooltipSide(cardButton.tooltipSide);
            }
        }
        internal void Close()
        {
            Setup();
            dialog.SetActive(false, gameObject);
        }

        // Many failsafe check in case the menu is trying to open without being initialized
        internal void Setup()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;

            bg = transform.Find("Dialog Overlay").GetComponent<Image>();
            var uiFooter = FindObjectOfType<UIFooter>();
            InitializeBasicComponents();
            SetupDialogAndScrollView();
            SetupTitleAndWarning();
            SetupStarterCardsButton(uiFooter);
            SetupCardGridLayout();
            SetupCardPreviewTemplate();
            SetupCloseButton();
            SetupResetButton(uiFooter);
            SetupOptions();
        }

        private void InitializeBasicComponents()
        {
            Instance = this;
            name = nameof(CardSelectionDialog);
            dialog = GetComponentInChildren<ScreenDialog>();
            scrollRect = GetComponentInChildren<ScrollRect>();
        }

        private void SetupDialogAndScrollView()
        {
            Transform originalCloseButton = transform.Find("Dialog/CloseButton");
            DestroyImmediate(originalCloseButton.GetComponent<GameUISelectableButton>());
            DestroyImmediate(scrollRect.content.GetChild(0).gameObject);
        }

        private void SetupTitleAndWarning()
        {
            var instructions = dialog.transform.Find("Content/Info and Preview/Instructions");

            title = instructions.Find("Instructions label").GetComponent<TextMeshProUGUI>();
            DestroyImmediate(title.GetComponent<Localize>());
            title.text = "Choose any cards to customize your run.";

            warning = instructions.Find("Warning layout/Warning label").GetComponent<TextMeshProUGUI>();
            DestroyImmediate(warning.GetComponent<Localize>());
            warning.text = "Not every card here has been tested, enabled card will be sorted at the top. " +
                           "Re-open the menu to sort it, left click to increase the amount, " +
                           "right click to decrease.";
        }

        private void SetupStarterCardsButton(UIFooter uiFooter)
        {
            Transform swapChampButton = uiFooter.transform.Find("Swap Champion Button");
            Transform starterCardsButton = Instantiate(swapChampButton, uiFooter.transform);

            var starterLabel = starterCardsButton.Find("Label").GetComponent<TextMeshProUGUI>();
            DestroyImmediate(starterLabel.GetComponent<Localize>());
            starterLabel.fontSizeMin = starterLabel.fontSizeMax;
            starterLabel.fontSize = starterLabel.fontSizeMax;
            starterLabel.text = "Set Starter Cards";

            starterCardsButton.GetComponent<GameUISelectableButton>().onClick.AddListener(() =>
            {
                SoundManager.PlaySfxSignal.Dispatch("UI_Click");
                enabled = true;
                gameObject.SetActive(true);
            });
        }

        private void SetupCardGridLayout()
        {
            var contentGroup = scrollRect.content.GetComponent<GridLayoutGroup>();
            contentGroup.cellSize = new Vector2(312, 450);
            contentGroup.constraintCount = 4;
        }

        private void SetupCardPreviewTemplate()
        {
            var clonedCardPreview = Instantiate(FindObjectOfType<CardUI>(), transform);
            clonedCardPreview.name = "CardUIPrefab";
            layout = clonedCardPreview.gameObject.AddComponent<CardButtonUI>();
            layout.transform.localScale = Vector3.one;
            layout.gameObject.SetActive(false);
        }

        private void SetupCloseButton()
        {
            Transform originalCloseButton = transform.Find("Dialog/CloseButton");
            closeButton = originalCloseButton.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeButton.transform.Find("Target Graphic").GetComponent<Image>();
            closeButton.gameObject.SetActive(true);
            closeButton.onClick = new Button.ButtonClickedEvent();
            closeButton.onClick.AddListener(() =>
            {
                SoundManager.PlaySfxSignal.Dispatch("UI_Click");
                Close();
            });
        }

        private void SetupResetButton(UIFooter uiFooter)
        {
            Button resetButton = Instantiate(closeButton, transform);
            resetButton.name = "Reset Button";
            resetButton.transform.localPosition = new Vector2(0, -460);

            DestroyImmediate(resetButton.targetGraphic.gameObject);
            DestroyImmediate(resetButton.transform.Find("Image close icon").gameObject);

            var starterLabel = uiFooter.transform.Find("Swap Champion Button/Label").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI resetLabel = Instantiate(starterLabel, resetButton.transform);
            DestroyImmediate(resetLabel.GetComponent<Localize>());

            resetLabel.name = "Label";
            resetLabel.fontSizeMin = resetLabel.fontSizeMax;
            resetLabel.fontSize = resetLabel.fontSizeMax;
            resetLabel.text = "Reset Current Page";

            resetButton.targetGraphic = Instantiate(
                uiFooter.transform.Find("Swap Champion Button/Target Graphic").GetComponent<Image>(),
                resetButton.transform
            );
            resetButton.targetGraphic.name = "Target Graphic";
            resetButton.targetGraphic.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 112);
            resetButton.targetGraphic.transform.SetAsFirstSibling();

            resetButton.onClick = new Button.ButtonClickedEvent();
            resetButton.onClick.AddListener(() =>
            {
                SoundManager.PlaySfxSignal.Dispatch("UI_Click");
                ResetAllCardCounts();
            });

            RectTransform resetButtonHitboxTr = resetButton.transform.Find("Hitbox Invis").GetComponent<RectTransform>();
            resetButtonHitboxTr.localRotation = Quaternion.identity;
            resetButtonHitboxTr.sizeDelta = new Vector2(280, 0);

            var resetAll = Instantiate(resetButton, transform);
            resetAll.name = "Reset All Button";
            resetAll.transform.localPosition = new Vector2(-500, -460);
            resetAll.transform.Find("Label").GetComponent<TextMeshProUGUI>().text = "Reset All";

            resetAll.onClick = new Button.ButtonClickedEvent();
            resetAll.onClick.AddListener(() =>
            {
                SoundManager.PlaySfxSignal.Dispatch("UI_Click");
                foreach (var entry in Plugin.Entries)
                {
                    entry.Value.Value = 0;
                }
                ResetAllCardCounts();
            });


            var runSetupScreen = (RunSetupScreen)AllGameManagers.Instance.GetScreenManager().GetScreen(ScreenName.RunSetup);
            var mutatorSelectionDialog = (MutatorSelectionDialog)AccessTools.Field(typeof(RunSetupScreen), "mutatorSelectionDialog")
                                                                            .GetValue(runSetupScreen);
            var clonedDialog = Instantiate(mutatorSelectionDialog, mutatorSelectionDialog.transform.parent.parent);
            var modOptionDialog = clonedDialog.gameObject.AddComponent<ModOptionDialog>();
            modOptionDialog.cardSelectionDialog = this;
            modOptionDialog.Setup();

            var advanced = Instantiate(resetButton, transform);
            advanced.name = "Advanced Button";
            advanced.transform.localPosition = new Vector2(500, -460);
            advanced.transform.Find("Label").GetComponent<TextMeshProUGUI>().text = "Options";

            advanced.onClick = new Button.ButtonClickedEvent();
            advanced.onClick.AddListener(() =>
            {
                SoundManager.PlaySfxSignal.Dispatch("UI_Click");
                bg.color = new Color(0, 0, 0, 0.502f);
                SetFocusCard(null);
                modOptionDialog.Open();
            });
        }
        private void SetupOptions()
        {
            var orderedClassTerms = Plugin.ClanNameTranslationTerm.Keys.Cast<string>().ToList();
            var orderedClass = Plugin.ClanNameTranslationTerm.Values.Cast<string>().ToList();

            var clanlessName = LocalizationManager.GetTranslation(orderedClassTerms.First());
            CardFactionOptions[clanlessName] = null;
            FactionNames.Add(clanlessName);

            var allClassDatas = Plugin.GetAllGameData().GetAllClassDatas();
            var orderedVanillaFactions = allClassDatas.Where(data => orderedClass.Contains(data.Cheat_GetNameEnglish()))
                                                      .OrderBy(data => orderedClass.IndexOf(data.Cheat_GetNameEnglish()));

            var orderedCustomFactions = allClassDatas.Where(data => !orderedClass.Contains(data.Cheat_GetNameEnglish()))
                                                     .OrderBy(data => data.GetTitle());

            foreach (var classData in orderedVanillaFactions.Union(orderedCustomFactions))
            {
                var title = classData.GetTitle();
                FactionNames.Add(title);
                CardFactionOptions[title] = classData;
            }

            foreach (DictionaryEntry cardRarity in Plugin.CardRarityTranslationTerm)
            {
                if (!LocalizationManager.TryGetTranslation((string)cardRarity.Key, out string translatedCardRarity))
                {
                    translatedCardRarity = (string)cardRarity.Value;
                }
                switch (cardRarity.Value)
                {
                    case "All":
                        CardRarityOptions[translatedCardRarity] = (CollectableRarity)(-9999);
                        break;
                    case "Champion":
                        CardRarityOptions[translatedCardRarity] = CollectableRarity.Champion;
                        break;
                    case "Common":
                        CardRarityOptions[translatedCardRarity] = CollectableRarity.Common;
                        break;
                    case "Uncommon":
                        CardRarityOptions[translatedCardRarity] = CollectableRarity.Uncommon;
                        break;
                    case "Rare":
                        CardRarityOptions[translatedCardRarity] = CollectableRarity.Rare;
                        break;
                }
            }
            foreach (DictionaryEntry cardType in Plugin.CardTypeTranslationTerm)
            {
                if (!LocalizationManager.TryGetTranslation((string)cardType.Key, out string translatedCardType))
                {
                    translatedCardType = (string)cardType.Value;
                }
                switch (cardType.Value)
                {
                    case "All":
                        CardTypeOptions[translatedCardType] = (CardType)(-9999);
                        break;
                    case "Unit":
                        CardTypeOptions[translatedCardType] = CardType.Monster;
                        break;
                    case "Spell":
                        CardTypeOptions[translatedCardType] = CardType.Spell;
                        break;
                    case "Equipment":
                        CardTypeOptions[translatedCardType] = CardType.Equipment;
                        break;
                    case "Room":
                        CardTypeOptions[translatedCardType] = CardType.TrainRoomAttachment;
                        break;
                    case "Blight":
                        CardTypeOptions[translatedCardType] = CardType.Blight;
                        break;
                    case "Scourge":
                        CardTypeOptions[translatedCardType] = CardType.Junk;
                        break;
                }
            }
        }

        private void ResetAllCardCounts()
        {
            foreach (CardButtonUI buttonUI in cardButtons)
            {
                buttonUI.SetAmount(0);
            }
            ResetOrder();
        }

        internal void RefreshCards()
        {
            LoadCards(selectedType, selectedRarity, selectedFaction);
        }

        private void LoadCards(CardType cardType, CollectableRarity rarity, ClassData faction)
        {
            IEnumerable<CardData> cards = Plugin.GetAllCardDatas()
                                                .Where(card => card.GetLinkedClass() == faction);
            // -9999 is value for All
            if ((int)cardType != -9999)
            {
                cards = cards.Where(card => card.GetCardType() == cardType);
            }

            if ((int)rarity != -9999)
            {
                cards = cards.Where(card => card.GetRarity() == rarity);
            }

            AssetLoadingManager.GetInst().LoadCardsForCompendium(cards, delegate (IAddressableAssetOwner owner)
            {
                return AssetLoadingManager.IsOwnerInBaseGameOrInstalledDlc(owner, AllGameManagers.Instance.GetSaveManager());
            }, () => CreateCardButtons(cards)
            );
        }

        private void CreateCardButtons(IEnumerable<CardData> cards)
        {
            foreach (var card in cardButtons)
            {
                DestroyImmediate(card.gameObject);
            }
            cardButtons.Clear();

            foreach (var card in cards)
            {
                var cardButton = Instantiate(layout, scrollRect.content);
                cardButton.Set(card, this);
                cardButton.gameObject.SetActive(true);
                cardButton.transform.localScale = Vector3.one;
                cardButtons.Add(cardButton);
            }

            ResetOrder();
        }

        IEnumerator CreateDropdownList()
        {
            var preview = dialog.transform.Find("Content")
                                          .Find("Info and Preview")
                                          .Find("Mutators preview");
            foreach (Transform transform in preview)
            {
                Destroy(transform.gameObject);
            }
            DestroyImmediate(preview.GetComponent<HorizontalLayoutGroup>());
            preview.gameObject.AddComponent<VerticalLayoutGroup>();

            var ogDropdown = FindObjectOfType<GameUISelectableDropdown>(true);
            GameUISelectableDropdown CreateDropdownWithOptions(OrderedDictionary translatedTerms = null,
                                                               HashSet<string> customOptions = null)
            {
                var dropdown = Instantiate(ogDropdown, preview);
                using (GenericPools.GetList(out List<string> translatedOptions))
                {
                    if (translatedTerms != null)
                    {
                        foreach (DictionaryEntry option in translatedTerms)
                        {
                            string translatedName;
                            if (!LocalizationManager.TryGetTranslation((string)option.Key, out translatedName))
                            {
                                translatedName = (string)option.Value;
                            }
                            translatedOptions.Add(translatedName);
                        }
                    }
                    if (customOptions != null)
                    {
                        translatedOptions.AddRange(customOptions);
                    }
                    translatedOptions.RemoveDuplicates();
                    dropdown.SetOptions(translatedOptions);
                }
                dropdown.onClick.AddListener(delegate ()
                {
                    InputManager.Inst.TryGetSignaledInputMapping(InputManager.Controls.Submit, out CoreInputControlMapping mapping);
                    dropdown.ApplyScreenInput(mapping, dropdown, InputManager.Controls.Submit);
                });
                return dropdown;
            }
            void SetupDropdownButtonListeners(GameUISelectableDropdown targetDropdown)
            {
                foreach (var button in targetDropdown.GetComponentsInChildren<GameUISelectableButton>(true))
                {
                    if (button == targetDropdown)
                    {
                        continue;
                    }
                    button.onClick.AddListener(delegate ()
                    {
                        InputManager.Inst.TryGetSignaledInputMapping(InputManager.Controls.Submit, out CoreInputControlMapping mapping);
                        targetDropdown.ApplyScreenInput(mapping, button, InputManager.Controls.Submit);
                    });
                }
            }
            var factionDropdown = CreateDropdownWithOptions(customOptions: FactionNames);
            var cardTypeDropdown = CreateDropdownWithOptions(Plugin.CardTypeTranslationTerm);
            var cardRarityDropdown = CreateDropdownWithOptions(Plugin.CardRarityTranslationTerm);


            // Honestly there should be a better way of doing this than having to write it manually
            // But I'm not gonna bother spending more time on it as there will only be like 3 dropdown anyways
            factionDropdown.onClick.AddListener(() =>
            {
                cardTypeDropdown.Close();
                cardRarityDropdown.Close();
            });

            cardTypeDropdown.onClick.AddListener(() =>
            {
                factionDropdown.Close();
                cardRarityDropdown.Close();
            });

            cardRarityDropdown.onClick.AddListener(() =>
            {
                factionDropdown.Close();
                cardTypeDropdown.Close();
            });

            yield return null;
            SetupDropdownButtonListeners(factionDropdown);
            SetupDropdownButtonListeners(cardTypeDropdown);
            SetupDropdownButtonListeners(cardRarityDropdown);

            factionDropdown.optionChosenSignal.AddListener(delegate (int index, string optionName)
            {
                if (selectedFaction == (ClassData)CardFactionOptions[optionName])
                {
                    return;
                }
                selectedFaction = (ClassData)CardFactionOptions[optionName];
                LoadCards(selectedType, selectedRarity, selectedFaction);
            });

            cardTypeDropdown.optionChosenSignal.AddListener(delegate (int index, string optionName)
            {
                if (selectedType == (CardType)CardTypeOptions[optionName])
                {
                    return;
                }
                selectedType = (CardType)CardTypeOptions[optionName];
                LoadCards(selectedType, selectedRarity, selectedFaction);
            });

            cardRarityDropdown.optionChosenSignal.AddListener(delegate (int index, string optionName)
            {
                if (selectedRarity == (CollectableRarity)CardRarityOptions[optionName])
                {
                    return;
                }
                selectedRarity = (CollectableRarity)CardRarityOptions[optionName];
                LoadCards(selectedType, selectedRarity, selectedFaction);
            });

            factionDropdown.SetIndex(1);
            cardTypeDropdown.SetIndex(1);
            cardRarityDropdown.SetIndex(0);
            selectedFaction = (ClassData)CardFactionOptions[1];
            selectedType = (CardType)CardTypeOptions[1];
            selectedRarity = (CollectableRarity)CardRarityOptions[0];

            LoadCards(selectedType, selectedRarity, selectedFaction);
            preview.gameObject.SetActive(true);
        }

        internal void SetFocusCard(CardButtonUI cardButton)
        {
            if (focusedButton != null)
            {
                focusedButton.tooltip.transform.SetParent(focusedButton.cardUI.transform, false);
                focusedButton.cardUI.ResetFocus();
                focusedButton.cardUI.ResetFocusedScale();
                focusedButton = null;
            }
            if (cardButton == null)
            {
                return;
            }
            focusedButton = cardButton;
            cardButton.tooltip.transform.SetParent(transform, false);
            cardButton.cardUI.SetFocus(null, true, CardTooltipContainer.LoreTooltipSetting.Never, false);
            UpdateTooltipPosition();
        }
        void LateUpdate()
        {
            UpdateTooltipPosition();
        }

        void UpdateTooltipPosition()
        {
            if (focusedButton == null)
            {
                return;
            }
            var focusedTooltipRectTransform = (RectTransform)focusedButton.tooltip.transform;
            var focusedCardRectTransform = (RectTransform)focusedButton.cardUI.transform;
            focusedTooltipRectTransform.anchorMin = focusedCardRectTransform.anchorMin;
            focusedTooltipRectTransform.anchorMax = focusedCardRectTransform.anchorMax;
            focusedTooltipRectTransform.pivot = focusedCardRectTransform.pivot;
            focusedTooltipRectTransform.sizeDelta = focusedCardRectTransform.sizeDelta;

            Vector3 sourceWorldPos = focusedCardRectTransform.position;

            RectTransform targetParent = (RectTransform)focusedTooltipRectTransform.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                targetParent,
                RectTransformUtility.WorldToScreenPoint(null, sourceWorldPos),
                null,
                out Vector2 localPos
            );
            var offset = new Vector2(900, -550);
            if (focusedButton.tooltipSide == TooltipSide.Left)
            {
                offset = new Vector2(1020, -550);
            }
            focusedTooltipRectTransform.anchoredPosition = localPos + offset;
        }
    }
}
