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

        [MenuItem("扩展/q-snapshot")]
        public static void ShowSnapshotWindow()
        {
            SnapshotMainWindow wnd = GetWindow<SnapshotMainWindow>();
            wnd.titleContent = new GUIContent("q-snapshot");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/q-snapshot/Editor/SnapshotMainWindow.uxml");
            VisualElement tree = visualTree.Instantiate();
            root.Add(tree);
            
            tree.Q<Button>("btnSnapshotCs").clicked += () => OnSnapshotInCs();
            btnDiff = tree.Q<Button>("btnDiff");
            btnDiff.clicked += () => OnDiff();


            //list
            var listView = tree.Q<ListView>("listSnapshots");
            listView.itemsSource = snapshots;
            listView.makeItem = () => new Label();
            listView.bindItem = (VisualElement element, int index) =>
                (element as Label).text = snapshots[index].ToString();
            listSnapshots = listView;
        }

        private void OnInit() {
            var L = Snapshot.getLuaEnvL();
            if (L == System.IntPtr.Zero) {
                Debug.Log("no lua env found");
                return;
            }

            string text = System.IO.File.ReadAllText(Application.dataPath + "/q-snapshot/Editor/snapshot.lua");
            Snapshot.getLuaEnv().DoString(text);
            Debug.Log("register lua functions done");
        }

        public List<SnapshotData> snapshots = new List<SnapshotData>();
        private void OnSnapshotInCs() {
            var L = Snapshot.getLuaEnvL();
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

            var data = Snapshot.Run();
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
        
    
    }

}