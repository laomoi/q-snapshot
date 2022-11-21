using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

using LuaAPI = XLua.LuaDLL.Lua;
using XLua;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;

namespace QSnapshot
{

    public enum RefType {
        REGISTRY = 1,
        TABLE_KEY = 2,
        TABLE_VALUE = 3,
        METATABLE = 4,
        UPVALUE = 5,
    };
    public class RefInfo {
        public RefType type;
        public string desc;

        public RefInfo(RefType refType, string refDesc)
        {
            type = refType;
            desc = refDesc;
        }

        public override string ToString()
        {
            if (RefType.TABLE_KEY == type) {
                return "[key]";
            }else if (RefType.TABLE_VALUE == type) {
                return "[val]" + desc;
            }else if (RefType.METATABLE == type) {
                return "[meta]";
            }else if (RefType.UPVALUE == type) {
                return "[upv]" + desc;
            }

            return "";
        }
    }
    public class ObjectData
    {
        public string className = "";
        public LuaTypes type;
        public Dictionary<System.IntPtr, RefInfo> parents;

        public string getDesc(System.IntPtr p, SnapshotData ss) {
            if ( type == LuaTypes.LUA_TUSERDATA) {
                return "[u]" + p.ToString();
            }else if ( type == LuaTypes.LUA_TTABLE) {
                if (parents.Count == 0) {
                    return "[R]";
                }
                if (this.className != "") {
                    return "[class]" + this.className + p.ToString();
                }

                return "[t]" + p.ToString();
            }else if ( type == LuaTypes.LUA_TFUNCTION) {
                if (ss.sources.ContainsKey(p)) {
                    return "[f]"  + ss.sources[p];
                }
                return "[f]" + p.ToString();
            }
            else {
                return "[unknown]" + p.ToString();
            }
        }

    };
    public class SnapshotData
    {
        public Dictionary<System.IntPtr, ObjectData> objects;
        public Dictionary<System.IntPtr, string> sources;
        public DateTime snapshotTime;
        public double memoryUsage;

        public override string ToString() {
            return "ss-" + this.snapshotTime.ToLongTimeString().ToString() + '-' + "mem-" + memoryUsage+ '-' + "objs-" + objects.Count; 
        }

        protected void traverseChain(System.IntPtr p, List<string> allPath, List<string> path, string refDesc) {
            if (path.Count > 50) {
                //发现环？ 结束
                allPath.Add("too Long path:" + String.Join("", path) );
                return;
            } 

            if (allPath.Count > 50) {
                //"too many path..."
                return;
            }
            
            
            var obj = this.objects[p];
            var selfDesc = obj.getDesc(p, this);
            if (obj.parents.Count == 0) {
                //到达registry 终点， 结束,把path放入allPath
                allPath.Add(String.Join("", path) );
            } else {
                var parents = obj.parents;
                foreach (var item in parents) {
                    var parent = item.Key;
                    var parentRef = item.Value;
                    var parentRefDesc = parentRef.ToString();
                    var oldCount = path.Count;
                    path.Add("<--(" + parentRefDesc + ")--" + this.objects[parent].getDesc(parent, this) );
                    traverseChain(parent, allPath, path, parentRefDesc);
                    path.RemoveRange(oldCount, path.Count-oldCount);
                }
            }

        }
        public string getRefChain(System.IntPtr p) {
            List<string> path = new List<string>();
            List<string> allPath = new List<string>();

            traverseChain(p, allPath, path, "");            
            
            return String.Join(Environment.NewLine, allPath);
        }
    };
    public class Snapshot
    {
        public static SnapshotData Run() {
            var L = GlobalLuaEnv.Instance.GetRawLuaL();
            if (L == System.IntPtr.Zero) {
                Debug.Log("no lua env found");
                return null;
            }

            Snapshot.FullGC();

            var data = new SnapshotData();
            data.objects = new Dictionary<System.IntPtr, ObjectData>();
            data.sources = new Dictionary<System.IntPtr, string>();


            if (!Snapshot.call_lua(L, "get_memory_usage", 0, 1) ){
                return null;
            }
            data.memoryUsage = LuaAPI.lua_tonumber(L, -1);
            LuaAPI.lua_pop(L, 1);


            if (!Snapshot.call_lua(L, "get_registry", 0, 1) ){
                return null;
            }
            data.snapshotTime = DateTime.Now;

            //stack top is registry
            Snapshot.traverse_table(L, data, System.IntPtr.Zero, RefType.REGISTRY);
            LuaAPI.lua_pop(L, 1);
            return data;
        }


