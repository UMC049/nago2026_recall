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
| L / 滚轮上翻 | 打开历史记录（系统页第 4 页） |

## 分支 / 数值 / 条件

### 节点

每一行都可以标一个节点，分支树和跳转都靠它定位：

```json
"nodeId": "door",
"nodeTitle": "门口 · 第一次选择",
"effects": "affection += 1",
"gotoNode": "ending"
```

- `nodeId`：唯一 id（跳转目标）
- `nodeTitle`：分支树里显示的名字
- `effects`：经过这一行时执行
- `gotoNode` / `gotoLine`：推进时无条件跳到别处（用于支线汇合）

### 选项

```json
"choices": [
  { "text": "进去坐坐", "targetNode": "inside", "effects": "affection += 2" },
  { "text": "留在门口陪她一会", "targetNode": "stay",
    "condition": "affection >= 2",
    "showWhenLocked": true,
    "lockedText": "（你们还没熟到那个地步）" }
]
```

- 有 `choices` 的行不会自动推进，必须选
- `condition` 不满足时：默认**隐藏**；`showWhenLocked: true` 则显示为灰色不可点
- `targetNode` 优先，其次 `targetLine`，都没有就顺序往下

### 条件表达式

```
affection >= 3
met_alice == 1
visited(route_a)
!visited(route_b)
affection >= 2 && (trust > 0 || visited(ending_a))
```

- 变量名直接写（不存在时按 0 算）
- `visited(节点id)` 判断是否经过某节点
- 支持 `&&` `||` `!` 和括号
- 留空 = 总是成立

### 效果语句

```
affection += 1
trust -= 2
flag_met = 1
visit(route_a)
```

多条用分号：`affection += 1; flag_met = 1`

### 在菜单里看路线

系统页面的第三页「路线」会按时间顺序列出玩家做过的选择、跳到的场景，以及当前所有数值和已解锁节点数。没做过选择时显示空提示。

`chapter02.json` 是完整示例：两个节点的两级选择、数值解锁隐藏选项、支线汇合到同一结局。

### BGM 控制

每行除了「播放哪首」，还能发控制指令：

| `bgmAction` | 作用 |
|---|---|
| `keep`（默认） | 什么都不做，继续播当前 BGM |
| `stop` | 停止当前 BGM（可淡出、可边淡出边变调） |
| `pitch` | 只改变当前 BGM 的音调，不停止 |
| `restart` | 当前曲目从头重播 |

配套字段：

- `bgmFade`：淡入 / 淡出时长（秒）
- `bgmPitch`：目标音调倍率（1 = 原调，0.5 = 低八度，1.5 = 高）
- `bgmPitchDuration`：变调持续时长（秒），0 = 立即变调

示例——结局时让音乐缓慢降调并淡出：

```json
"bgm": "",
"bgmAction": "stop",
"bgmFade": 2.5,
"bgmPitch": 0.8,
"bgmPitchDuration": 2.0
```

> 如果同一行还填了 `bgm` 路径，`bgmAction` 会被忽略，直接播放那首（切换曲目本身就是交叉淡入淡出）。
> 停止后音调会自动复位成 1，不会影响下一首。

## 演出能力总览

| 类别 | 能力 | 关键字段 |
|---|---|---|
| **背景** | 交叉淡入淡出、等比裁切 | `background`、`backgroundFade` |
| **立绘** | 三槽位、入场动画、换表情、平移/缩放补间 | `actors`、`motion`、`character`/`expression`、`moveDuration` |
| **文字** | 像素字体、字号、颜色、描边、速度、瞬间、**停顿标记** | `style`、`[p:0.8]` |
| **文字特效** | 抖动 / 波浪 / 呼吸 / 故障 | `effect`、`effectStrength`、`effectSpeed` |
| **镜头** | 平移、缩放、晃动（不影响 UI） | `useCamera`、`camera` |
| **转场** | 黑场 / 白场 / 白闪 | `transition`、`transitionDuration` |
| **时序** | 舞台与文字同时/先后、自动等待、**演出顺序表** | `stageTiming`、`steps`、`autoDelay` |
| **音频** | BGM 播放/切换/停止/变调/重播、音效、**角色说话音** | `bgm*`、`sfx`、`voice*` |
| **分支** | 节点、选项、条件、效果、路线树 | `nodeId`、`choices`、`effects` |
| **系统** | 存档（含缩略图）、读档、跳转、记录、设置 | 运行时 UI |

## 系统页面（右上角「菜单」或 F1）

右上角常驻一个「菜单」按钮，点开是统一系统页面，分两页：

### 第 1 页 · 存档 / 读档

6 个槽位，每行有 **保存 / 读取 / 删除**。

存档记录的是**完整舞台快照**：剧本路径 + 行号 + 背景 + 所有立绘（槽位 / 图片 / 偏移 / 缩放 / 着色）+ BGM 与音量 + 数值变量 + 分支历史 + 时间戳 + 一行预览文字，另外附一张**存档缩略图**（保存时截的当前画面）。读档时先把舞台恢复到存档时的样子，再从那行重新播放台词，所以不会出现「读档后立绘没了」这种情况。

