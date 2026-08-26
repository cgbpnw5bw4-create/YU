# DeskStealth 验收报告

验收日期：2026-08-26（Asia/Shanghai）

## 结论

- Release 自动化测试：`52/52` 通过，零失败。
- 发布目录：仅 `DeskStealth.exe` 一个文件。
- 最终文件：`116,156,685` bytes；SHA-256 `8A24F981E19CE380756BD7E7CC5037CE7AE4F359EF24A342EAE5D818B1DD304D`。
- Golden 回归、150% DPI 真实窗口、单实例、连续 Esc、失焦时钟、时钟态移动和配置恢复均通过。
- 视觉 QA：`design-qa.md` 的最终结果为 `passed`。

## 自动化测试

命令：

```powershell
dotnet test .\DeskStealth.sln -c Release --no-restore
dotnet format .\DeskStealth.sln --verify-no-changes --no-restore
```

结果：

- `DeskStealth.Core.Tests`：`35/35`。
- `DeskStealth.App.Tests`：`17/17`。
- 合计：`52/52`。
- `dotnet format --verify-no-changes`：通过。

覆盖范围包括：

- 八方向移动归一化、步行/冲刺/蹲伏速度和 Ctrl 优先级。
- 体力消耗、0.75 秒恢复延迟和 2.5 秒充满。
- 掩体完全隐藏、70°/64px 视野、墙体遮挡、发现阈值和 1 秒衰减。
- 抓捕扣时、资料保留、冻结/保护、三次失败、计时、拾取、出口和计分。
- Clock/Ready/Playing/Won/Lost、1 秒准备门、下一图/重试。
- 三图边界、可达性、初始视野、28–36 秒八邻接连续步行路线。
- 30Hz 守卫时序、移动过程视线、等待和掩体参与的 50 秒安全路线搜索。
- 必经窄口至少 1.2 秒连续安全窗口。
- 设置原子替换、损坏 JSON 备份、数据归一化。
- 单实例事件唤醒、Esc 自动重复去抖、失焦清键和托盘失败回退。
- 100%/125%/150%/200% DPI 尺寸计算、显示工作区回收算法。
- `.resx` 嵌入图集尺寸和二值 Alpha，防止软边回归。

## Golden 与视觉证据

命令：

```powershell
& .\tools\verify_golden.ps1 -Configuration Release
```

结果：

- `game-golden.png`：`7B10330FE2159971DBED7FBBA4FEED6CBC21B4659C51FB812B49AE093D6D023E`。
- `clock-golden.png`：`FCFB90BECA357DAE06702AE7DF9C4AE6C49B22E4070F19D218B6FF926F479E47`。
- Source/implementation 同图对照：`artifacts/screenshots/design-comparison-final.png`。
- 最终 EXE 游戏态 150% DPI：`artifacts/screenshots/game-focused-perf-final-build-150dpi.png`，实际窗口 `384×240` physical px，对应 `256×160 DIP`。
- 最终 EXE 时钟态 150% DPI：`artifacts/screenshots/clock-focused-perf-final-build-150dpi.png`。

## 真实窗口与桌面行为

- 启动：真实本地日期/时间时钟，通过。
- 连续 Esc：单次按住发送 8 个 KeyDown 只切换一次；KeyUp 后下一次 Esc 正常返回，通过。
- 第二实例：第二进程在 3 秒内退出，系统中保持一个进程，现有窗口切入时钟，通过。
- 失焦：游戏态启动记事本取得焦点后，DeskStealth 立即显示时钟，通过。
- 时钟态窗口移动：3 次 `Shift+A` 将窗口从 `x=2124` 移到 `x=2088`，即 `24 DIP / 36 physical px`，通过。
- 托盘失败：注入初始化异常后 `ShowInTaskbar=true`，自动化测试通过。
- 配置损坏：损坏文件改名为 `settings.corrupt-*.json` 并恢复默认，自动化测试通过。
- 显示器/DPI：`WM_DISPLAYCHANGE`、`PerMonitorV2` 和工作区回收算法已覆盖；当前硬件实际窗口验证为 150%。

## 性能与资源

Windows CPU 百分比按 18 个逻辑处理器归一化。

- 最终 EXE 游戏态、窗口保持前台、30.09 秒：CPU `0.208%`，最大工作集 `86.44 MiB`，GDI `28→28`，USER 最大 `30`。
- 最终 EXE 时钟态、窗口保持前台、30.10 秒：CPU `0.014%`，最大工作集 `86.47 MiB`，GDI `28→28`，USER 最大 `30`。
- 前一发布候选连续运行约 38 分钟，并在结束前保持 Playing：总 CPU 时间给出的归一化上界约 `0.42%`，工作集约 `84.7 MiB`，GDI 为 `28`，总句柄从 `400` 降至 `397`，未见持续增长。证据：`artifacts/screenshots/soak-state-probe.png`。

结论：已观察值满足游戏态 `<3%`、时钟态 `<0.5%`、工作集 `<100MB`，GDI 句柄稳定。长时运行使用的是紧邻最终版的候选；最终发布哈希另做了分模式短测，但未再次完整等待 30 分钟。

## 发布检查

```powershell
& .\tools\verify_publish.ps1 -PublishDirectory .\artifacts\publish
```

- 目录仅含一个 `DeskStealth.exe`。
- `win-x64`、自包含、单文件、不裁剪、无调试符号、嵌入本机库。
- 图像资源通过 `.resx` 嵌入；发布目录无松散 PNG、JSON、DLL 或 PDB。
- 源码未引用音频、网络、全局热键或键盘钩子 API。
- Authenticode 状态：`NotSigned`，可能出现 SmartScreen 提示。

## 明确未验证

- 未在无 .NET 的干净 Windows 10/11 x64 机器上启动。当前 Windows Sandbox 环境不可免提升验证，需用 Sandbox 或第二台电脑补测。
- 未在实际 100%/125%/200% 显示器 DPI 上截图；这些比例已做尺寸算法测试，真实窗口只验证了当前 150% DPI。
- 未实际拔除显示器或执行 Windows 锁屏/解锁；对应 DPI、显示变化、失焦和工作区回收路径已分别测试，但物理事件仍未验证。
- 未提供代码签名证书，因此未执行签名或 SmartScreen 信誉验证。
