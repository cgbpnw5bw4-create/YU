# 工位潜行（DeskStealth）

一款固定 `256×160 DIP` 的 Windows 微型桌面潜行游戏。程序默认置顶、无边框、静音，启动和失焦时显示真实本地时间；无需安装、联网或外部素材。

## 直接运行

双击 `dist\DeskStealth.exe`。发布物为 Windows 10/11 x64 自包含单文件，不要求预装 .NET。

未签名 EXE 可能触发 Windows SmartScreen；本项目不会绕过系统提示，也未提供安装包、自启动或代码签名。

## 操作

| 按键 | 作用 |
| --- | --- |
| `WASD` | 八方向移动；斜向速度归一化 |
| `Shift` + `WASD` | 冲刺，消耗体力 |
| `Ctrl` + `WASD` | 蹲伏；位于虚线标记工位时完全隐藏；与 Shift 同按时 Ctrl 优先 |
| `Esc` | 游戏/结算画面与迷你时钟之间切换 |
| `Enter` | 结算后进入下一张地图或重试当前地图 |
| `Alt+F4` | 退出 |

从时钟返回后先显示 1 秒准备提示；倒计时完成后的首次 `WASD` 才恢复模拟。时钟态可用 `Shift+WASD` 移动窗口，也可按住鼠标左键拖动。

## 玩法

- 每张地图限时 60 秒，自动拾取 3 份黄色资料，集齐后进入出口通关。
- 两名守卫按固定路线巡逻；珊瑚色扇形为经过墙体裁切的视野。
- 连续暴露会被抓：步行 0.75 秒、冲刺 0.5 秒、蹲伏 1 秒；脱离视野后用 1 秒衰减至零。
- 被抓会保留资料、扣 8 秒并重置位置；三次被抓或时间归零即失败。
- 通关分数为“剩余整秒 × 100”，零抓捕额外加 500；三张地图分别保存最高分。

## 桌面与隐私行为

- 启动、失焦和第二次启动程序时都进入真实本地时间的时钟态。
- 不注册全局热键、键盘钩子、联网、遥测、日志或音频设备。
- 托盘菜单提供置顶、1×/1.25×/1.5×缩放、重置位置、清除本地数据和退出；托盘不可用时回退为任务栏可见。
- 设置仅保存在 `%LocalAppData%\DeskStealth\settings.json`，内容限于版本、窗口位置、缩放、置顶和分图最高分。损坏文件会先改名备份，再恢复默认。
- 时钟只是低视觉暴露界面，不仿冒系统或办公软件，也不构成隐私或安全保护。

## 源码结构

- `src\DeskStealth.Core`：确定性游戏模拟、地图、守卫视野与计分。
- `src\DeskStealth.App`：WinForms 窗口、输入、托盘、DPI、固定后备位图和 GDI+ 渲染。
- `tests`：核心规则、地图静态验收、设置、单实例和嵌入资源测试。
- `design\reference`：选定的“冷色工位蓝图”视觉基准。
- `design\generated`：由内置 ImageGen 生成的角色和办公设施源图集。
- `tools`：资产整理、真实窗口截图、Golden 回归和 QA 辅助脚本。

角色与办公图集的运行时版本通过 `GameAssets.resx` 嵌入 EXE；发布目录不需要任何松散资源。

## 构建与验证

需要 Windows x64 与 .NET 10 SDK：

```powershell
& .\tools\prepare_assets.ps1
dotnet test .\DeskStealth.sln -c Release
dotnet format .\DeskStealth.sln --verify-no-changes --no-restore
dotnet publish .\src\DeskStealth.App\DeskStealth.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:PublishTrimmed=false `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None -p:DebugSymbols=false `
  -o .\artifacts\publish
& .\tools\verify_golden.ps1 -Configuration Release
```

完整验收结果和明确未验证项见 `VALIDATION.md`，视觉对照见 `design-qa.md`。
