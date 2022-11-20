一个小而美的工具，在unity+xlua环境下，生成lua对象的内存快照，并支持2个快照之间做diff，用来查找对象泄露。

### 使用前准备：

Snapshot.cs最下面有2个静态函数，是用来取得当前运行中的LuaEnv对象和L指针，请根据自己项目修改这2个静态函数的实现代码。

public static System.IntPtr getLuaEnvL() {
    return GlobalLuaEnv.Instance.GetRawLuaL();
}

public static LuaEnv getLuaEnv() {
    return GlobalLuaEnv.Instance.LuaEnv;
}


### 使用方法：

菜单中选中: 扩展/qsnapshot-内存镜像
在游戏运行时，点击"Snapshot in c#" 抓取内存镜像，随后可以对多个镜像进行 "Snapshot diff"

