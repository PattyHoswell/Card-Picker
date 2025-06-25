using BepInEx;
using HarmonyLib;
using ShinyShoe;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using BepInEx.Configuration;

namespace Patty_CardPicker_MOD
{
    [RequireComponent(typeof(RectTransform))]
    internal class CardButtonUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IInitializePotentialDragHandler
    {
        bool isHovering;
        bool initialized;
        CardState cardState;
        internal CardData Data { get; private set; }
        internal CardUI cardUI;
        internal CardSelectionDialog selectionDialog;
        internal CardTooltipContainer tooltip;
        internal TooltipSide tooltipSide;
        internal TextMeshProUGUI CountLabel;
        internal Dictionary<Transform, Vector3> transformsToReposition = new Dictionary<Transform, Vector3>();
        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovering = true;
            selectionDialog.SetFocusCard(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovering = false;
            cardUI.ResetFocus();
            cardUI.ResetFocusedScale();
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (isHovering && selectionDialog.scrollRect != null)
            {
                selectionDialog.scrollRect.OnInitializePotentialDrag(eventData);
                eventData.useDragThreshold = true;
            }
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                SetAmount((int)Plugin.Entries[Data].BoxedValue + 1);
                SoundManager.PlaySfxSignal.Dispatch("UI_Click");
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                SoundManager.PlaySfxSignal.Dispatch("UI_Cancel");
                SetAmount((int)Plugin.Entries[Data].BoxedValue - 1);
            }
        }
        void Setup()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            cardUI = GetComponent<CardUI>();
            gameObject.AddComponent<Image>().color = Color.clear;
            tooltip = (CardTooltipContainer)AccessTools.Field(typeof(CardUI), "cardTooltipsUI")
                                                       .GetValue(cardUI);

        }
        internal void Set(CardData data, CardSelectionDialog cardSelectionDialog)
        {
            Setup();
            name = data.GetName();
            Data = data;
            selectionDialog = cardSelectionDialog;
            cardState = new CardState(Data, AllGameManagers.Instance.GetSaveManager(), true);
            cardState.RefetchAssets(Data);
            cardState.RefreshCardBodyTextLocalization();
            cardUI.SetCardVisibleAndInteractable();
            cardUI.SetUIState(CardUI.CardUIState.Screen);
            cardUI.ApplyStateToUI(cardState, null, null, null, null, AllGameManagers.Instance.GetSaveManager(), AllGameManagers.Instance.GetSaveManager().GetMastery(cardState));
            tooltipSide = transform.GetSiblingIndex() % 4 >= 2 ? TooltipSide.Left : TooltipSide.Right;
            cardUI.SetTooltipSide(tooltipSide);

            var cardFront = transform.Find("CardCanvas")
                                     .Find("CardUIContainer")
                                     .Find("Card front");

            var countRoot = cardFront.Find("Count root");
            CountLabel = countRoot.Find("Count label").GetComponent<TextMeshProUGUI>();
            if (Plugin.Entries.TryGetValue(Data, out ConfigEntry<int> entry) && (int)entry.BoxedValue > 0)
            {
                SetAmount((int)Plugin.Entries[Data].BoxedValue);
            }
            SetPositions();
        }

        internal void SetAmount(int amount)
        {
            Plugin.Entries[Data].BoxedValue = Mathf.Max(0, amount);
            CountLabel.text = $"x{Plugin.Entries[Data].BoxedValue}";

            if ((int)Plugin.Entries[Data].BoxedValue <= 0)
            {
                CountLabel.transform.parent.gameObject.SetActive(false);
            }
            else if (!CountLabel.transform.parent.gameObject.activeSelf)
            {
                CountLabel.transform.parent.gameObject.SetActive(true);
            }
        }

        void SetPositions()
        {
            var cardFront = transform.Find("CardCanvas")
                                     .Find("CardUIContainer")
                                     .Find("Card front");

            var countRoot = cardFront.Find("Count root");
            transformsToReposition[countRoot] = new Vector3(108f, -80f, 0);

            var cardFrame = cardFront.Find("Card Frame UI");
            var statsIcon = cardFrame.Find("Stat icons");
            transformsToReposition[statsIcon] = new Vector3(22f, -496f, 0);

            var banner = cardFrame.Find("Banner unit");
            transformsToReposition[banner] = new Vector3(-128f, 229f, 0);

            var abilityArea = statsIcon.Find("AbilityArea");
            transformsToReposition[abilityArea] = new Vector3(-170, 288f, 0);

            var healthArea = statsIcon.Find("HealthArea");
            transformsToReposition[healthArea] = new Vector3(133f, 288f, 0);

            var ember = cardFrame.Find("Ember Pieces");
            transformsToReposition[ember] = new Vector3(-137f, 228f, 0);
        }
        void LateUpdate()
        {
            foreach (KeyValuePair<Transform, Vector3> pair in transformsToReposition)
            {
                pair.Key.localPosition = pair.Value;
            }
        }
    }
}
