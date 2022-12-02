using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

using LuaAPI = XLua.LuaDLL.Lua;
using XLua;
using System.Collections.Generic;

using PopupWindow = UnityEditor.PopupWindow;

namespace QSnapshot
{
    public class SnapshotMainWindow : EditorWindow
    {
        protected ListView listSnapshots;
        protected Button btnDiff;

        protected VisualElement uiTree;

        [MenuItem("扩展/q-snapshot")]
        public static void ShowSnapshotWindow()
        {
            SnapshotMainWindow wnd = GetWindow<SnapshotMainWindow>();
            wnd.titleContent = new GUIContent("q-snapshot");
            wnd.minSize = new Vector2(545, 292);
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/q-snapshot/Editor/SnapshotMainWindow.uxml");
            
            uiTree = visualTree.Instantiate();
            root.Add(uiTree);
            
            uiTree.Q<Button>("btnSnapshotCs").clicked += () => OnSnapshotInCs();
            btnDiff = uiTree.Q<Button>("btnDiff");
            btnDiff.clicked += () => OnDiff();


            //list
            var listView = uiTree.Q<ListView>("listSnapshots");
            listView.itemsSource = snapshots;
            listView.makeItem = () => new Label();
            listView.bindItem = (VisualElement element, int index) =>
                (element as Label).text = snapshots[index].ToString();
            listSnapshots = listView;
        }

        private void OnInit() {
            var L = Snapshot.GetLuaEnvL();
            if (L == System.IntPtr.Zero) {
                Debug.Log("no lua env found");
                return;
            }

            string text = System.IO.File.ReadAllText(Application.dataPath + "/q-snapshot/Editor/snapshot.lua");
            Snapshot.GetLuaEnv().DoString(text);
            Debug.Log("register lua functions done");
        }

        public List<SnapshotData> snapshots = new List<SnapshotData>();
        private void OnSnapshotInCs() {
            var L = Snapshot.GetLuaEnvL();
            if (L == System.IntPtr.Zero) {
                Debug.Log("no lua env found");
                return;
            }
            Snapshot.lua_getglobal(L, "qsnapshot");
            if (LuaAPI.lua_isnil(L, -1)) {
                LuaAPI.lua_pop(L, 1);
                OnInit();
            } else {
                LuaAPI.lua_pop(L, 1);
            }
            SnapshotSetting setting = new SnapshotSetting();
            setting.tagWhenSnapshot = uiTree.Q<Toggle>("chkTag").value;
            var data = Snapshot.Run(setting);
            if (data != null) {
                snapshots.Add(data);
                listSnapshots.RefreshItems();
            }
          
        }

        protected void OnDiff() {
            if (this.snapshots.Count == 0) {
                Debug.Log("Snapshot in game first.");
                return;
            }

            PopupWindow.Show(btnDiff.worldBound, new SelectSnapshot().setMainWin(this));
        }

        public DiffSetting getDiffSetting() {
            DiffSetting setting = new DiffSetting();
            setting.filterRequire = uiTree.Q<Toggle>("chkFilterRequire").value;
            return setting;
        }
        
    
    }

}