存档文件：`Application.persistentDataPath/vn_saves/slot_N.json`。本项目当前位于

```
C:\Users\22776\AppData\LocalLow\DefaultCompany\nago2026_recall\vn_saves\
```

### 第 2 页 · 设置

- **音乐音量** / **音效音量** 两个滑块，拖动立即生效
- 实际音量 = 剧本里每行给的音量 × 主音量，互不覆盖
- **文字速度**：0.5x ~ 2.0x，直接乘在每行的 charsPerSecond 上
- **切换全屏**：切换窗口 / 全屏
- 设置写进 `PlayerPrefs`（`vn.audio.bgm` / `vn.audio.sfx` / `vn.textSpeed`），关掉游戏再开仍然保留

### 第 3 页 · 路线

分支树：按顺序列出玩家做过的每个选择（节点 → 选了什么 → 去了哪），
下方附当前所有数值和已解锁节点数量，方便作者自测分支逻辑。

### 第 4 页 · 记录（L 键 / 滚轮上翻）

按时间顺序列出已显示的台词，带说话人名和分页。最新内容在最后一页。

## 演出细节

### 文字停顿

台词里可以直接写停顿标记，打字机遇到就停：

```
……[p:1.2]你来了。
[p:0.5]（沉默）
```

- `[p]` = 停 0.5 秒
- `[p:0.8]` = 停 0.8 秒（0 ~ 10 秒）

标记不会显示出来，历史记录里也会自动去掉。按住 Ctrl 快进时会跳过停顿。

### 舞台与文字的先后

默认是**同时**：立绘开始淡入、镜头开始推、文字同时开始打。

想让「先演完再说话」，把 `stageTiming` 设成 `wait`：

```json
"stageTiming": "wait",
"transition": "fadeBlack",
"transitionDuration": 0.6,
"text": "……你终于醒了。"
```

播放器会算出这一行里最长的舞台动画（转场 / 镜头平移 / 晃动 / 立绘入场），
等它播完再开始打字。按住 Ctrl 可以跳过等待。

### 演出顺序表（时间轴）

一行里要「先演一段再说话」，除了 `stageTiming: "wait"`，还能用顺序表精确编排：

```json
"steps": [
  { "at": 0.0,  "label": "黑场",     "transition": true, "bgm": true },
  { "at": 0.35, "label": "换景推镜", "background": true, "actors": true, "camera": true, "sfx": true },
  { "at": 1.2,  "label": "开口",     "text": true }
]
```

- `at`：相对本行开始的秒数，按从小到大执行
- **同一行勾选多个动作 = 同时发生**（比如「镜头晃动 + 立绘入场」）
- 可用动作：`background` / `actors` / `camera` / `transition` / `text` / `sfx` / `bgm`
- 没有 `text` 步骤时，序列结束会自动开始打字
- 序列播放中点击屏幕 = 跳过剩余演出，立刻补齐舞台和文字

编辑器里在「舞台 / 音频」下方有独立的**「演出顺序表」**，每条一行，时间、备注、七个动作勾选框都在一起。

留空则回到默认流程（背景/立绘/镜头立刻应用，文字随后开始）。

### 字体

字体放在 `Assets/VN/Fonts/`，编辑器下拉会自动列出该目录下所有 `.ttf`。

**按角色记忆**：在立绘配置里给角色选一个「角色字体」，之后这个角色的台词都会自动用它。
旁白（没有角色）用播放器的默认字体。

```json
"actors": [{
  "slot": "center", "character": "she",
  "font": "Assets/VN/Fonts/my-font.ttf"
}]
```

**行级覆盖**：某一行想临时换字体，在「文字演出」里选「字体」，优先级高于角色字体。

优先级：**行级 `style.font` → 角色 `actor.font` → 播放器默认 `dialogueFont`**。

> 像素字体建议字号用 12 的倍数（24 / 36 / 48 / 60），换字体后可能需要在「字号」上微调。

### 说话音（人声替身）

角色可以配一个「说话音」——用合成器或乐器音色代替人声，Undertale / 动物森友会那种效果。
在立绘配置里填一次，之后这个角色的所有台词都会用：

```json
"actors": [{
  "slot": "center", "character": "she",
  "voice": "Assets/VN/Resources/VN/Audio/Voice/voice_she.wav",
  "voiceMode": "char",
  "voiceVolume": 0.7,
  "voicePitchRandom": 0.06,
  "voiceEveryNChars": 1
}]
```

| 字段 | 说明 |
|---|---|
| `voice` | 短音频路径（建议 0.05 ~ 0.3 秒） |
| `voiceMode` | `char`：每 N 个字触发一次；`loop`：打字期间循环播放 |
| `voiceVolume` | 音量 |
| `voicePitchRandom` | 每次触发的音调随机幅度（0 = 完全一致，0.06 = 轻微变化，避免机械感） |
| `voiceEveryNChars` | 每隔几个字触发（char 模式） |

