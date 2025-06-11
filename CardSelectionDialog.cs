using BepInEx;
using ShinyShoe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using BepInEx.Configuration;
using TMPro;
using I2.Loc;
using System.Collections;

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
        internal RunSetupScreen SetupScreen { get; set; }
        internal static CardSelectionDialog Instance { get; set; }
        internal CardButtonUI focusedButton;

        internal ClassData selectedFaction;
        internal CollectableRarity selectedRarity = CollectableRarity.Common;
        internal CardType selectedType = CardType.Monster;

        internal readonly Dictionary<string, ClassData> CardFactionOptions = new Dictionary<string, ClassData>();
        internal readonly Dictionary<string, CollectableRarity> CardRarityOptions = new Dictionary<string, CollectableRarity>();
        internal readonly Dictionary<string, CardType> CardTypeOptions = new Dictionary<string, CardType>();
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

            Instance = this;
            name = nameof(CardSelectionDialog);
            dialog = GetComponentInChildren<ScreenDialog>();
            scrollRect = GetComponentInChildren<ScrollRect>();


            Transform ogCloseTr = transform.Find("Dialog").Find("CloseButton");
            DestroyImmediate(ogCloseTr.GetComponent<GameUISelectableButton>());

            var instructions = dialog.transform.Find("Content")
                                               .Find("Info and Preview")
                                               .Find("Instructions");

            title = instructions.Find("Instructions label")
                                .GetComponent<TextMeshProUGUI>();
            DestroyImmediate(title.GetComponent<Localize>());

            warning = instructions.Find("Warning layout")
                                  .Find("Warning label")
                                  .GetComponent<TextMeshProUGUI>();
            DestroyImmediate(warning.GetComponent<Localize>());

            var uiFooter = FindObjectOfType<UIFooter>();
            Transform swapChampButton = uiFooter.transform.Find("Swap Champion Button");

            Transform starterCardsButton = Instantiate(swapChampButton, uiFooter.transform);
            var starterLabel = starterCardsButton.Find("Label").GetComponent<TextMeshProUGUI>();
            DestroyImmediate(starterLabel.GetComponent<Localize>());
            starterLabel.fontSizeMin = starterLabel.fontSizeMax;
            starterLabel.fontSize = starterLabel.fontSizeMax;
            starterLabel.text = "Set Starter Cards";

            starterCardsButton.GetComponent<GameUISelectableButton>().onClick.AddListener(() =>
            {
                enabled = true;
                gameObject.SetActive(true);
            });

            title.text = "Choose any cards to customize your run.";
            warning.text = "Not every card here has been tested, enabled card will be sorted at the top. Re-open the menu to sort it, " +
                           "left click to increase the amount, right click to decrease.";

            var runSetupScreen = SetupScreen;
            if (runSetupScreen == null)
            {
                runSetupScreen = FindObjectOfType<RunSetupScreen>();
            }
            var contentGroup = scrollRect.content.GetComponent<GridLayoutGroup>();
            contentGroup.cellSize = new Vector2(312, 450);
            contentGroup.constraintCount = 4;

            DestroyImmediate(scrollRect.content.GetChild(0).gameObject);

            var clonedCardPreview = Instantiate(FindObjectOfType<CardUI>(), transform);

            layout = clonedCardPreview.gameObject.AddComponent<CardButtonUI>();
            layout.transform.localScale = Vector3.one;
            layout.gameObject.SetActive(false);

            closeButton = ogCloseTr.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeButton.transform.Find("Target Graphic").GetComponent<Image>();
            closeButton.gameObject.SetActive(true);
            closeButton.onClick = new Button.ButtonClickedEvent();
            closeButton.onClick.AddListener(delegate ()
            {
                SoundManager.PlaySfxSignal.Dispatch("UI_Click");
                Close();
            });

            Button resetButton = Instantiate(closeButton, transform);
            resetButton.transform.localPosition = new Vector3(0, -460, 0);
            DestroyImmediate(resetButton.targetGraphic.gameObject);
            DestroyImmediate(resetButton.transform.Find("Image close icon").gameObject);
            TextMeshProUGUI resetLabel = Instantiate(starterLabel, resetButton.transform);
            resetLabel.text = "Reset All Count";

            resetButton.targetGraphic = Instantiate(starterCardsButton.Find("Target Graphic").GetComponent<Image>(), resetButton.transform);
            resetButton.targetGraphic.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 112);
            resetButton.targetGraphic.transform.SetAsFirstSibling();
            resetButton.onClick.AddListener(() =>
            {
                SoundManager.PlaySfxSignal.Dispatch("UI_Click");
                foreach (CardButtonUI buttonUI in cardButtons)
                {
                    buttonUI.SetAmount(0);
                }
                ResetOrder();
            });

            RectTransform resetButtonHitboxTr = resetButton.transform.Find("Hitbox Invis").GetComponent<RectTransform>();
            resetButtonHitboxTr.localRotation = Quaternion.identity;
            resetButtonHitboxTr.sizeDelta = new Vector2(280, 0);

        }
        void LoadCards(CardType cardType, CollectableRarity rarity, ClassData faction)
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

            cards = cards.Where(card => AssetLoadingManager.IsOwnerInBaseGameOrInstalledDlc(card, AllGameManagers.Instance.GetSaveManager()));
            AssetLoadingManager.GetInst().LoadCardsForCompendium(cards, delegate (IAddressableAssetOwner owner)
            {
                return AssetLoadingManager.IsOwnerInBaseGameOrInstalledDlc(owner, AllGameManagers.Instance.GetSaveManager());
            }, delegate
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
            });
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
            GameUISelectableDropdown CreateDropdownWithOptions(List<string> options)
            {
                var dropdown = Instantiate(FindObjectOfType<GameUISelectableDropdown>(true), preview);
                dropdown.SetOptions(options);
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
            foreach (var faction in factions)
            {
                switch (faction)
                {
                    case "Clanless":
                        CardFactionOptions[faction] = null;
                        break;
                    default:
                        CardFactionOptions[faction] = Plugin.GetAllGameData().GetAllClassDatas()
                                                                             .First(data => data.Cheat_GetNameEnglish() == faction);
                        break;
                }
            }
            var cardTypes = new List<string>
            {
                "All",
                "Unit",
                "Spell",
                "Equipment",
                "Room",
                "Blight",
                "Scourge",
            };
            foreach (var cardType in cardTypes)
            {
                switch (cardType)
                {
                    case "All":
                        CardTypeOptions[cardType] = (CardType)(-9999);
                        break;
                    case "Unit":
                        CardTypeOptions[cardType] = CardType.Monster;
                        break;
                    case "Spell":
                        CardTypeOptions[cardType] = CardType.Spell;
                        break;
                    case "Equipment":
                        CardTypeOptions[cardType] = CardType.Equipment;
                        break;
                    case "Room":
                        CardTypeOptions[cardType] = CardType.TrainRoomAttachment;
                        break;
                    case "Blight":
                        CardTypeOptions[cardType] = CardType.Blight;
                        break;
                    case "Scourge":
                        CardTypeOptions[cardType] = CardType.Junk;
                        break;
                }
            }
            var cardRarities = new List<string>
            {
                "All",
                "Champion",
                "Common",
                "Uncommon",
                "Rare",
            };
            foreach (var cardRarity in cardRarities)
            {
                switch (cardRarity)
                {
                    case "All":
                        CardRarityOptions[cardRarity] = (CollectableRarity)(-9999);
                        break;
                    case "Champion":
                        CardRarityOptions[cardRarity] = CollectableRarity.Champion;
                        break;
                    case "Common":
                        CardRarityOptions[cardRarity] = CollectableRarity.Common;
                        break;
                    case "Uncommon":
                        CardRarityOptions[cardRarity] = CollectableRarity.Uncommon;
                        break;
                    case "Rare":
                        CardRarityOptions[cardRarity] = CollectableRarity.Rare;
                        break;
                }
            }
            var factionDropdown = CreateDropdownWithOptions(factions);
            var cardTypeDropdown = CreateDropdownWithOptions(cardTypes);
            var cardRarityDropdown = CreateDropdownWithOptions(cardRarities);
            yield return null;
            SetupDropdownButtonListeners(factionDropdown);
            SetupDropdownButtonListeners(cardTypeDropdown);
            SetupDropdownButtonListeners(cardRarityDropdown);

            factionDropdown.optionChosenSignal.AddListener(delegate (int index, string optionName)
            {
                selectedFaction = CardFactionOptions[optionName];
                LoadCards(selectedType, selectedRarity, selectedFaction);
            });

            cardTypeDropdown.optionChosenSignal.AddListener(delegate (int index, string optionName)
            {
                selectedType = CardTypeOptions[optionName];
                LoadCards(selectedType, selectedRarity, selectedFaction);
            });

            cardRarityDropdown.optionChosenSignal.AddListener(delegate (int index, string optionName)
            {
                selectedRarity = CardRarityOptions[optionName];
                LoadCards(selectedType, selectedRarity, selectedFaction);
            });

            factionDropdown.SetIndex(1);
            cardTypeDropdown.SetIndex(1);
            cardRarityDropdown.SetIndex(0);
            selectedFaction = CardFactionOptions["Banished"];
            selectedType = CardTypeOptions["Unit"];
            selectedRarity = CardRarityOptions["All"];

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
        void UpdateTooltipPosition()
        {
            if (focusedButton == null)
            {
                return;
            }
            var focusedTooltipRectTransform = (RectTransform)focusedButton.tooltip.transform;
            var focusedCardRectTransform = (RectTransform)focusedButton.cardUI.transform;
            focusedTooltipRectTransform.anchoredPosition = focusedCardRectTransform.anchoredPosition;
            focusedTooltipRectTransform.anchorMin = focusedCardRectTransform.anchorMin;
            focusedTooltipRectTransform.anchorMax = focusedCardRectTransform.anchorMax;
            focusedTooltipRectTransform.pivot = focusedCardRectTransform.pivot;
            focusedTooltipRectTransform.sizeDelta = focusedCardRectTransform.sizeDelta;

            Vector3 sourceWorldPos = focusedCardRectTransform.position;

            RectTransform targetParent = focusedTooltipRectTransform.parent as RectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                targetParent,
                RectTransformUtility.WorldToScreenPoint(null, sourceWorldPos),
                null,
                out Vector2 localPos
            );
            var offset = new Vector2(900, -500);
            if (focusedButton.tooltipSide == TooltipSide.Left)
            {
                offset = new Vector2(1000, -500);
            }
            focusedTooltipRectTransform.anchoredPosition = localPos + offset;
        }
        void LateUpdate()
        {
            if (focusedButton == null)
            {
                return;
            }
            UpdateTooltipPosition();
        }
    }
}
