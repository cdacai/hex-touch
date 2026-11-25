# HexTouch

一个常驻 Windows 托盘的语音输入按钮，依赖 [Hex](https://anoma.ly/hex/) 做本地转写。

点一下开始说话，再点一下结束，文字直接落到光标处。**不用按住快捷键，手可以继续敲键盘、点鼠标、做别的事。**

> HexTouch 是 Hex 的"遥控器"：它不包含语音模型，也不修改 Hex，只是调用 Hex 的命令行来开关听写。
> 所以使用前需要先装好 [Hex](https://anoma.ly/hex/)（本地离线转写，支持中文）。

**特别推荐 Windows 平板使用。** 没有键盘的平板上，按住快捷键这条路基本走不通；点屏幕上的悬浮按钮是最顺手的方式。

## 为什么

Hex 自带的听写方式是"按住快捷键说话"：按键期间那只手被占住，聊天、写代码、查资料都得停下来。

| | 原生 Hex | HexTouch |
|---|---|---|
| 开始 / 结束 | 按住键不放 | 点一下开始，再点一下结束 |
| 双手 | 一只手被按键占着 | 完全解放 |
| 无键盘设备（平板） | 几乎没法触发 | 点屏幕上的悬浮按钮 |

输入焦点方面两者一致：都**不抢焦点**，文字都落回原光标处，HexTouch 不改变这一点。

## 功能

- 托盘常驻，左键显示 / 隐藏悬浮按钮
- 悬浮按钮三颗：**主球**（录音 / 停止）、**回车**、**删除**
- 点按切换，不需要按住
- 悬浮窗带 `WS_EX_NOACTIVATE`，点击不会夺走输入框焦点
- 双击回车钮 = 插入换行（用剪贴板粘贴，绕过中文输入法吞 `Shift+Enter`）
- 可拖动，位置自动记住
- 开机自启（托盘菜单里勾选）
- 单文件 exe，不依赖 Python，不需要额外运行时（用系统自带的 .NET Framework 4.x）

## 安装

### 安装包（推荐）

运行 `HexTouchSetup.exe`：

| 操作 | 命令 |
|---|---|
| 图形安装（可选目录） | 双击 `HexTouchSetup.exe` |
| 静默安装到默认位置 | `HexTouchSetup.exe --silent` |
| 指定路径静默安装 | `HexTouchSetup.exe --silent --dir=D:\Tools\HexTouch` |
| 卸载 | 安装目录下的 `HexTouchUninstall.exe`，或「设置 → 应用」 |

安装会：释放主程序和卸载器 → 写开机自启 → 注册卸载入口 → 立即启动。

默认目录 `%LOCALAPPDATA%\HexTouch\`，装到用户目录，不需要管理员权限。

### 免安装

直接运行 `HexTouch.exe` 即可，不写任何东西到系统（除了自启项，可在菜单里关）。

### 从源码构建

需要 Windows 自带的 .NET Framework 4.x（Win10/11 都有，不用装 SDK）。

```cmd
build.cmd
```

依次生成：

1. `dist\HexTouch.exe` — 主程序
2. `dist\HexTouchUninstall.exe` — 卸载器
3. `dist\HexTouchSetup.exe` — 安装器（把上面两个嵌为资源）

编译器路径自动探测 `Framework64` / `Framework` 下的 `csc.exe`。

改图标后重新生成（只在构建期用 Python）：

```cmd
python tools\gen_icon.py
```

## 使用

| 操作 | 效果 |
|---|---|
| 左键点托盘图标 | 显示 / 隐藏悬浮按钮 |
| **点一下主球** | 开始录音（白底黑边音波 → 红底白边方块） |
| **再点一下主球** | 停止录音，转写并粘贴到原光标处 |
| **点回车钮** | 发送 `Enter`（配合聊天框「发送」） |
| **双击回车钮** | 插入换行 |
| **点删除钮** | 发送 `Backspace` |
| **按住拖动** | 挪位置（移动 8px 后进入拖动，位置会记住） |
| 右键托盘图标 | 唤起 Hex 窗口 / 开机自启动 / 退出 Hex / 退出 |

三颗圆按等腰三角形摆放：主球在左，回车在右下，删除在回车正上方。

默认位置按屏幕比例算：主球圆心在**屏宽 7/8** 处，球底距工作区底部 **335px**。

触摸屏上请**轻点**：按住超过 0.5 秒，Windows 会判成长按右键（系统手势，绕不开），所以设计成点按切换，不依赖按住。

## 工作原理

```
托盘图标（NotifyIcon）
   │ 左键点击：显示 / 隐藏
   ▼
悬浮按钮窗口（WinForms，WS_EX_NOACTIVATE，不抢焦点）
   ├─ 主球   → hex dictation start / stop
   ├─ 回车钮 → Enter；双击 = 插入换行
   └─ 删除钮 → Backspace
        │
        ▼
Hex CLI（%APPDATA%\hex\bin\hex.com）
        │
        ▼
Hex 本地模型转写 → 粘贴回原来的光标位置
```

几个关键设计：

- **不模拟"按住热键说话"**：触摸屏上按住会触发 Windows 的长按右键手势，所以改成点按切换
- **窗口带 `WS_EX_NOACTIVATE`**：点击悬浮按钮不会抢走输入框焦点，文字和回车才能落到正确位置
- **只调 Hex 的命令行**：不修改 Hex 本身，也不依赖它的热键
- **失败自愈**：Hex 没运行时点主球会自动拉起 `hex.exe`，约 2 秒后开始录音
- **状态轮询先看进程**：`hex.exe` 不在时跳过 CLI 调用，避免命令挂起把内部队列堵死；悬浮按钮隐藏时也不查录音状态，省掉每轮的进程创建

## 自启与退出

### 开机自启

托盘菜单里的「开机自启动」勾选即写入：

```
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
  值名: HexTouchTray
  值:   <程序绝对路径>
```

也可以命令行关掉：

```cmd
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v HexTouchTray /f
```

### 启动

```cmd
HexTouch.exe                  :: 启动，托盘出现图标
HexTouch.exe --show-ball      :: 启动并直接显示悬浮按钮
```

### 退出

| 方式 | 说明 |
|---|---|
| 托盘右键 → 退出 | 会弹确认框，并提示怎么重新打开 |
| 托盘右键 → 退出 Hex | 只关 Hex，托盘程序还在（同时收起悬浮按钮） |
| 任务管理器结束进程 | 兜底 |

退出后再打开：双击程序文件，或开始菜单里的 **HexTouch**（安装版才会创建快捷方式）。

### 命令行参数

| 参数 | 作用 |
|---|---|
| `--show-ball` | 启动后立即显示悬浮按钮 |
| `--pos=x,y` | 指定悬浮按钮初始位置（调试用） |
| `--selftest` | 自测：自动录音 4 秒后退出，用于检查 Hex 通路 |

## 托盘图标常驻

Windows 11 默认把新图标放进托盘溢出区（`^`）。第一次显示后：

`设置 → 个性化 → 任务栏 → 其他系统托盘图标` → 把 **HexTouch** 打开。

## 项目结构

```
.
├── src/
│   ├── HexTouch.cs          # 主程序：托盘、悬浮按钮、录音、换行、删除
│   ├── Installer.cs         # 安装器（把主程序和卸载器嵌进一个 exe）
│   └── Uninstaller.cs       # 卸载器（复制到临时目录再删自己）
├── assets/
│   └── app.ico              # 程序图标
├── tools/
│   └── gen_icon.py          # 构建期生成 app.ico（运行时不依赖 Python）
├── build.cmd                # 一键编译三个 exe 到 dist/
└── dist/                    # 编译产物（不入库）
```

运行时会在 exe 同目录生成：

| 文件 | 说明 |
|---|---|
| `hextouch.log` | 运行日志（超 512KB 自动清空） |
| `button.pos` | 悬浮按钮位置记忆（拖过之后才有） |

## 排错

- 看 `hextouch.log`：每次调用 `hex ...` 都会记录命令、退出码和输出摘要
- 托盘图标变灰 = Hex 没在运行；点一下主球会自动把它拉起来（约 2 秒）
- `dictation start failed` 之后会有 `hex app started directly` 和重试记录

## 已知限制

- **只对接 Hex**：程序调用的是 Hex 的命令行（`hex dictation start/stop`）。换成别的听写软件需要另写适配层，目前没有做
- Hex 的 `hex start` 命令在 Hex 未运行时会挂起不返回，所以程序里改为直接启动 `hex.exe` 进程
- 触摸点击存在系统层约 100–300ms 的判定延迟，按下反馈会稍晚
- 只支持 Windows（依赖 WinForms 和 `Shell_NotifyIcon`）

## License

MIT
