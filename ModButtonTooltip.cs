using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Patty_CardPicker_MOD
{
    [RequireComponent(typeof(RectTransform))]
    internal class ModButtonTooltip : MonoBehaviour
    {
        internal ModOptionDialog optionDialog;
        bool initialized;
        Image bg;
        LayoutElement layoutElement;
        ContentSizeFitter sizeFitter;
        TextMeshProUGUI label;
        internal ModButtonUI focusedModButton;
        private void Start()
        {
            name = nameof(ModButtonTooltip);
            Initialize();
        }

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            gameObject.layer = LayerMask.NameToLayer("UI");

            bg = gameObject.AddComponent<Image>();
            bg.rectTransform.pivot = new Vector2(0.5f, 1f);
            bg.raycastTarget = false;
            bg.type = Image.Type.Sliced;
            bg.sprite = ModOptionDialog.tooltipSprite;

            gameObject.AddComponent<VerticalLayoutGroup>();

            layoutElement = gameObject.AddComponent<LayoutElement>();
            layoutElement.minWidth = 350f;
            layoutElement.preferredWidth = 350f;

            sizeFitter = gameObject.AddComponent<ContentSizeFitter>();
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var runSetupScreen = (RunSetupScreen)AllGameManagers.Instance.GetScreenManager().GetScreen(ScreenName.RunSetup);
            var championNameRoot = (TextMeshProUGUI)AccessTools.Field(typeof(RunSetupScreen), "championNameLabel").GetValue(runSetupScreen);
            label = Instantiate(championNameRoot, transform);
            label.fontSizeMax = 30;
            label.fontSizeMin = label.fontSizeMax;
            label.fontSize = label.fontSizeMax;
            label.enableAutoSizing = false;
            label.enableWordWrapping = true;
            label.margin = new Vector4(10, 5, 10, 5);
        }

        internal void Open(ModButtonUI modButton)
        {
            Initialize();
            focusedModButton = modButton;
            label.text = focusedModButton.info.entry.Description.Description;
            SoundManager.PlaySfxSignal.Dispatch("UI_HighlightLight");
            gameObject.SetActive(true);
        }
        internal void Close()
        {
            SoundManager.PlaySfxSignal.Dispatch("UI_CancelLight");
            gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            UpdateTooltipPosition();
        }

        void UpdateTooltipPosition()
        {
            if (focusedModButton == null)
            {
                return;
            }
            var tooltipRT = (RectTransform)transform;
            var modButtonRT = (RectTransform)focusedModButton.transform;
            tooltipRT.anchorMin = modButtonRT.anchorMin;
            tooltipRT.anchorMax = modButtonRT.anchorMax;
            tooltipRT.sizeDelta = modButtonRT.sizeDelta;

            Vector3 sourceWorldPos = modButtonRT.position;

            RectTransform targetParent = (RectTransform)optionDialog.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                targetParent,
                RectTransformUtility.WorldToScreenPoint(null, sourceWorldPos),
                null,
                out Vector2 localPos
            );
            var offset = new Vector2(950, -570);
            tooltipRT.anchoredPosition = localPos + offset;
        }
    }
}
