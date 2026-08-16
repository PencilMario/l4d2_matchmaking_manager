# NativeMatchFrameworkProbe 中文说明

## 目的

`NativeMatchFrameworkProbe` 是一个 Windows x86 原生探针，用于验证在不启动
`left4dead2.exe` 的情况下，能否加载 L4D2 的 matchmaking 组件并创建一个
`IMatchSession`。

探针还会通过 L4D2 自带的 `steam_api.dll` 查询 Steam lobby 列表，并检查
以下三个字段是否同时匹配目标房间：

```text
game:map       = c1m1_hotel
game:mode       = coop
system:network = LIVE
```

本探针只查询已有 lobby，不负责创建 Steam lobby。独立的 Steam lobby 创建
实验位于 [SteamLobbyProbe](../SteamLobbyProbe/README.md)。

## 已验证能力

- `SteamAPI_Init()` 成功。
- `MATCHFRAMEWORK_001` 可以加载、连接并初始化。
- `IMatchFramework::CreateSession()` 同步生成非空的 `IMatchSession`。
- Steam `RequestLobbyList()` 可以收到 `LobbyMatchList_t` 异步结果。
- 可以枚举 lobby ID，并读取 `game:map`、`game:mode`、`system:network`。
- 全流程使用 x86，不启动 `left4dead2.exe`。

当前验证不能证明 L4D2 已经完成真正的服务器预留或加入流程。创建出来的
session 通常还没有：

```text
server:reservationid
server:connectstring
```

## 运行环境

需要满足以下条件：

1. Windows x86 运行环境。
2. Steam 正在运行，并且当前账号已登录、拥有 L4D2 AppID 550。
3. L4D2 安装在探针源码中的路径：

   ```text
   D:\Steam\steamapps\common\Left 4 Dead 2
   ```

4. 已安装 LLVM、Windows SDK 和 x86 CDB。

默认工具路径为：

```text
C:\Program Files\LLVM\bin\clang-cl.exe
C:\Program Files\LLVM\bin\lld-link.exe
C:\Program Files (x86)\Windows Kits\10\Debuggers\x86\cdb.exe
```

如果 L4D2 安装路径不同，需要先修改 `NativeMatchFrameworkProbe.cpp` 中的
绝对路径。

## 编译

在项目根目录 `J:\GithubRep\l4d2_matchmaking_manager` 执行：

```powershell
$clang = 'C:\Program Files\LLVM\bin\clang-cl.exe'
& $clang --target=i686-pc-windows-msvc /clang:-fno-builtin /nologo /c /O1 /GS- /GR- /EHs-c- /Zl `
  /Fo'.\research\NativeMatchFrameworkProbe\NativeMatchFrameworkProbe.obj' `
  '.\research\NativeMatchFrameworkProbe\NativeMatchFrameworkProbe.cpp'

$link = 'C:\Program Files\LLVM\bin\lld-link.exe'
$kernel = 'C:\Program Files (x86)\Windows Kits\10\Lib\10.0.26100.0\um\x86\kernel32.lib'
& $link /machine:x86 /subsystem:windows /entry:WinMainCRTStartup /nodefaultlib `
  /out:'.\research\NativeMatchFrameworkProbe\NativeMatchFrameworkProbe.exe' `
  '.\research\NativeMatchFrameworkProbe\NativeMatchFrameworkProbe.obj' $kernel
```

探针是无 CRT 的最小原生程序，最终 PE 应为 `COFF-i386`，只导入
`KERNEL32.dll`。64 位 KeyValues 值和 Steam lobby ID 使用固定宽度十六进制
输出，以避免引入 CRT 的 64 位除法运行库。

## 直接运行

```powershell
& '.\research\NativeMatchFrameworkProbe\NativeMatchFrameworkProbe.exe'
```

典型关键输出如下，Steam lobby 数量会随在线房间变化：

```text
Connect=true
Init result=1
CreateSession called=true
GetMatchSession after CreateSession pointer=0x031B91F8
Steam lobby query completed=true
Steam lobby query result_ok=true failed=false
Steam lobby count=47
Steam lobby match=false
RunFrame skipped=true reason=external-process-no-engine-frame
```

