﻿using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 对象选择器窗口
/// </summary>
public class ObjectSelectorWindow : EditorWindow
{
    private class Styles
    {
        public GUIStyle smallStatus = "ObjectPickerSmallStatus";
        public GUIStyle largeStatus = "ObjectPickerLargeStatus";
        public GUIStyle toolbarBack = "ObjectPickerToolbar";
        public GUIStyle tab = "ObjectPickerTab";
        public GUIStyle bottomResize = "WindowBottomResize";
        public GUIStyle background = "ObjectPickerBackground";
        public GUIStyle previewBackground = "PopupCurveSwatchBackground";
        public GUIStyle previewTextureBackground = "ObjectPickerPreviewBackground";

        public GUIStyle resultsGridLabel = "ProjectBrowserGridLabel";
        public GUIStyle resultsLabel = "PR Label";
        public GUIStyle iconAreaBg = "ProjectBrowserIconAreaBg";
        public GUIStyle previewBg = "ProjectBrowserPreviewBg";

        public GUIStyle preButton = "preButton";
        public GUIStyle preToolbar = "preToolbar";
        public GUIStyle dragHandle = "RL DragHandle";
    }

    private class BuiltinRes
    {
        public string name;
        public Texture icon;
        public string path;
    }

    private Styles m_Styles;
    private float m_ToolbarHeight = 44f;
    private float m_PreviewSize = 101f;
    private float m_TopSize;
    private string m_SearchFilter;
    private bool m_FocusSearchFilter;
    private int m_LastSelectedIdx;
    private BuiltinRes[] m_CurrentBuiltinResources;
    private BuiltinRes[] m_ActiveBuiltinList;
    private string m_FolderPath;
    private UnityEngine.Object m_LastSelectedObject;
    private Texture2D m_LastSelectedObjectIcon;
    private Action<UnityEngine.Object> m_ItemSelectedCallback;
    private BuiltinRes m_NoneBuiltinRes;
    private bool m_ShowNoneItem;
    private AudioClip m_AudioClip;
    private Vector2 m_ScrollPosition;
    private EditorCache m_EditorCache;
    private SerializedProperty m_CacheProperty;
    [SerializeField]
    private PreviewResizer m_PreviewResizer = new PreviewResizer();
    private static ObjectSelectorWindow s_SharedObjectSelector;

    public static ObjectSelectorWindow get
    {
        get
        {
            if (s_SharedObjectSelector == null)
            {
                UnityEngine.Object[] array = Resources.FindObjectsOfTypeAll(typeof(ObjectSelectorWindow));
                if (array != null && array.Length > 0)
                {
                    s_SharedObjectSelector = (ObjectSelectorWindow)array[0];
                }
                if (s_SharedObjectSelector == null)
                {
                    s_SharedObjectSelector = CreateInstance<ObjectSelectorWindow>();
                }
            }
            return s_SharedObjectSelector;
        }
    }

    /// <summary>
    /// 列表项绘制区域
    /// </summary>
    private Rect listPosition
    {
        get { return new Rect(0f, m_ToolbarHeight, position.width, Mathf.Max(0f, m_TopSize - m_ToolbarHeight)); }
    }

    /// <summary>
    /// 完全文件夹路径
    /// </summary>
    private string folderFullPath
    {
        get { return Path.Combine(Application.dataPath, m_FolderPath.Length > 6 ? m_FolderPath.Substring(7) : String.Empty); }
    }

    /// <summary>
    /// 显示的列表项数量
    /// </summary>
    private int itemCount
    {
        get
        {
            int num2 = m_ActiveBuiltinList.Length;
            int num3 = (!m_ShowNoneItem) ? 0 : 1;
            return num2 + num3;
        }
    }

    /// <summary>
    /// 显示对象选择器
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="obj">初始的对象</param>
    /// <param name="itemSelectedCallback">列表项选中回调</param>
    /// <param name="folderPath">所属的文件夹路径</param>
    public static void ShowObjectPicker<T>(UnityEngine.Object obj, Action<UnityEngine.Object> itemSelectedCallback, string folderPath = "Assets") where T : UnityEngine.Object
    {
        Type typeFromHandle = typeof(T);
        get.Show(obj, typeFromHandle, null, itemSelectedCallback, folderPath);
    }

    public static void ShowObjectPicker(SerializedProperty property, Action<UnityEngine.Object> itemSelectedCallback, string folderPath = "Assets")
    {
        get.Show(null, null, property, itemSelectedCallback, folderPath);
    }

