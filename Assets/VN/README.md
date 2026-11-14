# VN 视觉小说模板

一个可编辑的视觉小说框架：背景 / 立绘 / 台词 / 音乐音效 / 文字演出，全部数据驱动。

## 快速开始

1. 打开场景 `Assets/VN/Scenes/VN.unity`
2. 点 Play
3. 用 `VN → 剧本编辑器`（Ctrl+Shift+E）编辑剧本

## 操作

| 按键 | 作用 |
|---|---|
| 左键 / 空格 / 回车 | 推进；文字播放中则立即播完 |
| 按住 Ctrl | 加速打字 |
| A | 开关自动播放 |
| R | 重新播放本行 |
| N | 跳到下一行（编辑器调试用） |
| F1 | 打开 / 关闭系统页面（也能点右上角「菜单」） |
| F5 | 快速存档（槽位 1） |
| F9 | 快速读档（槽位 1） |

## 系统页面（右上角「菜单」或 F1）

右上角常驻一个「菜单」按钮，点开是统一系统页面，分两页：

### 第 1 页 · 存档 / 读档

6 个槽位，每行有 **保存 / 读取 / 删除**。

存档记录的是**完整舞台快照**：剧本路径 + 行号 + 背景 + 所有立绘（槽位 / 图片 / 偏移 / 缩放 / 着色）+ BGM 与音量 + 时间戳 + 一行预览文字。读档时先把舞台恢复到存档时的样子，再从那行重新播放台词，所以不会出现「读档后立绘没了」这种情况。

存档文件：`Application.persistentDataPath/vn_saves/slot_N.json`。本项目当前位于

```
C:\Users\22776\AppData\LocalLow\DefaultCompany\nago2026_recall\vn_saves\
```

### 第 2 页 · 设置

- **音乐音量** / **音效音量** 两个滑块，拖动立即生效
- 实际音量 = 剧本里每行给的音量 × 主音量，互不覆盖
- 设置写进 `PlayerPrefs`（`vn.audio.bgm` / `vn.audio.sfx`），关掉游戏再开仍然保留

代码里也可以直接调：

```csharp
var snapshot = player.CaptureState(2);   // 抓当前状态（槽位 3）
VNSaveSystem.Write(snapshot);

var data = VNSaveSystem.Read(2);
player.RestoreState(data);               // 恢复舞台 + 定位到该行

player.audioManager.SetGlobalBgmVolume(0.6f);
player.audioManager.SetGlobalSfxVolume(0.8f);
```

想做自动存档，在 `VNPlayer.ShowLine` 末尾调 `QuickSave(0)` 即可；想做「继续游戏」，启动时判断 `VNSaveSystem.HasAny()` 再 `LoadSlot`。

## 目录结构

```
Assets/VN/
├── Scenes/VN.unity              生成的场景
├── Scripts/                     运行时代码
│   ├── VNTypes.cs               数据结构
│   ├── VNPlayer.cs              播放器（核心）
│   ├── VNTextEffect.cs          逐字文字特效
│   ├── VNAudioManager.cs        BGM 交叉淡入 / 音效
│   ├── VNAssetLoader.cs         资源加载
│   ├── VNSaveSystem.cs          存档读写（JSON / 6 槽位）
│   ├── VNSystemPanel.cs         右上角菜单 + 系统页面（存档页 / 设置页）
│   └── Editor/
│       ├── VNSceneBuilder.cs    一键生成场景
│       ├── VNScriptEditorWindow.cs  剧本编辑器
│       └── VNImportPostprocessor.cs 素材自动导入设置
├── Resources/VN/Data/chapter01.json   示例剧本
└── Fonts/fusion-pixel-12px-zh_hans.ttf  像素字体
```

素材可以放在任意位置（当前示例用 `Assets/Arts`、`Assets/Sounds`）。
`Assets/Arts/` 和 `Assets/Sounds/` 下的图片/音频会被自动设成正确的导入格式
（Sprite + Point 过滤 + 无压缩），像素图不会糊。

## 剧本格式

每行 = 一句台词 + 可选的舞台变化。路径用工程内路径（`Assets/...`）。

