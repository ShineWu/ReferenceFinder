using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.IMGUI.Controls;

public class ReferenceFinderWindow : EditorWindow
{
    //依赖模式的key
    const string IsDependencyKey = "ReferenceFinderData_IsDependency";
    //是否需要更新信息状态的key
    const string NeedUpdateStateKey = "ReferenceFinderData_IsUpdateState";

    private ReferenceFinderData data = new ReferenceFinderData();
    
    private bool initializedData = false;

    private bool isDependency = false;
    private bool needUpdateState = true;

    [SerializeField]
    private TreeViewState m_TreeViewState;
    
    private AssetTreeView m_AssetTreeView;
    private bool needUpdateAssetTree = false;
    
    // 工具栏样式
    private bool initializedStyle = false;
    private GUIStyle toolbtnGUIStyle;
    private GUIStyle toolbarGUIStyle;
    
    //查找资源引用信息
    [MenuItem("Assets/Reference Finder")]
    static void FindRef()
    {
        ReferenceFinderWindow window = GetWindow<ReferenceFinderWindow>();
        window.wantsMouseMove = false;
        window.titleContent = new GUIContent("Reference Finder");
        window.InitDataIfNeeded();
        window.InitStyleIfNeeded();
        window.Show();
        window.Focus();       
        window.UpdateSelectedAssets();
    }
    
    //初始化数据
    void InitDataIfNeeded()
    {
        if (!initializedData)
        {
            //初始化数据
            data.CollectDependenciesInfo();
            initializedData = true;
        }
    }

    //初始化GUIStyle
    void InitStyleIfNeeded()
    {
        if (!initializedStyle)
        {
            toolbtnGUIStyle = new GUIStyle("ToolbarButton");
            toolbarGUIStyle = new GUIStyle("Toolbar");
            initializedStyle = true;
        }
    }
    
    //选中资源列表
    private List<string> selectedAssets = new List<string>();
    //更新选中资源列表
    private void UpdateSelectedAssets()
    {
        selectedAssets.Clear();
        foreach(var obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            //如果是文件夹
            if (Directory.Exists(path))
            {
                string[] folder = new string[] { path };
                //将文件夹下所有资源作为选择资源
                string[] guids = AssetDatabase.FindAssets(null, folder);
                foreach(var guid in guids)
                {
                    if (!selectedAssets.Contains(guid) &&
                        !Directory.Exists(AssetDatabase.GUIDToAssetPath(guid)))
                    {
                        selectedAssets.Add(guid);
                    }                        
                }
            }
            //如果是文件资源
            else
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                selectedAssets.Add(guid);
            }
        }
        needUpdateAssetTree = true;
    }

    //通过选中资源列表更新TreeView
    private void UpdateAssetTree()
    {
        if (needUpdateAssetTree && selectedAssets.Count != 0)
        {
            var root = SelectedAssetToRootItem(selectedAssets);
            if(m_AssetTreeView == null)
            {
                //初始化TreeView
                if (m_TreeViewState == null)
                    m_TreeViewState = new TreeViewState();
                var headerState = AssetTreeView.CreateDefaultMultiColumnHeaderState(position.width);
                var multiColumnHeader = new MultiColumnHeader(headerState);
                m_AssetTreeView = new AssetTreeView(m_TreeViewState, multiColumnHeader);
            }
            m_AssetTreeView.assetRoot = root;
            m_AssetTreeView.CollapseAll();
            m_AssetTreeView.Reload();
            needUpdateAssetTree = false;
        }
    }

    private void OnEnable()
    {
        isDependency = PlayerPrefs.GetInt(IsDependencyKey, 0) == 1;
        needUpdateState = PlayerPrefs.GetInt(NeedUpdateStateKey, 1) == 1;
    }

    private void OnGUI()
    {
        DrawOptionBar();
        UpdateAssetTree();
        
        //绘制Treeview
        if (m_AssetTreeView != null)
        {
            m_AssetTreeView.OnGUI(new Rect(0, toolbarGUIStyle.fixedHeight, position.width, position.height - toolbarGUIStyle.fixedHeight));
        }        
    }
    
    //绘制Toolbar
    public void DrawOptionBar()
    {
        EditorGUILayout.BeginHorizontal(toolbarGUIStyle);
        
        //刷新数据
        if (GUILayout.Button("Refresh Data", toolbtnGUIStyle, GUILayout.Width(120)))
        {
            data.CollectDependenciesInfo();
            needUpdateAssetTree = true;
            EditorGUIUtility.ExitGUI();
        }
        
        //修改模式
        bool preIsDependency = isDependency;
        isDependency = GUILayout.Toggle(isDependency, isDependency ? "Model(Dependency)" : "Model(Reference)", toolbtnGUIStyle,GUILayout.Width(150));
        if(preIsDependency != isDependency){
            needUpdateAssetTree = true;
            PlayerPrefs.SetInt(IsDependencyKey, isDependency ? 1 : 0);
        }
        
        //是否需要更新状态
        bool preNeedUpdateState = needUpdateState;
        needUpdateState = GUILayout.Toggle(needUpdateState, "Need Update State", toolbtnGUIStyle, GUILayout.Width(150));
        if (preNeedUpdateState != needUpdateState)
        {
            PlayerPrefs.SetInt(NeedUpdateStateKey, needUpdateState ? 1 : 0);
        }
        
        GUILayout.FlexibleSpace();

        //扩展
        if (GUILayout.Button("Expand", toolbtnGUIStyle))
        {
            if (m_AssetTreeView != null) m_AssetTreeView.ExpandAll();
        }
        //折叠
        if (GUILayout.Button("Collapse", toolbtnGUIStyle))
        {
            if (m_AssetTreeView != null) m_AssetTreeView.CollapseAll();
        }
        EditorGUILayout.EndHorizontal();
    }
    
    //生成root相关
    private HashSet<string> updatedAssetSet = new HashSet<string>();
    //通过选择资源列表生成TreeView的根节点
    private  AssetViewItem SelectedAssetToRootItem(List<string> selectedAssets)
    {
        updatedAssetSet.Clear();
        int elementCount = 0;
        var root = new AssetViewItem { id = elementCount, depth = -1, displayName = "Root", data = null };
        int depth = 0;
        foreach (var guid in selectedAssets)
        {
            root.AddChild(CreateTree(guid, ref elementCount, depth));
        }
        updatedAssetSet.Clear();
        return root;
    }
    
    //通过每个节点的数据生成子节点
    private  AssetViewItem CreateTree(string guid, ref int elementCount, int _depth)
    {
        if (needUpdateState && !updatedAssetSet.Contains(guid))
        {
            data.UpdateAssetState(guid);
            updatedAssetSet.Add(guid);
        } 
        
        ++elementCount;
        var referenceData = data.assetDict[guid];
        var root = new AssetViewItem { id = elementCount, displayName = referenceData.name, data = referenceData, depth = _depth };
        var childGuids = isDependency ? referenceData.dependencies : referenceData.references;
        foreach (var childGuid in childGuids)
        {
            root.AddChild(CreateTree(childGuid, ref elementCount, _depth + 1));
        }

        return root;
    }
}