    public void Show(UnityEngine.Object obj, Type requiredType, SerializedProperty property, Action<UnityEngine.Object> itemSelectedCallback, string folderPath)
    {
        m_FolderPath = folderPath;
        if (!Directory.Exists(folderFullPath))
        {
            Debug.LogError(folderPath + " is not a Directory!");
            return;
        }
        m_CacheProperty = property;
        m_ItemSelectedCallback = itemSelectedCallback;
        InitIfNeeded();
        string requiredTypeName = string.Empty;
        if (property != null)
        {
            obj = property.objectReferenceValue;
            requiredTypeName = property.objectReferenceValue.GetType().Name;
        }
        else
        {
            requiredTypeName = requiredType.Name;
        }
        InitBuiltinList(requiredTypeName);
        titleContent = new GUIContent("Select " + requiredTypeName);
        m_FocusSearchFilter = true;
        m_ShowNoneItem = true;
        m_SearchFilter = String.Empty;
        ListItemFrame(obj, true);
        ShowAuxWindow();
    }

    /// <summary>
    /// 初始化所指定的文件夹路径里的对象列表
    /// </summary>
    /// <param name="requiredType"></param>
    private void InitBuiltinList(string requiredTypeName)
    {
        int lenFolderPath = m_FolderPath.Length + 1;
        List<BuiltinRes> builtinResList = new List<BuiltinRes>();
        string[] guids = AssetDatabase.FindAssets("t:" + requiredTypeName, new[] { m_FolderPath });
        foreach (var guid in guids)
        {
            BuiltinRes builtinRes = new BuiltinRes();
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            builtinRes.name = assetPath.Substring(lenFolderPath, assetPath.LastIndexOf('.') - lenFolderPath);
            builtinRes.icon = AssetDatabase.GetCachedIcon(assetPath);
            builtinRes.path = assetPath;
            builtinResList.Add(builtinRes);
        }

        m_CurrentBuiltinResources = m_ActiveBuiltinList = builtinResList.ToArray();
    }

    private void OnEnable()
    {
        m_PreviewResizer.Init("ObjectSelectorWindow");
        m_PreviewSize = m_PreviewResizer.GetPreviewSize();
    }

    private void OnDisable()
    {
        AudioClipGUI.Clear();
        m_ItemSelectedCallback = null;
        m_CurrentBuiltinResources = null;
        m_ActiveBuiltinList = null;
        m_LastSelectedObject = null;
        m_LastSelectedObjectIcon = null;
        m_AudioClip = null;
        if (s_SharedObjectSelector == this)
        {
            s_SharedObjectSelector = null;
        }
        if (m_EditorCache != null)
        {
            m_EditorCache.Dispose();
        }
    }