        public static bool call_lua(System.IntPtr L, string func_name, int argn, int resultn) {
            lua_getglobal(L, "qsnapshot");

            LuaAPI.lua_pushstring(L, func_name);
            LuaAPI.lua_rawget(L, -2);

            //remove qsnapshot
            LuaAPI.lua_remove(L, -2);

            //move function under args
            if (argn > 0) {
                LuaAPI.lua_insert(L, -argn-1);
            }


            int oldTop = LuaAPI.lua_gettop(L);
            if (
            LuaAPI.lua_pcall(L, argn, resultn, 0) != 0 ) {
                Snapshot.GetLuaEnv().ThrowExceptionFromError(oldTop);
                LuaAPI.lua_settop(L, oldTop);
                return false;
            } else {
                //call ok
                return true;
            }
        }       


        public static void traverse_function(System.IntPtr L, SnapshotData data, System.IntPtr parent, RefType refType, string refDesc){
            var p = Snapshot.mark_object(L, data, parent, refType, refDesc);
            if (p == System.IntPtr.Zero) {
                return;
            }
            LuaAPI.lua_pushvalue(L, -1);

            if (Snapshot.call_lua(L, "get_func_source", 1, 1) ) {
                data.sources[p] = LuaAPI.lua_tostring(L, -1);
                LuaAPI.lua_pop(L, 1);
            }

            for (var i=1;;i++) {
                System.IntPtr name = LuaAPI.lua_getupvalue(L, -1, i);
                if (name == System.IntPtr.Zero) {
                    break;
                }
                string str = Marshal.PtrToStringAuto(name);
                Snapshot.traverse_object(L, data, p, RefType.UPVALUE, str);
                LuaAPI.lua_pop(L, 1);
            }
        }


        public static void traverse_userdata(System.IntPtr L, SnapshotData data, System.IntPtr parent, RefType refType, string refDesc){
            var p = Snapshot.mark_object(L, data, parent, refType, refDesc);
            if (p == System.IntPtr.Zero) {
                return;
            }
            LuaAPI.lua_pushvalue(L, -1);
            if (Snapshot.call_lua(L, "get_metatable", 1, 1) ) {
                if (!LuaAPI.lua_isnil(L, -1)) {
                    Snapshot.traverse_table(L, data, p, RefType.METATABLE);
                }
                LuaAPI.lua_pop(L, 1);//pop metatable
            }
        }

        public static string get_key_desc(System.IntPtr L){
            var t =  LuaAPI.lua_type(L, -1);
            if ( t == LuaTypes.LUA_TNUMBER) {
                return "[n]" +LuaAPI.lua_tonumber(L, -1).ToString() ;
            }else if ( t == LuaTypes.LUA_TBOOLEAN) {
                return "[b]" + LuaAPI.lua_toboolean(L, -1).ToString();
            }else if ( t == LuaTypes.LUA_TSTRING) {
                return LuaAPI.lua_tostring(L, -1);
            }else if ( t == LuaTypes.LUA_TUSERDATA) {
                return "[u]" + LuaAPI.lua_topointer(L, -1);
            }else if ( t == LuaTypes.LUA_TTABLE) {
                return "[t]" + LuaAPI.lua_topointer(L, -1);
            }else if ( t == LuaTypes.LUA_TNIL) {
                return "[nil]";
            }
            else {
                return "[unknown]" + t.ToString() + ":" + LuaAPI.lua_topointer(L, -1).ToString();
            }
        }

        public static void traverse_table(System.IntPtr L, SnapshotData data, System.IntPtr parent, RefType refType, string refDesc=""){
            var p = Snapshot.mark_object(L, data, parent, refType, refDesc);
            if (p == System.IntPtr.Zero) {
                return;
            }
            bool weakk = false;
            bool weakv = false;
            
            int oldTop = LuaAPI.lua_gettop(L);
            LuaAPI.lua_pushvalue(L, -1);
            if (Snapshot.call_lua(L, "get_metatable", 1, 1) ) {
                if (!LuaAPI.lua_isnil(L, -1)) {
                    Snapshot.traverse_table(L, data, p, RefType.METATABLE);
                    LuaAPI.lua_pushstring(L, "__mode");
                    LuaAPI.lua_rawget(L, -2);
                    if (!LuaAPI.lua_isnil(L, -1)) {
                        var mode = LuaAPI.lua_tostring(L, -1);
                        if (mode == "k"){
                            weakk = true;
                        } else if (mode == "v") {
                            weakv = true;
                        } else if (mode == "kv") {
                            weakk = true;
                            weakv = true;
                        }
                    }
                    LuaAPI.lua_pop(L, 1); //pop __mode
                }
                LuaAPI.lua_pop(L, 1); //pop metatable
            }
            int newTop = LuaAPI.lua_gettop(L);

            LuaAPI.lua_pushnil(L);
            while (LuaAPI.lua_next(L, -2) != 0)
            {
                //v, k , T
                LuaAPI.lua_insert(L, -2); //k, v, T
                var key_desc = Snapshot.get_key_desc(L);
                LuaAPI.lua_insert(L, -2); //v, k, T

                if (!weakv) {
                    Snapshot.traverse_object(L, data, p, RefType.TABLE_VALUE, key_desc);
                }
                LuaAPI.lua_pop(L, 1);

                if (!weakk) {
                    Snapshot.traverse_object(L, data, p, RefType.TABLE_KEY);
                }

            }
        }