**停顿联动**：打字里写 `[p:0.8]` 时，`loop` 模式会**暂停**语音，停顿结束再继续；
`char` 模式本身不发声，所以自然静音。

**谁来发声（和字体同一套逻辑）**：

```
行级 voice  →  说话角色的 voice  →  VNPlayer.defaultVoice  →  静音
```

- **旁白**（没名字、也没指定 `speakerSlot`）不发声，避免沿用画面上角色的声音
- 有名字但角色 id 对不上时，如果场上只有**一个**角色配了语音，就认为是他
- 场上多个角色都配了语音时，必须靠 `speakerSlot` 或名字匹配来区分，不会再「随便抓一个」

**默认说话音**：在「舞台 / 音频」里有「默认说话音」，写在场景的 `VNPlayer` 上，
角色没单独配语音时用它兜底（比如旁白也想有声音）。

**行级覆盖**：某一行想换音色或换模式，直接在行上写 `voice` / `voiceMode` / `voiceVolume`。

示例素材：`Resources/VN/Audio/Voice/` 下有三个我用程序生成的方波音（`voice_she` / `voice_traveler` / `voice_loop_she`），
替换成你自己的合成器采样即可，文件名随意，在编辑器下拉里选。

### 表情切换

立绘可以只写「角色 + 表情」，播放器会自动去找 `Char/角色/表情.png`：

```json
"actors": [{
  "slot": "center",
  "character": "she",
  "expression": "smile"
}]
```

查找顺序：`Char/she/smile.png` → `Char/she/smile.jpg` → `Char/she.png` → `sprite` 字段的整路径。

**同一个角色换表情不会重播入场动画**（只做 0.12 秒的位置补间），换人才会走 fade/slide 等入场效果。

### 说话者高亮

```json
"speakerSlot": "center"
```

当前说话的角色保持原色，其他槽位的角色自动压暗。也可以用 `line.name` 去匹配 `actor.character`（如果没填 `speakerSlot`）。
两者都没有时不压暗，避免单人场景自己变暗。

### 转场

```json
"transition": "fadeBlack",
"transitionDuration": 0.5
```

可选 `fadeBlack`（黑场）/ `fadeWhite`（白场）/ `flash`（白闪）。
覆盖层在主 Canvas 内渲染，**不会盖住右上角「菜单」按钮**（那个是独立的高排序 Canvas）。

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
│   ├── VNState.cs               数值 / 已访问节点 / 条件表达式求值
│   ├── VNEffects.cs             效果语句执行
│   ├── VNChoicePanel.cs         运行时选项按钮
│   ├── VNSystemPanel.cs         右上角菜单 + 系统页面（存档 / 设置 / 路线）
│   └── Editor/
│       ├── VNSceneBuilder.cs    一键生成场景
│       ├── VNScriptEditorWindow.cs  剧本编辑器
│       └── VNImportPostprocessor.cs 素材自动导入设置
├── Resources/VN/Data/chapter01.json   示例剧本
└── Fonts/fusion-pixel-12px-zh_hans.ttf  像素字体
```

素材统一放在 `Assets/VN/Resources/VN/` 下：

```
Assets/VN/Resources/VN/
├── BG/            背景图
├── Char/          人物立绘（透明 PNG）
├── Audio/BGM/     背景音乐
├── Audio/SFX/     音效
└── Data/          剧本 JSON
```

放在 `Resources` 下有两个好处：编辑器里能按路径加载，**打包后也能用**（`Resources.Load`）。
`Assets/VN/Resources/` 和 `Assets/Sounds/` 下的图片/音频会被自动设成正确的导入格式
（Sprite + Point 过滤 + 无压缩），像素图不会糊。

剧本里的路径写工程内全路径，例如 `Assets/VN/Resources/VN/BG/bg_room.png`；
加载器会自动把它转成 `Resources` 相对路径供打包后使用。

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

## 撤销 / 重做

剧本编辑器支持撤销最近一步操作：

| 操作 | 快捷键 |
|---|---|
| 撤销 | `Ctrl + Z` |
| 重做 | `Ctrl + Y` |

工具栏也有 `↶ 撤销` / `↷ 重做` 按钮（栈空时自动置灰）。

**会记录的操作**：新建 / 复制 / 删除行、上移 / 下移、切换选中行、添加 / 删除选项。

**不会逐个字符记录**：输入框里的打字和滑块拖动不单独进栈（否则拖一次滑块会产生几十个快照）。
但只要你切到另一行，刚才的修改就会作为一个快照被记住——所以「改完一行 → 点下一行 → 发现改错了 → Ctrl+Z」是有效的。

打开 / 新建剧本时会清空撤销栈（避免跨文件回退）。

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