```json
{
  "name": "少女",
  "text": "你终于来了。",
  "background": "Assets/Arts/Background/xxx.jpg",
  "backgroundFade": 0.6,
  "bgm": "Assets/Sounds/Music/xxx.ogg",
  "bgmVolume": 0.65,
  "sfx": "Assets/Sounds/SFX/xxx.wav",
  "sfxVolume": 0.8,
  "actors": [
    { "slot": "center", "sprite": "Assets/Arts/Characters/she.png",
      "hide": false, "motion": "fade", "offsetX": 0, "offsetY": 40,
      "scale": 1.1, "tint": "#FFFFFF", "fadeDuration": 0.4 }
  ],
  "style": {
    "fontSize": 40, "charsPerSecond": 30, "effect": "wave",
    "effectStrength": 9, "effectSpeed": 1.2, "color": "#FFE9A8",
    "outline": true, "instant": false
  },
  "autoDelay": 0,
  "note": "给自己看的备注"
}
```

### 字段说明

- `background` 留空 = 保持不变；切换时按 `backgroundFade` 秒交叉淡入淡出
- `bgm` 留空 = 保持不变；与当前相同则不会重启
- `actors` 留空 = 沿用上一行立绘
- `slot`：`left` / `center` / `right`（**必须小写**，JsonUtility 不认枚举字符串）
- `motion`：`fade` / `none` / `slideUp` / `slideSide` / `bounce` / `pop`
- `moveDuration`：**同一张图只改位置/大小时**的平移补间时长（默认 0.4 秒）。
  如果这一行用的图和上一行是同一张、并且槽位已经在显示，播放器不会重播入场动画，
  而是从当前位置平滑过渡到新位置/新大小。想做「她往前靠了一步」这种镜头就用它。
- `hide: true` = 清掉该槽位立绘
- `effect`：`none` / `shake`（抖动）/ `wave`（波浪）/ `pulse`（呼吸）/ `glitch`（故障）
- `effectStrength` 按 36 号字为基准，字越大位移越大
- `charsPerSecond: 0` 或 `instant: true` = 不做打字机
- `autoDelay > 0` 且开自动播放时，播完等待该秒数再继续

### 镜头

每行可以带一段镜头指令，**只作用于背景和立绘（Stage 容器），对话框和右上角按钮不受影响**：

```json
"useCamera": true,
"camera": {
  "offsetX": -30, "offsetY": 12, "zoom": 1.07,
  "duration": 0.7,
  "shake": "light", "shakeDuration": 0.4, "shakeStrength": 10
}
```

- `offsetX / offsetY`：整体平移（设计像素）
- `zoom`：整体缩放，1 = 原始
- `duration`：平移到目标的时间，镜头指令不写就保持上一行的位置
- `shake`：`none` / `light` / `medium` / `strong`，按强度叠加一段衰减抖动
- 想做「突然一震」，把 `duration` 调小（0.05）、`shake` 设 `strong`

## 编辑器里直接预览（不用 Play）

剧本编辑器右上角有 **`👁 预览`** 按钮：把当前选中行应用到场景里的 UI，**不进入 Play 模式**，
直接在 Game 视图里看到效果。适合反复微调立绘位置、缩放、镜头取景和字号。

注意：预览会真实修改场景对象（场景会变脏），不想要就 `Ctrl+Z` 或重新「构建场景」。
`▶ 播放` 则是完整跑一遍（含音频、打字机、补间），用来验证动态效果。

## 编辑工作流

打开 `VN → 剧本编辑器`：

- 左侧是行列表，点一行编辑；`＋新行` / `⧉复制` / `✕删除` / `↑↓` 排序
- 右侧可点资源下拉直接选背景、立绘、BGM、音效
- `字号` 有 24 / 36 / 48 / 60 快捷按钮；`速度` 有 慢/中/快/瞬间
- `▶ 播放` 会保存并从当前选中行开始跑

## 重新生成场景

`VN → 构建场景（或重建）`（Ctrl+Shift+V）。会新建场景并覆盖 `Assets/VN/Scenes/VN.unity`。

设计分辨率是 **1920×1080（16:9）**。屏幕比例不是 16:9 时，内容按 16:9 居中留黑边，
不会被拉变形。想改基准，编辑 `VNSceneBuilder.ReferenceResolution` 后重建。

## 注意事项

- 字号建议用 **12 的倍数**（24 / 36 / 48 / 60），像素字体在这个网格下最锐利
- Unity 编辑器在窗口失焦时会暂停主循环，导致淡入淡出/打字机停在半路——
  已设置 `PlayerSettings.runInBackground = true`，但若仍遇到，点一下 Game 视图即可
- 立绘建议透明 PNG、画布高度一致，换表情时脚不会跳
- 打包发布时，素材若不在 `Resources` 下需要用 Addressables 或放进 `Resources`；
  当前实现编辑器与 Play 模式用 `AssetDatabase` 直接按路径读