        public static void traverse_object(System.IntPtr L, SnapshotData data, System.IntPtr parent, RefType refType, string refDesc=""){
            var t =  LuaAPI.lua_type(L, -1);
            if (t == LuaTypes.LUA_TTABLE ){
                Snapshot.traverse_table(L, data, parent, refType, refDesc);
            } else if (t == LuaTypes.LUA_TFUNCTION) {
                Snapshot.traverse_function(L, data, parent, refType, refDesc);
            } else if ( t == LuaTypes.LUA_TUSERDATA) {
                Snapshot.traverse_userdata(L, data, parent, refType, refDesc);
            }
        }


        public static System.IntPtr mark_object(System.IntPtr L,  SnapshotData data, System.IntPtr parent, RefType refType, string refDesc) {       
            if (LuaAPI.lua_isnil(L, -1)) {
                return System.IntPtr.Zero;
            }

            var t =  LuaAPI.lua_type(L, -1);
            //only mark gc object
            if (t != LuaTypes.LUA_TTABLE &&
                t != LuaTypes.LUA_TFUNCTION &&
                t != LuaTypes.LUA_TUSERDATA 
            ) {
                return System.IntPtr.Zero;
            }
            System.IntPtr p = LuaAPI.lua_topointer(L, -1);
            var first_mark = false;
            if (!data.objects.ContainsKey(p)) {
                var objData = new ObjectData();
                objData.parents = new Dictionary<System.IntPtr, RefInfo>();
                objData.type = t;
                data.objects[p] = objData;

                //special key: class name
                if (t == LuaTypes.LUA_TTABLE) {
                    LuaAPI.lua_pushstring(L, "__class_name");
                    LuaAPI.lua_rawget(L, -2);
                    if (!LuaAPI.lua_isnil(L, -1)) {
                        var classname = LuaAPI.lua_tostring(L, -1);
                        objData.className = classname;
                    }                    
                    LuaAPI.lua_pop(L, 1);
                }
                    
                first_mark = true;                
            }
            if (parent != System.IntPtr.Zero) {
                data.objects[p].parents[parent] = new RefInfo(refType, refDesc);
            }
            if (first_mark){
                return p;
            }
            return System.IntPtr.Zero;
        }

        public static void lua_getglobal(System.IntPtr L, string name) {
            //为了避免触发global table 的metable 事件，所以不能直接使用xlua_getglobal方法
            LuaAPI.xlua_getglobal(L, "_G"); //_G, value
            if (LuaAPI.lua_isnil(L, -1)) {
                Debug.Log("_G is nil ");
                LuaAPI.lua_pop(L, 1);
                return;
            } 
            LuaAPI.lua_pushstring(L, name);    //name, _G    
            LuaAPI.lua_rawget(L, -2);
            LuaAPI.lua_remove(L, -2);
        }



        public static string get_stack_desc(System.IntPtr L ) {
            var top = LuaAPI.lua_gettop(L);
            if (top > 5) {
                top = 5;
            }
            var str = "";
            for (var i=1;i<=top;i++) {
                LuaAPI.lua_pushvalue(L, -i);
                var t =  LuaAPI.lua_type(L, -1);
                var key_desc = Snapshot.get_key_desc(L);
                str = str + "[" + i + "]" + key_desc + "  ";
                LuaAPI.lua_pop(L, 1);
            }

            return str;
        }


        public static void FullGC() {
            Snapshot.GetLuaEnv().FullGc();
            Snapshot.GetLuaEnv().Tick();

            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
        }

        public static System.IntPtr GetLuaEnvL() {
            return GlobalLuaEnv.Instance.GetRawLuaL();
        }
        public static LuaEnv GetLuaEnv() {
            return GlobalLuaEnv.Instance.LuaEnv;
        }
    }

}