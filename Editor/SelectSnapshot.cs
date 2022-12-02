
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.IO;
using System;

namespace QSnapshot
{
    public class SelectSnapshot : PopupWindowContent
    {
        //Set the window size
        protected SnapshotMainWindow mainWin;
        protected VisualElement tree;


        public override Vector2 GetWindowSize()
        {
            return new Vector2(441, 106);
        }

        public override void OnGUI(Rect rect)
        {
            // Intentionally left empty
        }

        public SelectSnapshot setMainWin(SnapshotMainWindow win) {
            this.mainWin = win;
            return this;
        }

        public override void OnOpen()
        {

            var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/q-snapshot/Editor/SelectSnapshot.uxml");
            tree = visualTreeAsset.Instantiate();
            editorWindow.rootVisualElement.Add(tree);

            var snapshots = mainWin.snapshots;
            
            //fill dropdowns
            var choices = new List<string>{};
            foreach (var ss in snapshots) {
                choices.Add(ss.ToString());
            }

            var oldSnapshot = tree.Q<DropdownField>("oldSnapshot");
            oldSnapshot.choices = choices;
            if (choices.Count > 0) {
                if (choices.Count > 1){
                    oldSnapshot.value = choices[choices.Count-2];
                } else {
                    oldSnapshot.value = choices[choices.Count-1];
                }
            }

            var newSnapshot = tree.Q<DropdownField>("newSnapshot");
            newSnapshot.choices = choices;
            newSnapshot.value = choices[0];
            if (choices.Count > 0) {
                newSnapshot.value = choices[choices.Count-1];
            }

            //btn diff
            tree.Q<Button>("btnDiffNow").RegisterCallback<MouseDownEvent>((evt) =>
            {
                OnDiff();
            }, TrickleDown.TrickleDown);
        }
            
        public override void OnClose()
        {
        }

        protected void OnDiff() {
            var oldSnapshotValue = tree.Q<DropdownField>("oldSnapshot").value;
            var newSnapshotValue = tree.Q<DropdownField>("newSnapshot").value;
            var snapshots = mainWin.snapshots;
           
            SnapshotData oldSnapshot = null;
            SnapshotData newSnapshot = null;
            foreach (var ss in snapshots) {
                if (ss.ToString() == oldSnapshotValue) {
                    oldSnapshot = ss;
                } 
                if (ss.ToString() == newSnapshotValue) {
                    newSnapshot = ss;
                } 
            }

            if(oldSnapshot == null || newSnapshot == null){
                return;
            }

            diffSnapshot(oldSnapshot, newSnapshot);            
        }

        protected void diffSnapshot(SnapshotData oldSs, SnapshotData newSs) {
            //added = leak
            DiffSetting setting = mainWin.getDiffSetting();            
            var filterRequireCount = 0;

            var keys = newSs.objects.Keys;
            Dictionary<System.IntPtr, bool> dictAddKeys = new Dictionary<System.IntPtr, bool>(); 
            List<System.IntPtr> addKeys = new List<System.IntPtr>(); 
            foreach(var objKey in keys) {
                if (!oldSs.objects.ContainsKey(objKey)) {                    
                    addKeys.Add(objKey);
                    dictAddKeys[objKey] = true;
                }
            }

            //泄露的对象里剔除掉二级泄露的对象（比如由于某个对象泄露，它引用的所有内部属性也泄露了，这些属于二级泄露）
            List<System.IntPtr> leakKeys = new List<System.IntPtr>(); 
            foreach(var objKey in addKeys) {
                var obj = newSs.objects[objKey];
                var parents = obj.parents;
                bool isDirectLeak = false;
                foreach(var parent in parents) {
                    if (!dictAddKeys.ContainsKey(parent.Key)) {
                        isDirectLeak = true;
                        break;
                    }
                }
                if (isDirectLeak) {
                    if (setting.filterRequire && obj.requirePath != "") {
                        filterRequireCount++;
                        continue;
                    }
                    leakKeys.Add(objKey);
                }
            }

            

            var tmpPath = FileUtil.GetUniqueTempPathInProject() + ".txt";
            StreamWriter writer = new StreamWriter(tmpPath, false);

            var sb = new System.Text.StringBuilder();
            
          

            sb.AppendLine("summary:").AppendLine("leak objects count:" + leakKeys.Count);
            if (setting.filterRequire){
                sb.AppendLine("filter required count:" + filterRequireCount);
            }
            sb.AppendLine();

            // sb.AppendLine("total related leak objs:" + keys.Count).AppendLine();


            foreach (var objKey in leakKeys) {
                var obj = newSs.objects[objKey];
                sb.Append("leak:").AppendLine(newSs.objects[objKey].getDesc(objKey, newSs));
                var chainDesc = newSs.getRefChain(objKey, setting.maxPathLength, setting.maxPathCount);
                sb.AppendLine(chainDesc).AppendLine();
                if ( sb.Length > 100000 ) {
                    writer.Write(sb.ToString());
                    sb.Clear();
                }
            }

            
            writer.Write(sb.ToString());
            writer.Close();
            EditorUtility.OpenWithDefaultApp(tmpPath);

        }

    }

}