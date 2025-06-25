using BepInEx;
using I2.Loc;
using ShinyShoe;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Patty_CardPicker_MOD
{
    internal class ModOptionDialog : MonoBehaviour
    {
        ScreenDialog dialog;
        internal ModButtonUI layout;
        Button closeButton;
        List<ModButtonUI> modButtons = new List<ModButtonUI>();
        TextMeshProUGUI title, warning;
        bool initialized;
        internal ScrollRect scrollRect;
        internal CardSelectionDialog cardSelectionDialog;
        internal ModButtonTooltip modTooltip;
        internal static Sprite tooltipSprite;
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

            transform.SetAsLastSibling();
            dialog.SetActive(true, gameObject);
        }

        internal void Close()
        {
            Setup();
            dialog.SetActive(false, gameObject);
            cardSelectionDialog.bg.color = CardSelectionDialog.BGColor;
        }

        // Many failsafe check in case the menu is trying to open without being initialized
        internal void Setup()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;

            DestroyImmediate(gameObject.GetComponent<MutatorSelectionDialog>());
            InitializeBasicComponents();
            SetupDialogAndScrollView();
            SetupTitleAndWarning();
            SetupGridLayout();
            SetupModButtonTemplate();
            SetupCloseButton();

            foreach (ModButtonInfo buttonInfo in CreateModButtons())
            {
                var modButton = Instantiate(layout, scrollRect.content);
                modButton.modDialog = this;
                modButton.Set(buttonInfo);
                modButtons.Add(modButton);
            }
            ResetOrder();
            modTooltip = CreateTooltip();
        }

        private ModButtonTooltip CreateTooltip()
        {
            if (tooltipSprite == null)
            {
                var tooltip = FindObjectOfType<TooltipUI>(true);
                if (tooltip == null)
                {
                    return null;
                }
                var tooltipBG = tooltip.transform.GetChild(0).GetComponent<Image>();
                tooltipSprite = Sprite.Create((Texture2D)tooltipBG.mainTexture, new Rect(3, 389, 256, 60), new Vector2(128, 30), 100, 0, SpriteMeshType.Tight, Vector4.one * 10);
            }
            if (tooltipSprite == null)
            {
                return null;
            }

            var parent = new GameObject("TooltipContent");
            parent.transform.SetParent(transform, false);
            parent.SetActive(false);
            var modButtonTooltip = parent.AddComponent<ModButtonTooltip>();
            modButtonTooltip.optionDialog = this;
            return modButtonTooltip;
        }

        private void ResetOrder()
        {
            modButtons = modButtons.OrderBy(modButton => modButton.info.text).ToList();
            for (var i = 0; i <  modButtons.Count; i++)
            {
                var button = modButtons[i];
                button.transform.SetSiblingIndex(i);
            }
        }

        private IEnumerable<ModButtonInfo> CreateModButtons()
        {
            yield return new ModButtonInfo(Plugin.RemoveStartingDeck, 30);
            yield return new ModButtonInfo(Plugin.EnableOnChallenge, 25);
            yield return new ModButtonInfo(Plugin.ShowAllCards, 25, delegate (bool toggled)
            {
                if (cardSelectionDialog == null)
                {
                    return;
                }
                cardSelectionDialog.RefreshCards();
            });
        }

        private void InitializeBasicComponents()
        {
            name = nameof(ModOptionDialog);
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
            title.text = "Change the mod options.";

            warning = instructions.Find("Warning layout/Warning label").GetComponent<TextMeshProUGUI>();
            DestroyImmediate(warning.GetComponent<Localize>());
            warning.text = "Some options might requires you to re-open the menu for it to take effect. " +
                           "Hover into the option to view the description.";
        }

        private void SetupGridLayout()
        {
            var contentGroup = scrollRect.content.GetComponent<GridLayoutGroup>();
            contentGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            contentGroup.cellSize = new Vector2(434, 100);
            contentGroup.constraintCount = 3;
        }

        private void SetupModButtonTemplate()
        {
            layout = ModButtonUI.CreateModButtonUI(transform, new ModButtonInfo("Placeholder"));
            layout.name = $"{nameof(ModButtonUI)} Prefab";
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
    }
}