    private void OnGUI()
    {
        OnObjectListGUI();
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            Cancel();
        }
    }

    private void InitIfNeeded()
    {
        if (m_Styles == null)
        {
            m_Styles = new Styles();
        }
        if (m_NoneBuiltinRes == null)
        {
            m_NoneBuiltinRes = new BuiltinRes();
            m_NoneBuiltinRes.name = "None";
        }
        m_TopSize = position.height - m_PreviewSize;
    }

    private void Cancel()
    {
        Close();
        GUI.changed = true;
        GUIUtility.ExitGUI();
    }

    private void OnObjectListGUI()
    {
        InitIfNeeded();
        HandleKeyboard();
        GUI.BeginGroup(new Rect(0f, 0f, position.width, position.height), GUIContent.none);
        SearchArea();
        GridListArea();
        PreviewArea();
        GUI.EndGroup();
    }

    private void SearchArea()
    {
        GUI.Label(new Rect(0f, 0f, position.width, m_ToolbarHeight), GUIContent.none, m_Styles.toolbarBack);
        bool flag = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape;
        GUI.SetNextControlName("SearchFilter");
        string text = EditorGUIUtil.SearchField(new Rect(5f, 5f, position.width - 10f, 15f), m_SearchFilter);
        if (flag && Event.current.type == EventType.Used)
        {
            if (m_SearchFilter == string.Empty)
            {
                Cancel();
            }
            m_FocusSearchFilter = true;
        }
        if (text != m_SearchFilter || m_FocusSearchFilter)
        {
            m_SearchFilter = text;
            FilterSettingsChanged();
            Repaint();
        }
        if (m_FocusSearchFilter)
        {
            EditorGUI.FocusTextInControl("SearchFilter");
            m_FocusSearchFilter = false;
        }

        GUILayout.BeginArea(new Rect(0f, 26f, position.width, m_ToolbarHeight - 26f));
        GUILayout.BeginHorizontal();
        GUILayout.Toggle(true, m_FolderPath, m_Styles.tab);
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void GridListArea()
    {
        Rect totalRect = listPosition;
        float itemHeight = itemCount * 16f;
        Rect viewRect = new Rect(0f, 0f, 1f, itemHeight);
        GUI.Label(totalRect, GUIContent.none, m_Styles.iconAreaBg);
        m_ScrollPosition = GUI.BeginScrollView(totalRect, m_ScrollPosition, viewRect);

        int num = FirstVisibleRow(0f, m_ScrollPosition);
        if (num >= 0 && num < itemCount)
        {
            int num3 = num;
            int num4 = Math.Min(itemCount, 2147483647);
            float num5 = 16f;
            int num6 = (int)Math.Ceiling((double)(position.height / num5));
            num4 = Math.Min(num4, num3 + num6 * 1 + 1);
            DrawListInternal(num3, num4);
        }

        GUI.EndScrollView();

        if (m_LastSelectedObject && !m_LastSelectedObjectIcon && AssetPreview.IsLoadingAssetPreview(m_LastSelectedObject.GetInstanceID()))
        {
            m_LastSelectedObjectIcon = AssetPreview.GetAssetPreview(m_LastSelectedObject);
            Repaint();
        }
    }

    private void DrawListInternal(int beginIndex, int endIndex)
    {
        int num = beginIndex;
        int num2 = 0;
        if (m_ShowNoneItem)
        {
            if (beginIndex < 1)
            {
                DrawListItemInternal(ListItemCalcRect(num), m_NoneBuiltinRes, num);
                num++;
            }
            num2++;
        }

        if (m_ActiveBuiltinList.Length > 0)
        {
            int num4 = beginIndex - num2;
            num4 = Math.Max(num4, 0);
            int num5 = num4;
            while (num5 < m_ActiveBuiltinList.Length && num <= endIndex)
            {
                DrawListItemInternal(ListItemCalcRect(num), m_ActiveBuiltinList[num5], num);
                num++;
                num5++;
            }
        }
    }

    private void DrawListItemInternal(Rect rect4, BuiltinRes builtinResource, int itemIdx)
    {
        Event current = Event.current;
        float num5 = 18f;
        Rect rect5 = new Rect(num5, rect4.y, rect4.width - num5, rect4.height);
        bool selected = false;
        bool focus = true;

        if (current.type == EventType.MouseDown)
        {
            if (current.button == 0 && rect4.Contains(current.mousePosition))
            {
                if (current.clickCount == 1)
                {
                    SetSelectedAssetByIdx(itemIdx);
                    current.Use();
                }
                else if (current.clickCount == 2)
                {
                    current.Use();
                    Close();
                    GUIUtility.ExitGUI();
                }
            }
        }
        else if (current.type == EventType.Repaint)
        {
            if (itemIdx == m_LastSelectedIdx)
            {
                m_Styles.resultsLabel.Draw(rect4, GUIContent.none, false, false, true, focus);
            }

            m_Styles.resultsLabel.Draw(rect5, builtinResource.name, false, false, selected, focus);
            Rect rect6 = rect5;
            rect6.width = 16f;
            rect6.x = 16f;
            if (builtinResource.icon != null)
            {
                GUI.DrawTexture(rect6, builtinResource.icon);
            }
        }
    }

    /// <summary>
    /// 每个项的矩形区域
    /// </summary>
    /// <param name="itemIdx"></param>
    /// <returns></returns>
    private Rect ListItemCalcRect(int itemIdx)
    {
        return new Rect(0f, itemIdx * 16f, listPosition.width, 16f);
    }

    private void PreviewArea()
    {
        GUI.Box(new Rect(0f, m_TopSize, position.width, m_PreviewSize), string.Empty, m_Styles.previewBackground);

        if (m_EditorCache == null)
        {
            m_EditorCache = new EditorCache(EditorFeatures.PreviewGUI);
        }
        UnityEngine.Object currentObject = m_LastSelectedObject;
        EditorWrapper editorWrapper = null;
        string text;
        if (currentObject != null)
        {
            editorWrapper = m_EditorCache[currentObject];
            string text2 = ObjectNames.NicifyVariableName(currentObject.GetType().Name);

            {
                text = currentObject.name + "\n" + text2;
            }
            text = text + "\n" + AssetDatabase.GetAssetPath(currentObject);
        }
        else
        {
            text = "None";
        }

        float toolbarHeight = 16f;
        float previewSize = m_PreviewSize - toolbarHeight;
        float num = 5f;
        Rect rect2 = new Rect(num, m_TopSize + num + toolbarHeight, previewSize - num * 2f, previewSize - num * 2f);
        Rect rect = new Rect(previewSize + 3f, m_TopSize + (previewSize - 75f) * 0.5f + toolbarHeight, position.width - previewSize - 3f - num, 75f);

        if (editorWrapper != null && editorWrapper.HasPreviewGUI())
        {
            Rect rect3 = new Rect(0f, m_TopSize, position.width, 16f);
            GUI.BeginGroup(rect3);
            Rect rect4 = EditorGUILayout.BeginHorizontal(m_Styles.preToolbar, GUILayout.Height(17f));

            if (m_PreviewResizer.GetExpandedBeforeDragging())
            {
                editorWrapper.OnPreviewSettings();
            }
            GUILayout.FlexibleSpace();

            Rect lastRect = GUILayoutUtility.GetLastRect();
            Rect rect5 = rect4;
            rect5.x = lastRect.x;
            rect5.y = rect5.y + (17f - m_Styles.dragHandle.fixedHeight) / 2f + 1f;
            rect5.height = m_Styles.dragHandle.fixedHeight;
            rect5.width = lastRect.width;
            if (Event.current.type == EventType.Repaint)
            {
                m_Styles.dragHandle.Draw(rect5, GUIContent.none, false, false, false, false);
            }

            EditorGUILayout.EndHorizontal();
            GUI.EndGroup();

            m_PreviewSize = this.m_PreviewResizer.ResizeHandle(base.position, 101f, 270f, 20f, lastRect);
            this.m_TopSize = base.position.height - this.m_PreviewSize;
            if (!this.m_PreviewResizer.GetExpanded())
            {
                return;
            }

            editorWrapper.OnInteractivePreviewGUI(rect2, m_Styles.previewTextureBackground);
        }
        else
        {
            DrawObjectIcon(rect2, m_LastSelectedObjectIcon);
        }
        if (EditorGUIUtility.isProSkin)
        {
            EditorGUI.DropShadowLabel(rect, text, m_Styles.smallStatus);
        }
        else
        {
            GUI.Label(rect, text, m_Styles.smallStatus);
        }
        //DrawPreviewTool();
		m_EditorCache.CleanupUntouchedEditors();
    }

    private void HandleKeyboard()
    {
        if (!GUI.enabled || Event.current.type != EventType.KeyDown)
        {
            return;
        }

        switch (Event.current.keyCode)
        {
            case KeyCode.UpArrow:
                if (m_LastSelectedIdx > 0)
                {
                    m_LastSelectedIdx--;
                    SetSelectedAssetByIdx(m_LastSelectedIdx);
                }
                break;
            case KeyCode.DownArrow:
                if (m_LastSelectedIdx < itemCount - 1)
                {
                    m_LastSelectedIdx++;
                    SetSelectedAssetByIdx(m_LastSelectedIdx);
                }
                break;
        }
    }

    /// <summary>
    /// 设置选中的索引
    /// </summary>
    /// <param name="selectedIdx"></param>
    /// <param name="callback"></param>
    private void SetSelectedAssetByIdx(int selectedIdx, bool callback = true)
    {
        m_LastSelectedIdx = selectedIdx;

        if (m_ShowNoneItem && selectedIdx == 0)
        {
            m_LastSelectedObject = null;
            m_LastSelectedObjectIcon = null;
        }
        else
        {
            if (m_ShowNoneItem)
            {
                selectedIdx--;
            }
            m_LastSelectedObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(m_ActiveBuiltinList[selectedIdx].path);
            m_LastSelectedObjectIcon = AssetPreview.GetAssetPreview(m_LastSelectedObject);
        }
        Rect r = ListItemCalcRect(selectedIdx);
        ScrollToPosition(AdjustRectForFraming(r));

        m_AudioClip = null;
        if (m_LastSelectedObject)
        {
            m_AudioClip = m_LastSelectedObject as AudioClip;
        }

        Repaint();

        if (callback && m_ItemSelectedCallback != null)
        {
            m_ItemSelectedCallback(m_LastSelectedObject);
        }
        else if (callback && m_CacheProperty != null)
        {
            m_CacheProperty.objectReferenceValue = m_LastSelectedObject;
            m_CacheProperty.serializedObject.ApplyModifiedProperties();
        }
    }

    /// <summary>
    /// 绘制对象的预览图标
    /// </summary>
    /// <param name="rect"></param>
    /// <param name="icon"></param>
    private void DrawObjectIcon(Rect rect, Texture icon)
    {
        if (icon == null)
        {
            return;
        }
        int num = Mathf.Min((int)rect.width, (int)rect.height);
        if (num >= icon.width * 2)
        {
            num = icon.width * 2;
        }
        FilterMode filterMode = icon.filterMode;
        icon.filterMode = FilterMode.Point;
        GUI.DrawTexture(new Rect(rect.x + (float)(((int)rect.width - num) / 2), rect.y + (float)(((int)rect.height - num) / 2),
            (float)num, (float)num), icon, ScaleMode.ScaleToFit);
        icon.filterMode = filterMode;
    }

    /// <summary>
    /// 找到第一个可见的列表项
    /// </summary>
    /// <param name="yOffset"></param>
    /// <param name="scrollPos"></param>
    /// <returns></returns>
    private int FirstVisibleRow(float yOffset, Vector2 scrollPos)
    {
        float num = scrollPos.y - yOffset;
        int result = 0;
        if (num > 0f)
        {
            float num2 = 16f; // 列表项高度
            result = (int)Mathf.Max(0f, Mathf.Floor(num / num2));
        }
        return result;
    }

    /// <summary>
    /// 搜索字符串变化
    /// </summary>
    private void FilterSettingsChanged()
    {
        BuiltinRes[] array = m_CurrentBuiltinResources;
        if (array != null && array.Length > 0 && !string.IsNullOrEmpty(m_SearchFilter))
        {
            List<BuiltinRes> list3 = new List<BuiltinRes>();
            string value = m_SearchFilter.ToLower();
            BuiltinRes[] array2 = array;
            for (int j = 0; j < array2.Length; j++)
            {
                BuiltinRes builtinResource = array2[j];
                if (builtinResource.name.ToLower().Contains(value))
                {
                    list3.Add(builtinResource);
                }
            }
            array = list3.ToArray();
        }
        m_ActiveBuiltinList = array;

        if (m_LastSelectedObject)
        {
            ListItemFrame(m_LastSelectedObject, true);
        }
    }

    /// <summary>
    /// 列表项滚动到指定矩形区域
    /// </summary>
    /// <param name="r"></param>
    private void ScrollToPosition(Rect r)
    {
        float y = r.y;
        float yMax = r.yMax;
        float height = listPosition.height;
        if (yMax > height + m_ScrollPosition.y)
        {
            m_ScrollPosition.y = yMax - height;
        }
        if (y < m_ScrollPosition.y)
        {
            m_ScrollPosition.y = y;
        }
        m_ScrollPosition.y = Mathf.Max(m_ScrollPosition.y, 0f);
    }

    /// <summary>
    /// 指定矩形定位后的区域
    /// </summary>
    /// <param name="r"></param>
    /// <returns></returns>
    private static Rect AdjustRectForFraming(Rect r)
    {
        r.height += s_SharedObjectSelector.m_Styles.resultsGridLabel.fixedHeight * 2f;
        r.y -= s_SharedObjectSelector.m_Styles.resultsGridLabel.fixedHeight;
        return r;
    }

    /// <summary>
    /// 列表项定位
    /// </summary>
    /// <param name="assetPath"></param>
    /// <param name="frame"></param>
    /// <returns></returns>
    private bool ListItemFrame(string assetPath, bool frame)
    {
        int num = ListItemIndexOf(assetPath);
        if (num != -1)
        {
            if (frame)
            {
                SetSelectedAssetByIdx(num, false);
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// 列表项定位
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="frame"></param>
    /// <returns></returns>
    private bool ListItemFrame(UnityEngine.Object obj, bool frame)
    {
        if (obj == null || !AssetDatabase.Contains(obj))
        {
            return false;
        }
        return ListItemFrame(AssetDatabase.GetAssetPath(obj), frame);
    }

    /// <summary>
    /// 根据路径查找所在的索引
    /// </summary>
    /// <param name="assetPath"></param>
    /// <returns></returns>
    private int ListItemIndexOf(string assetPath)
    {
        int num = 0;
        if (m_ShowNoneItem)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return 0;
            }
            num++;
        }
        else
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return -1;
            }
        }
        BuiltinRes[] activeBuiltinList = m_ActiveBuiltinList;
        for (int j = 0; j < activeBuiltinList.Length; j++)
        {
            BuiltinRes builtinResource = activeBuiltinList[j];
            if (assetPath == builtinResource.path)
            {
                return num;
            }
            num++;
        }
        return -1;
    }

    private void DrawPreviewTool()
    {
        DrawPreviewToolAudioClip();
    }

    private void DrawPreviewToolAudioClip()
    {
        if (!m_AudioClip)
        {
            AudioClipGUI.Clear();
            return;
        }
        Rect rect = new Rect(m_PreviewSize + 3f, position.height - 22f, 24f, 16f);
        AudioClipGUI.OnGUI(rect, m_AudioClip);
    }
}
