using BepInEx.Configuration;
using HarmonyLib;
using ShinyShoe;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Patty_CardPicker_MOD
{
    [RequireComponent(typeof(RectTransform))]
    internal class ModButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IInitializePotentialDragHandler
    {
        internal ModButtonInfo info;
        internal GameUISelectableToggle toggleButton;
        internal TextMeshProUGUI label;
        internal ModOptionDialog modDialog;
        bool isHovering;
        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovering = true;
            modDialog.modTooltip.Open(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovering = false;
            modDialog.modTooltip.Close();
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (isHovering && modDialog != null && modDialog.scrollRect != null)
            {
                modDialog.scrollRect.OnInitializePotentialDrag(eventData);
                eventData.useDragThreshold = true;
            }
        }

        internal void CheckNullComponent()
        {
            if (label == null)
            {
                label = transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            }
            if (toggleButton == null)
            {
                toggleButton = transform.GetChild(2).GetComponent<GameUISelectableToggle>();
            }
        }
        internal void Set(ModButtonInfo info)
        {
            if (info.entry == null)
            {
                Plugin.LogSource.LogError($"Please do not call {nameof(ModButtonUI)}.{nameof(Set)} without setting an entry. " +
                                          $"{nameof(ModButtonInfo)} without an entry is only intended for a placeholder");
                return;
            }
            CheckNullComponent();
            name = info.entry.Definition.Key;
            this.info = info;
            label.text = info.text;
            label.fontSizeMax = info.fontSize;
            label.fontSizeMin = info.fontSize;
            label.fontSize = info.fontSize;

            toggleButton.onClick.AddListener(() =>
            {
                var toggled = toggleButton.Toggle();
                info.entry.Value = toggled;
                info.onToggle?.Invoke(toggled);
            });

            info.entry.SettingChanged += Entry_SettingChanged;
            toggleButton.isOn = info.entry.Value;
            gameObject.SetActive(true);
        }

        private void OnDestroy()
        {
            if (info.entry != null)
            {
                info.entry.SettingChanged -= Entry_SettingChanged;
            }
        }

        private void Entry_SettingChanged(object sender, EventArgs e)
        {
            var eventArgs = (SettingChangedEventArgs)e;
            toggleButton.isOn = (bool)eventArgs.ChangedSetting.BoxedValue;
            info.onToggle?.Invoke(toggleButton.isOn);
        }

        internal static ModButtonUI CreateModButtonUI(Transform parent, ModButtonInfo info)
        {
            var runSetupScreen = AllGameManagers.Instance.GetScreenManager().GetScreen(ScreenName.RunSetup) as RunSetupScreen;
            if (runSetupScreen == null)
            {
                Plugin.LogSource.LogError($"This is intended to be created on {nameof(RunSetupScreen)} menu");
                return null;
            }
            var championNameRoot = (GameObject)AccessTools.Field(typeof(RunSetupScreen), "championNameRoot").GetValue(runSetupScreen);
            var background = Instantiate(championNameRoot.GetComponentInChildren<Image>(), parent);
            background.transform.localPosition = new Vector2(500, -460);
            background.GetComponent<RectTransform>().pivot = Vector2.one * 0.5f;

            var label = background.GetComponentInChildren<TextMeshProUGUI>();
            label.name = "Option Name";
            label.enableAutoSizing = false;
            label.alignment = TextAlignmentOptions.Left;
            label.text = info.text;
            label.rectTransform.pivot = new Vector2(0, 0.5f);
            label.rectTransform.localPosition = new Vector2(-165, 6);
            label.rectTransform.anchoredPosition = new Vector2(50, 6);
            label.fontSizeMax = info.fontSize;
            label.fontSizeMin = info.fontSize;
            label.fontSize = info.fontSize;

            var modButtonUI = background.gameObject.AddComponent<ModButtonUI>();
            modButtonUI.label = label;
            modButtonUI.info.onToggle = info.onToggle;

            var bg = new GameObject("Target Graphic").AddComponent<Image>();
            bg.gameObject.layer = LayerMask.NameToLayer("UI");
            bg.transform.SetParent(modButtonUI.transform, false);
            bg.rectTransform.pivot = new Vector2(0.5f, 0.42f);
            bg.rectTransform.sizeDelta = new Vector2(400, 88);
            bg.color = Color.clear;

            var toggleButton = Instantiate(FindObjectOfType<GameUISelectableToggle>(true), background.transform);
            toggleButton.gameObject.SetActive(true);
            toggleButton.name = "Option Toggle";
            toggleButton.onClick = new Button.ButtonClickedEvent();
            toggleButton.transform.GetChild(0).gameObject.SetActive(false);
            toggleButton.transform.GetChild(2).gameObject.SetActive(false);
            toggleButton.transform.localPosition = new Vector2(-106, 55);
            modButtonUI.toggleButton = toggleButton;

            var toggleRT = toggleButton.GetComponent<RectTransform>();
            toggleRT.anchoredPosition = new Vector2(toggleRT.anchoredPosition.x, 7);
            toggleRT.pivot = new Vector2(0.2f, 1);
            return modButtonUI;
        }
    }
}
