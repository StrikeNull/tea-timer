# 一盏茶 · Windows 泡茶计时

一个小巧的 Windows 桌面泡茶计时器：选茶，点击开始，到时弹窗提醒。

![主窗口](docs/main-window.png)

## 功能

- 绿茶、红茶、乌龙、白茶、熟普洱和花草茶六种预设。
- 每种茶的时间分别保存，支持 1 秒至 99 分 59 秒。
- 紧凑主窗口，可拖动调整大小，并记住窗口尺寸。
- 茶类对应不同配色；主界面只保留选茶、倒计时和基础按钮。
- 独立配置页设置时间、提示音、置顶、角色和动画。
- Q 版蓝鲸女仆和 GPT 原创茶娘，包含待机、泡茶、完成三种状态。
- 暂停、继续、重置、再泡一杯；最小化后继续计时。
- 到时置顶弹窗、系统托盘通知和可关闭的提示音。
- 睡眠期间到期，会在唤醒后补发提醒。

## 使用

从仓库 Releases 下载 Windows 压缩包，解压后双击 `一盏茶.exe`。
程序使用 Windows 自带的 .NET Framework；Windows 10 / 11 无需安装 Python 或 Node。
角色素材已经嵌入 EXE，单独复制程序也可以使用。

点击茶类名称选择茶，倒入热水后点击「开始泡茶」。点击「配置」可修改每种茶的时间，点击「保存」生效。
计时中修改配置不会打断当前计时，新的时间用于下一次泡茶。

最小化后双击系统托盘茶杯图标可恢复。计时中点击关闭会收起到托盘；右键托盘选择「退出（停止计时）」可彻底退出。

设置保存在 `%LOCALAPPDATA%\YiZhanCha\settings.xml`。程序运行时不联网。
更多说明见 [使用说明.txt](使用说明.txt)。

## 从源码编译

在项目目录的 PowerShell 中执行：

```powershell
.\build.ps1
```

编译脚本使用 Windows .NET Framework 自带的 C# 编译器，输出 `一盏茶.exe`。
源码为 `TeaTimer.cs`、`CompactUi.cs`，图像资源位于 `assets/`。

## 验证

```powershell
$test = Start-Process '.\一盏茶.exe' -ArgumentList '--self-test', 'verification' -Wait -PassThru
Get-Content '.\verification\test-results.txt'
```

自检覆盖计时边界、暂停继续、单次提醒、睡眠恢复、六种预设、配置校验、运行中修改配置、窗口缩放、设置保存、重开恢复及旧配置迁移，并生成界面预览。

## 角色素材

两张角色图均通过内置 imagegen 工具生成，含透明背景和三个动作状态。
蓝鲸女仆参考了蓝发鲸鱼女仆的社区形象元素。当前「GPT 茶娘（原创）」是银白发、圆眼镜、薄荷绿女仆装的原创暂用设计，没有使用网络上的现成 GPT 娘参考图，也不是官方形象。
完整提示词与来源说明见 [素材生成记录](assets/生成记录.txt)。

## 泡茶时间参考

默认时间是普通杯泡的可调整起点；盖碗、功夫泡和不同茶叶请按包装说明调整。
绿茶、红茶、乌龙、白茶和花草茶参考 [Twinings 杯泡指南](https://twiningsusa.com/pages/how-to-brew-the-perfect-cup-of-tea)，熟普洱参考 [Harney & Sons Pu-Erh](https://www.harney.com/products/pu-erh)。