`GetMatchSession after CreateSession pointer` 只要不是
`0x00000000`，就表示 MatchFramework 已返回 session 对象。

`Steam lobby match=false` 只表示当前查询结果中没有同时满足三个目标字段的
lobby，不表示 Steam 查询失败。查询失败或所需 API 不可用时，输出为：

```text
Steam lobby query completed=false
Steam lobby match=unavailable
```

## 自动化测试

三个 PowerShell 脚本都会使用 x86 CDB 执行探针，并检查 Access violation：

```powershell
& powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File '.\research\NativeMatchFrameworkProbe\Test-Connect.ps1'

& powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File '.\research\NativeMatchFrameworkProbe\Test-Init.ps1'

& powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File '.\research\NativeMatchFrameworkProbe\Test-CreateSession.ps1'
```

通过条件：

- Connect 脚本看到 `Connect=true` 或 `Connect=false`，且无 Access violation。
- Init 脚本看到 `Init result=1`，且无 Access violation。
- CreateSession 脚本看到非空 session、`Steam lobby query completed=true`，
  以及 `Steam lobby match=true/false/unavailable`。

CDB 启动时可能向 stderr 输出：

```text
Setting breakpad minidump AppID = 550
```

这是 Steam 的诊断文本，不是探针崩溃；测试脚本已避免将其当作失败。

## 关键 ABI 说明

### KeyValues

探针没有依赖 L4D2 的 C++ 头文件，而是按当前 L4D2 ABI 手动构造 44 字节
`KeyValues`：

- `IKeyValuesSystem` vtable slot 1：注册对象大小。
- `IKeyValuesSystem` vtable slot 2：分配对象。
- `IKeyValuesSystem` vtable slot 4：获取 key symbol。
- `IKeyValuesSystem` vtable slot 5：把 symbol 转回字符串。
- `tier0.dll` 导出的 `g_pMemAlloc`：分配字符串值。
- data type `7`：`TYPE_UINT64`，其值按指针指向的 64 位整数读取。

### MatchFramework

当前版本使用以下接口槽位：

| 槽位 | 方法 | 用途 |
| ---: | --- | --- |
| 0 | `Connect` | 注入聚合的 `CreateInterface` 工厂 |
| 3 | `Init` | 初始化 MatchFramework |
| 9 | `GetMatchSession` | 获取当前 session |
| 12 | `CreateSession` | 创建 session |

`RunFrame` 在完整 L4D2 进程外会依赖未初始化的引擎宿主状态，当前探针
明确跳过它，并输出：

```text
RunFrame skipped=true reason=external-process-no-engine-frame
```

### Steam lobby 查询

查询通过 `steam_api.dll` 动态解析以下接口：

- `SteamAPI_SteamMatchmaking_v009`
- `SteamAPI_SteamUtils_v010`
- `SteamAPI_ISteamMatchmaking_RequestLobbyList`
- `SteamAPI_ISteamUtils_IsAPICallCompleted`
- `SteamAPI_ISteamUtils_GetAPICallResult`
- `SteamAPI_ISteamMatchmaking_GetLobbyByIndex`
- `SteamAPI_ISteamMatchmaking_GetLobbyData`

回调 ID `510` 对应 `LobbyMatchList_t`。实际 L4D2 DLL 没有
`SteamAPI_ISteamMatchmaking_GetLobbyCount` 导出，因此枚举边界使用
`LobbyMatchList_t.m_nLobbiesMatching`，再调用 `GetLobbyByIndex`。

## 与完整游戏流程的边界

这项实验验证了三个独立事实：

1. Steam API 可以在外部 x86 进程中初始化并查询 lobby。
2. L4D2 MatchFramework 可以在外部进程中创建一个非空 session 对象。
3. 两者都不等价于 L4D2 客户端已经进入可加入游戏的完整 lobby。

要完成真正的游戏加入流程，还需要继续研究完整引擎宿主、listen/dedicated
server、reservation ID、connect string 以及 MatchFramework callback/state
推进。当前探针不会伪造这些字段，也不会把普通 Steam lobby 误报为完整的
L4D2 游戏 session。
