using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace ChemistryEditor.UI
{
    /// <summary>
    /// 移动端 UI 管理器
    /// 在移动平台上自动创建触摸友好的 UI 按钮
    /// </summary>
    public class MobileUIManager : MonoBehaviour
    {
        [Header("UI 设置")]
        [SerializeField] private bool autoCreateUI = true;
        [SerializeField] private int uiScaleFactor = 2;  // UI 缩放因子（移动端需要更大的按钮）

        [Header("颜色")]
        [SerializeField] private Color selectedColor = new Color(0.2f, 0.6f, 1f);
        [SerializeField] private Color normalColor = new Color(0.3f, 0.3f, 0.3f);

        // UI 引用
        private GameObject mobileUIPanel;
        private Dictionary<int, Button> elementButtons = new Dictionary<int, Button>();
        private Dictionary<int, Button> bondTypeButtons = new Dictionary<int, Button>();
        private Button undoButton;
        private Button redoButton;
        private Button deleteButton;
        private Button rotateButton;

        // 当前选择
        private int selectedElementIndex = 0;
        private int selectedBondType = 0;

        // 引用
        private MobileInputHandler inputHandler;
        private UIManager uiManager;
        private ChemistryEditor chemistryEditor;

        private void Start()
        {
            inputHandler = FindObjectOfType<MobileInputHandler>();
            uiManager = FindObjectOfType<UIManager>();
            chemistryEditor = FindObjectOfType<ChemistryEditor>();

#if UNITY_IOS || UNITY_ANDROID
            if (autoCreateUI)
            {
                CreateMobileUI();
            }
#endif
        }

        /// <summary>
        /// 创建移动端 UI
        /// </summary>
        private void CreateMobileUI()
        {
            // 创建 Canvas（如果不存在）
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("MobileCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // 创建主面板
            mobileUIPanel = CreatePanel(canvas.transform, "MobileUIPanel");
            RectTransform panelRect = mobileUIPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(1, 0.25f);  // 底部 25% 区域
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // 创建元素选择按钮
            CreateElementButtons(mobileUIPanel.transform);

            // 创建化学键类型按钮
            CreateBondTypeButtons(mobileUIPanel.transform);

            // 创建操作按钮
            CreateActionButtons(mobileUIPanel.transform);

            // 初始选择
            UpdateElementButtonSelection(selectedElementIndex);
            UpdateBondTypeButtonSelection(selectedBondType);
        }

        /// <summary>
        /// 创建元素选择按钮
        /// </summary>
        private void CreateElementButtons(Transform parent)
        {
            // 元素列表（从 AtomManager 获取）
            string[] elements = { "H", "He", "Li", "Be", "B", "C", "N", "O", "F", "Ne",
                                 "Na", "Mg", "Al", "Si", "P", "S", "Cl", "Ar" };

            GameObject buttonGrid = CreateGrid(parent, "ElementButtons", 9, 2);  // 9列2行

            for (int i = 0; i < elements.Length && i < 18; i++)
            {
                Button btn = CreateButton(buttonGrid.transform, $"Element_{i}", elements[i]);
                int index = i;  // 闭包捕获
                btn.onClick.AddListener(() => OnElementButtonClicked(index));
                elementButtons[i] = btn;
            }
        }

        /// <summary>
        /// 创建化学键类型按钮
        /// </summary>
        private void CreateBondTypeButtons(Transform parent)
        {
            string[] bondTypes = { "单键", "双键", "三键", "虚线" };

            GameObject buttonGrid = CreateGrid(parent, "BondTypeButtons", 4, 1);  // 4列1行

            for (int i = 0; i < bondTypes.Length; i++)
            {
                Button btn = CreateButton(buttonGrid.transform, $"BondType_{i}", bondTypes[i]);
                int index = i;  // 闭包捕获
                btn.onClick.AddListener(() => OnBondTypeButtonClicked(index));
                bondTypeButtons[i] = btn;
            }
        }

        /// <summary>
        /// 创建操作按钮
        /// </summary>
        private void CreateActionButtons(Transform parent)
        {
            GameObject actionPanel = CreatePanel(parent, "ActionButtons");
            HorizontalLayoutGroup hlg = actionPanel.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10;
            hlg.padding = new RectOffset(10, 10, 5, 5);
            hlg.childAlignment = TextAnchor.MiddleCenter;

            // 撤销按钮
            undoButton = CreateButton(actionPanel.transform, "UndoButton", "撤销");
            undoButton.onClick.AddListener(() =>
            {
                if (chemistryEditor != null)
                    chemistryEditor.Undo();
            });

            // 重做按钮
            redoButton = CreateButton(actionPanel.transform, "RedoButton", "重做");
            redoButton.onClick.AddListener(() =>
            {
                if (chemistryEditor != null)
                    chemistryEditor.Redo();
            });

            // 删除按钮
            deleteButton = CreateButton(actionPanel.transform, "DeleteButton", "删除");
            deleteButton.onClick.AddListener(() =>
            {
                // TODO: 删除选中的物体
            });

            // 旋转按钮
            rotateButton = CreateButton(actionPanel.transform, "RotateButton", "旋转");
            rotateButton.onClick.AddListener(() =>
            {
                // TODO: 启动化学键旋转模式
            });
        }

        /// <summary>
        /// 元素按钮点击
        /// </summary>
        private void OnElementButtonClicked(int elementIndex)
        {
            selectedElementIndex = elementIndex;
            UpdateElementButtonSelection(elementIndex);

            if (inputHandler != null)
                inputHandler.SetSelectedElement(elementIndex);
        }

        /// <summary>
        /// 化学键类型按钮点击
        /// </summary>
        private void OnBondTypeButtonClicked(int bondType)
        {
            selectedBondType = bondType;
            UpdateBondTypeButtonSelection(bondType);

            if (inputHandler != null)
                inputHandler.SetBondType(bondType);
        }

        /// <summary>
        /// 更新元素按钮选择状态
        /// </summary>
        private void UpdateElementButtonSelection(int selectedIndex)
        {
            foreach (var kvp in elementButtons)
            {
                ColorBlock colors = kvp.Value.colors;
                colors.normalColor = (kvp.Key == selectedIndex) ? selectedColor : normalColor;
                kvp.Value.colors = colors;
            }
        }

        /// <summary>
        /// 更新化学键类型按钮选择状态
        /// </summary>
        private void UpdateBondTypeButtonSelection(int selectedIndex)
        {
            foreach (var kvp in bondTypeButtons)
            {
                ColorBlock colors = kvp.Value.colors;
                colors.normalColor = (kvp.Key == selectedIndex) ? selectedColor : normalColor;
                kvp.Value.colors = colors;
            }
        }

        #region UI 创建辅助方法

        private GameObject CreatePanel(Transform parent, string name)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent);
            RectTransform rect = panel.AddComponent<RectTransform>();
            panel.AddComponent<CanvasRenderer>();
            Image img = panel.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            return panel;
        }

        private GameObject CreateGrid(Transform parent, string name, int columns, int rows)
        {
            GameObject grid = new GameObject(name);
            grid.transform.SetParent(parent);

            RectTransform rect = grid.AddComponent<RectTransform>();

            GridLayoutGroup gridLayout = grid.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(60 * uiScaleFactor, 40 * uiScaleFactor);
            gridLayout.spacing = new Vector2(5, 5);
            gridLayout.padding = new RectOffset(10, 10, 5, 5);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = columns;

            return grid;
        }

        private Button CreateButton(Transform parent, string name, string text)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent);

            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(60 * uiScaleFactor, 40 * uiScaleFactor);

            btnObj.AddComponent<CanvasRenderer>();
            Image img = btnObj.AddComponent<Image>();
            img.color = normalColor;

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = new Color(0.5f, 0.5f, 0.5f);
            colors.pressedColor = selectedColor;
            btn.colors = colors;

            // 添加文本
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text txt = textObj.AddComponent<Text>();
            txt.text = text;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.fontSize = 14 * uiScaleFactor;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            return btn;
        }

        #endregion
    }
}
