# VN 视觉小说模板

一个可编辑的视觉小说框架：背景 / 立绘 / 台词 / 音乐音效 / 文字演出，全部数据驱动。

> **文档导航**
> - 📘 [编辑器使用说明](编辑器使用说明.md) —— 编辑器怎么点、每个字段什么意思、常用工作流（**写剧情先看这个**）
> - 📗 本文 —— 运行时机制、JSON 字段全表、代码结构、扩展方式
> - 📝 [更新说明](../../CHANGELOG.md) —— 每个版本改了什么

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
- 想让这个选项直接进下一章，填 `targetChapter`（见下面的章节跳转）

### 章节跳转

剧本可以拆成多个 JSON（一章一个文件），在剧本里互相跳：

**播完这行自动进下一章**

```json
"nextChapter": "Assets/VN/Resources/Recall/Data/story03.json",
"nextChapterLine": 0
```

**某个选项直接进另一章**

```json
{ "text": "进入第三章", 
  "targetChapter": "Assets/VN/Resources/Recall/Data/story03.json",
  "targetChapterLine": 0 }
```

切换章节时：

| 保留 | 清空 |
|---|---|
| 数值变量 | 立绘（三个槽位全部清掉） |
| 已访问节点 | 镜头位置（回到原点） |
| 选择历史 | 当前选项面板 |
| 台词记录（log） | 打字机状态 |

所以「好感度、走过的路」会跨章节累积，而画面是干净的——新章节第一行重新铺背景和人物。

`targetChapterLine` / `nextChapterLine` 是**从 0 开始**的行号，留 0 就是从头。

### 章节过场

章节开始和切换时会自动播一张全屏卡片，显示这一章的标题：

```
        第一章
         回忆
                   ← 黑场
        点击继续
```

- 标题从剧本的 `title` 字段拆出来：`"第一章 回忆"` → 标签「第一章」+ 标题「回忆」
- 节奏：淡到全黑 0.8s → 文字淡入 0.5s → 停留 1.6s → 淡出 0.9s
- **淡出开始时才推进剧情**，所以新章节第一行正好从黑场里露出来
- 播放中点击屏幕 / 空格 / 回车可以跳过
- 读档（继续游戏）不会播卡片，直接回到存档位置

在 `VNPlayer` 上控制：

| 字段 | 默认 | 说明 |
|---|---|---|
| `showChapterCard` | true | 关掉就不播过场 |
| `chapterCardFadeToBlack` | 0.8 | 淡到全黑的时间 |
| `chapterCardHold` | 1.6 | 标题停留时间 |
| `chapterCardFadeOut` | 0.9 | 淡出时间 |

编辑器里「分支 / 选项 / 数值」组有「下一章」下拉，每个选项卡片里也有「或跳章节」，都会自动列出 `Resources/` 下的所有 JSON。

**调试整条线**：`VNPlayer` 上有个 `autoNextChapter` 开关。打开后，剧本播完会自动按文件名顺序进入下一章
（chapter01 → chapter02 → chapter03），不用每章都配 `nextChapter`。
正式发布建议关掉它，用剧本里的 `nextChapter` 显式控制——避免改文件名把流程弄乱。

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

### 多音轨（同时播放）

`bgm` 字段只能管一条主音乐。想同时放「音乐 + 环境音 + 雨声」，用 `audio` 列表：

```json
"audio": [
  { "track": "bgm",     "clip": ".../雨夜.ogg", "volume": 0.6, "fade": 1.0, "loop": true },
  { "track": "ambient", "clip": ".../雨声.ogg", "volume": 0.4, "fade": 1.5, "loop": true, "at": 0 },
  { "track": "rain",    "clip": ".../雷.ogg",   "volume": 0.8, "fade": 0.3, "loop": false, "at": 3.5 }
]
```

规则：

- **音轨名相同 = 互相替换**（同一轨只留一条，自动交叉淡入淡出）
- **音轨名不同 = 同时发声**，各自独立音量 / 淡入淡出 / 循环 / 音调
- `at` 是相对本行开始的秒数，播放器会按时间调度
- `action` 支持 `play` / `stop` / `pitch`
- 音轨名随便起：`bgm`、`ambient`、`rain`、`heartbeat`……

编辑器里「多音轨（可同时播放）」折叠组就是这张表，每条一行。

**和演出顺序表配合**：如果这一行用了顺序表，把某一步的 `音轨` 勾上，就会在那个时间点触发本行的整组音轨指令（忽略各自的 `at`）——适合「镜头推近的同时音乐进来」这种。

常用音轨建议：

| 音轨 | 放什么 |
|---|---|
| `bgm` | 主旋律（切换会交叉淡入） |
| `ambient` | 环境底噪，长时间不变 |
| `sfx2` 等 | 需要独立控制的一次性音效 |
| `voice` | 说话音是独立通道，不需要写在 audio 里 |

### BGM 控制

**BGM 默认循环播放**（AudioSource.loop = true），一首歌播完会自动从头继续，不需要额外设置。

每行除了「播放哪首」，还能发控制指令：

| `bgmAction` | 作用 | 会不会停 | 会不会改音调 |
|---|---|---|---|
| `keep`（默认） | 什么都不做 | – | – |
| `stop` | 停止当前 BGM | ✅ 淡出到 0 | ✅ 下坠 |
| `fade` | 渐变到指定音量（不停止） | ❌ | ✅ |
| `pitch` | 只改变音调 | ❌ | ✅ |
| `restart` | 当前曲目从头重播 | 瞬间停再播 | 复位 1 |

配套字段：

- `bgmFade`：淡入 / 淡出时长（秒）
- `bgmFadeIn`：**只用于播放时的淡入**（0 = 用 bgmFade）
- `bgmVolume`：`play` 时是起始音量，`fade` 时是**目标音量**
- `bgmPitchStart`：**播放时的起始音调**（1 = 原调，0.9 = 略低沉）
- `bgmPitch`：`stop` / `fade` / `pitch` 的目标音调
- `bgmPitchDuration`：变调持续时长；0 = 立即变，> 0 = 边淡边变
- `bgmResume`：**同一首 BGM 重新播放时，从上次停止的位置继续**

**音调不会污染后面的歌**：切换新曲目时，播放器会把音调复位成 1。
所以你在 A 段把音乐压到 0.75 变低沉，切到 B 段时 B 是正常的 1.0。

### 从上次进度继续

同一首 BGM 被停掉之后再放，默认会从头开始。勾上 `bgmResume`，就会从**上次停止的位置**接着播：

```json
"bgm": "BGM/雨夜.ogg",
"bgmResume": true,
"bgmFadeIn": 0.6
```

适合「进战斗时音乐停掉，打完回来接着放」这种。停止的位置会被记住（按曲目分开记）。

示例——音乐渐渐变弱变低（但不停）：

```json
"bgmAction": "fade",
"bgmVolume": 0.15,
"bgmFade": 1.2,
"bgmPitch": 0.75,
"bgmPitchDuration": 1.2
```

示例——完全停掉，边淡出边下坠：

```json
"bgmAction": "stop",
"bgmFade": 2.5,
"bgmPitch": 0.8,
"bgmPitchDuration": 2.0
```

> 如果同一行还填了 `bgm` 路径，`bgmAction` 会被忽略，直接播放那首（切换曲目本身就是交叉淡入淡出）。

## 演出能力总览

| 类别 | 能力 | 关键字段 |
|---|---|---|
| **背景** | 交叉淡入淡出、等比裁切 | `background`、`backgroundFade` |
| **立绘** | 三槽位、入场动画、换表情、平移/缩放补间、单角色退场、全体退场 | `actors`、`hide`、`clearActors`、`motion`、`character`/`expression`、`moveDuration` |
| **文字** | 像素字体、字号、颜色、描边、速度、瞬间、**停顿标记** | `style`、`[p:0.8]` |
| **文字特效** | 抖动 / 波浪 / 呼吸 / 故障 | `effect`、`effectStrength`、`effectSpeed` |
| **镜头** | 平移、缩放、晃动（不影响 UI） | `useCamera`、`camera` |
| **转场** | 黑场 / 白场 / 白闪 | `transition`、`transitionDuration` |
| **视觉** | 失明 / 模糊 / 去色滤镜（只影响背景层） | `useBlind`、`blind`、`blindFade` |
| **时序** | 舞台与文字同时/先后、自动等待、**演出顺序表** | `stageTiming`、`steps`、`autoDelay` |
| **音频** | BGM 播放/切换/停止/变调/重播、音效、角色说话音、**多音轨同时播放** | `bgm*`、`sfx`、`voice*`、`audio` |
| **分支** | 节点、选项、条件、效果、路线树 | `nodeId`、`choices`、`effects` |
| **系统** | 存档（含缩略图）、读档、跳转、记录、设置 | 运行时 UI |

## 主菜单

启动时先显示标题画面（`VNPlayer.showTitleOnStart`，默认开）：

```
新游戏      清空数值和历史，从 scriptAssetPath 的第一行开始
继续游戏    自动找最近一次存档并读入（没有存档时按钮变灰）
设置        打开系统页的设置页（音量 / 文字速度 / 全屏）
退出游戏    编辑器里停止播放，打包后退出程序
```

外观在 `VNPlayer` 上改：

| 字段 | 当前值 | 说明 |
|---|---|---|
| `gameTitle` | 回响RECALL | 主标题 |
| `gameSubtitle` | （空） | 副标题；**留空则不显示** |
| `titleBackground` | `BG/title.jpg` | 背景图；会等比裁切铺满，不会拉伸 |
| `titleMusic` | `Audio/BGM/tt.ogg` | 标题画面循环音乐 |
| `titleMusicVolume` | 0.6 | 主菜单音乐音量 |

**标题音乐的行为**：

- 进入标题 → 淡入到 `titleMusicVolume`（1.2 秒）
- 点「新游戏」「继续游戏」→ 淡出后停止
- 从游戏返回标题 → 重新播放
- 每次循环播放，不会断

想换歌：把新文件放进 `Audio/BGM/`，改 `VNPlayer` 的 `Title Music` 字段，或把文件名改成 `tt`。

想换标题背景：把新图放进 `Assets/VN/Resources/Recall/Arts/BG/`，在 `VNPlayer` 的 `Title Background` 里选，或者改名为 `title` 让它自动生效。

背景按 **EnvelopeParent** 处理——等比放大到铺满屏幕，裁掉溢出部分，所以任何分辨率下都不会被拉变形。

**鼠标视差**：标题画面会随着鼠标轻微平移背景，露出的角落内容视差感，但：

- 只移动背景层，标题和按钮位置完全不动
- 背景预先放大 `parallaxZoom`（默认 1.12），平移范围限制在放大余量之内，所以**永远不会露出黑边**

| 字段 | 默认 | 说明 |
|---|---|---|
| `parallaxStrength` | 1.0 | 移动幅度，0 = 关闭 |
| `parallaxZoom` | 1.12 | 背景额外放大倍数（越大可移动范围越大） |
| `parallaxSmooth` | 0.12 | 跟随鼠标的平滑时间（秒） |

想跳过标题直接进剧情，把 `showTitleOnStart` 关掉。

> 标题画面用的是独立 Canvas（sortingOrder 100），系统面板在它上面（200），
> 所以从标题打开设置时，设置面板会正确盖在标题之上。

## 系统页面（右上角「菜单」或 F1）

右上角常驻一个「菜单」按钮，点开是统一系统页面，分两页：

### 结局 / CG / 视频

剧本最后放一条**结局通用行**（story03 已经配好了），到达后显示结局画面并结束游戏：

```json
{
  "background": "Assets/VN/Resources/Recall/Arts/CG/ending01.jpg",
  "backgroundFade": 1.5,
  "text": "demo到此结束，非常非常感谢游玩……",
  "suppressActors": true,
  "clearActors": true,
  "endGame": true,
  "endDelay": 6
}
```

**当前 story03 的结局**：夜间海面 CG 作背景（等比裁切，不拉伸）+ 结语写在对话框里，台词打完后停 6 秒、音乐淡出，然后自动退出。

| 字段 | 说明 |
|---|---|
| `background` | 想在 CG 上显示台词，就把 CG 填在这里（背景层会等比裁切，不拉伸） |
| `endingCg` | 独立全屏 CG 层（**会盖住对话框**，适合没台词的纯 CG 结局） |
| `endingVideo` | 结局视频（填了就优先播视频，需要导入成 VideoClip） |
| `endingLoop` | 是否循环播放 |
| `suppressActors` | 本行不显示任何立绘 |
| `endGame` | 到达后结束游戏（编辑器停止播放，打包后退出程序） |
| `endDelay` | **打字结束之后**再等几秒才退出 |

**视频**：把 `.mp4` 拖进 `Assets/VN/Resources/`，Unity 会导入成 VideoClip，然后在编辑器的「结局 / CG / 视频」组里选。
视频用 RenderTexture 全屏播放，带原声。

**做新结局**：复制这条结局行，改 `background` / `endingCg` / `endingVideo` 和前面的剧情就行——`endGame` 会负责收尾。

### 自动存档

存档页最上面多了一栏**「自动存档」**：

- 剧情走到**有选项的行**时，自动存一份（不占你的 6 个手动槽）
- 独立文件：`vn_saves/auto.json` + `auto.png`
- 存档页里可以「读取」或「清除」
- 开关在 `VNPlayer` 的 `autoSaveOnChoice`（默认开）

自动存档没有提示音和弹窗，安静保存，避免打断剧情。

### 第 1 页 · 存档 / 读档

6 个槽位，每行有 **保存 / 读取 / 删除**。

存档记录的是**完整舞台快照**：剧本路径 + 行号 + 背景 + 所有立绘（槽位 / 图片 / 偏移 / 缩放 / 着色）+ BGM 与音量 + 数值变量 + 分支历史 + 时间戳 + 一行预览文字，另外附一张**存档缩略图**（保存时截的当前画面）。读档时先把舞台恢复到存档时的样子，再从那行重新播放台词，所以不会出现「读档后立绘没了」这种情况。

**操作音效**：保存成功播 `saveSfx`，读档成功播 `loadSfx`，删除播 `deleteSfx`（默认都用 `Audio/SFX/load&save.wav`）。

| 字段 | 默认 | 说明 |
|---|---|---|
| `saveSfx` | `SFX/load&save.wav` | 保存成功 |
| `loadSfx` | `SFX/load&save.wav` | 读档成功 |
| `deleteSfx` | （空 = 用 saveSfx） | 删除存档 |
| `systemSfxVolume` | 0.8 | 系统音效音量 |

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

### 失明 / 视觉滤镜

模拟「视力衰退、失明」的视角：**去色 → 提亮 → 模糊 → 轻微扭曲 → 边缘收缩**。
只作用于背景层，对话框、名牌、菜单都不受影响。

```json
"useBlind": true,
"blind": 0.35,
"blindFade": 3.0
```

| 字段 | 说明 |
|---|---|
| `useBlind` | 这一行是否改变滤镜状态 |
| `blind` | 强度 0 ~ 1（0 = 正常，1 = 完全失明） |
| `blindFade` | 从当前强度渐变到目标值需要几秒 |

**渐变是累积的**：上一行 0.35、这一行 0.7，就从 0.35 平滑过渡到 0.7，不是重新开始。
想恢复正常写 `"blind": 0`。

效果参数在 `Resources/Shaders/VNBlindEffect.shader` 顶部，可以直接改：

| 参数 | 默认 | 作用 |
|---|---|---|
| `_Brightness` | 1.55 | 提亮倍数 |
| `_Contrast` | 0.85 | 对比度（越低越灰） |
| `_BlurPixels` | 5 | 模糊强度（像素） |
| `_WarpStrength` | 0.018 | 扭曲幅度 |
| `_VignetteStrength` | 0.7 | 边缘暗角 |

> 实现方式是给背景 Image 挂一个自定义 UI 材质，逐帧更新 `_BlindAmount`，
> 不需要额外后处理相机，Screen Space Overlay 也能用。

### 角色卡

`Resources/<项目>/Data/characters.json` 里给每个角色配一次：默认立绘、说话音、字体。
之后编辑剧情只要选一下角色，这些就自动带上，不用每行重复填。

**可视化编辑**：菜单 `VN → 角色卡`（不用手改 JSON）

```
┌ 角色（5）───────┐ ┌ 凛（lin）──────────────┐
│ 凛（lin）       │ │ id: [lin]              │
│ 薇（wei）       │ │ 显示名: [凛]            │
│ 我（me）        │ │ 默认立绘: [she/normal ▾]│ ← 带预览
│ ？？？（unknown）│ │ 语音文件: [voice_she ▾] │
│ 外来者1         │ │ 模式: [char ▾]          │
└─────────────────┘ │ 音量 / 抖动 / 每几字    │
                    │ 字体: [（默认）▾]        │
                    └────────────────────────┘
```

- 左上「＋ 新建角色」、列表下方 `↑↓` 排序、`✕ 删除`
- 立绘选完会显示缩略图预览
- 改完点「保存」（工具栏会显示 `● 未保存`）
- 语音模式选 `none` 时，音量/抖动/间隔会自动隐藏（因为是静音角色）

```json
{
  "characters": [
    {
      "id": "lin",
      "displayName": "凛",
      "portrait": "Assets/VN/Resources/Recall/Arts/Char/she/normal.png",
      "voice": "Assets/VN/Resources/Recall/Audio/Voice/voice_she.wav",
      "voiceMode": "char",
      "voiceVolume": 0.7,
      "voicePitchRandom": 0.06,
      "voiceEveryNChars": 1,
      "font": ""
    }
  ]
}
```

| 字段 | 说明 |
|---|---|
| `id` | 内部 id（英文/拼音），剧本里很少直接写 |
| `displayName` | 剧本里的说话人，如「凛」「薇」「我」 |
| `portrait` | 默认立绘；**留空 = 这个角色不出立绘**（比如第一人称「我」） |
| `portraitEnabled` | **是否自动出图**（关掉 = 只说话不出图） |
| `portraitSlot` | 立绘固定位置 `left` / `center` / `right` |
| `voice` | 说话音；留空则不出声 |
| `voiceMode` | `char` / `loop` / **`none`（不发声）** |
| `font` | 角色专用字体（可空） |

**立绘现在由角色卡驱动**：剧本行只要写说话人，播放器就会按角色卡的 `portraitEnabled` + `portraitSlot` 自动出图，不用每行手配立绘栏。

优先级：**行级 `actors` > 角色卡自动立绘**（手配过就用手配的）。

想彻底不出图（结局 / CG 段）：勾剧本行的「本行不显示立绘」，或者把角色的 `portraitEnabled` 关掉。

**编辑器里怎么用**：

1. 「说话人」右边点 **「角色卡 ▾」** → 选角色，自动填好名字
2. 下面会显示 `角色卡：凛（lin）· 立绘 · 语音`
3. 点 **「套用」** → 立绘和语音一次填到这一行

**保存后自动同步剧本**：点「保存」时，会自动扫描 `Data/` 下所有剧本，凡是 `character` 指向该角色的立绘条目：

- 角色卡**有立绘** → 更新成角色卡里的路径
- 角色卡**没立绘**（比如把薇的立绘删了）→ 从剧本里**移除**这条，不会再出现「第一章没了、第二章还在」的情况
- 底部会显示同步结果，例如 `已同步 2 个剧本：更新 0 处立绘，移除 2 处`

**缺失提醒**：如果有剧本引用了角色卡里**已经不存在的角色**（角色被删除），窗口底部会显示黄色警告：

```
⚠ 有 2 处剧本引用了角色卡里已经不存在的角色：
   she（chapter02.json）
   ghost（story02.json）
```

打开窗口时就会检查一次，不用先保存。

> 自动同步只处理 `character` 字段非空的立绘条目。如果你在剧本里手动配了立绘但没填 `character`，不会被覆盖。

**当前三章的配置情况**（已批量应用）：

| 章节 | 立绘出现在 |
|---|---|
| story01 | 第 7 行「凛」露面露脸；第 20 行切到铁匠铺，清场后「薇」居中 |
| story02 | 第 4 行「薇」出场（center） |
| story03 | 第 2 行「薇」居中；第 53 行「凛？」出现（left，两人同场） |

- 每章第一行自动 `clearActors`，从干净画面开始
- 「？？？」（凛还没露面）只有声音、不出立绘

**配音匹配顺序**（不会串音）：

```
行级 voice → 角色卡 → 场上角色 → 默认说话音 → 静音
```

`voiceMode: "none"` 的角色（如「我」）永远是静音，不会借用别人的声音。

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
  "voice": "Assets/VN/Resources/Recall/Audio/Voice/voice_she.wav",
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

示例素材：`Resources/Recall/Audio/Voice/` 下有三个我用程序生成的方波音（`voice_she` / `voice_traveler` / `voice_loop_she`），
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
│       ├── VNCharacterEditorWindow.cs 角色卡编辑器
│       ├── VNDebugLauncherWindow.cs 调试启动器
│       ├── VNProjectPopup.cs    新建项目弹窗
│       ├── VNInputPopup.cs      通用输入弹窗（重命名）
│       ├── VNBuilder.cs         一键打包
│       └── VNImportPostprocessor.cs 素材自动导入设置
├── Effects/
│   ├── VNTextEffect.cs          文字特效（抖动/波浪/呼吸/故障）
│   └── VNStageEffect.cs         自定义效果插件接口 + 注册表
├── Examples/
│   └── VNExampleEffects.cs      4 个插件示例
├── Resources/
│   ├── Shaders/VNBlindEffect.shader
│   └── Recall/                  ← 一个项目 = 一个文件夹
│       ├── Arts/
│       │   ├── BG/                 背景
│       │   ├── CG/                 结局图 / 全屏插画
│       │   └── Char/<角色id>/      立绘
│       ├── Audio/
│       │   ├── BGM/
│       │   ├── SFX/
│       │   └── Voice/
│       └── Data/
│           ├── characters.json     角色卡
│           ├── story01.json        章节
│           ├── story02.json
│           └── story03.json
└── Fonts/fusion-pixel-12px-zh_hans.ttf  像素字体
```

**多项目**：`Resources/` 下的每个一级文件夹是一个独立项目，**自带一整套资源**：

```
Assets/VN/Resources/
├── Shaders/          框架共用
├── Recall/           项目 A
│   ├── Arts/{BG,CG,Char}
│   ├── Audio/{BGM,SFX,Voice}
│   └── Data/{characters.json, story*.json}
└── 另一个游戏/        项目 B（结构完全一样）
```

- 项目名 = 文件夹名，支持中文
- 新建项目时编辑器会自动建好整套模板目录 + 空角色卡
- 字体（`Assets/VN/Fonts`）和 Shader（`Resources/Shaders`）是所有项目共用的

`VNPlayer` 上对应三个字段：`projectId`（项目名）、`startChapterFile`（起始章节文件名）、
`characterSetFile`（角色卡文件名）。路径自动推导：

```
<projectId>/Data/<章节>          <projectId>/Arts/BG
<projectId>/Data/characters.json <projectId>/Arts/CG
<projectId>/Arts/Char/<角色id>    <projectId>/Audio/{BGM,SFX,Voice}
```

章节跳转 `nextChapter` 只写文件名（如 `story02.json`）时会自动拼到当前项目下，写完整路径则原样使用。

放在 `Resources` 下有两个好处：编辑器里能按路径加载，**打包后也能用**（`Resources.Load`）。
`Assets/VN/Resources/` 和 `Assets/Sounds/` 下的图片/音频会被自动设成正确的导入格式
（Sprite + Point 过滤 + 无压缩），像素图不会糊。

剧本里的路径写工程内全路径，例如 `Assets/VN/Resources/Recall/Arts/BG/bg_room.png`；
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
- `hide: true` = 清掉该槽位立绘（单个角色退场）
- `clearActors: true` = 这一行开始时**清空所有立绘**（全体退场）
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

### 分支自动标识

左侧行列表会**自动**标出所有涉及分支、条件的行，不需要手动维护：

| 标记 | 含义 |
|---|---|
| ◆ | 节点定义（这一行设了节点 ID） |
| ◇ | 这一行有选项 |
| ？ | 有选项带条件判定（如 `affection >= 2`） |
| → | 有跳转（节点 / 行号 / 切换到别的章节） |
| # | 选项带数值效果（如 `L += 1`） |

行底色也会跟着变：**红色** = 有条件判定，**橙色** = 有分支，**蓝色** = 有跳转，**绿色** = 节点定义。

列表顶部还有一行统计：

```
节点 2 · 分支 1 · 条件 0 · 跳转 3 · 数值 0
```

改动任何行的选项或条件，标记和统计会立即跟着更新——写长剧本时能一眼看出哪里分叉、哪里还没接上。

## 重新生成场景

`VN → 构建场景（或重建）`（Ctrl+Shift+V）。会新建场景并覆盖 `Assets/VN/Scenes/VN.unity`。

设计分辨率是 **1920×1080（16:9）**。屏幕比例不是 16:9 时，内容按 16:9 居中留黑边，
不会被拉变形。想改基准，编辑 `VNSceneBuilder.ReferenceResolution` 后重建。

## 自定义演出效果（插件）

内置效果（文字 / 镜头 / 立绘动作 / 转场 / 滤镜）都是写死的。要加自己的，**不用改框架**：

```csharp
public class MyEffect : IVNStageEffect
{
    public string Id => "my_effect";          // 剧本里填这个
    public string DisplayName => "我的效果";

    public IEnumerator Play(VNEffectContext ctx)
    {
        // ctx.stageRoot / ctx.actorCenter / ctx.dialogueRoot ...
        yield return null;
    }
}
```

- 脚本放 `Assets` 下任意位置即可，`VNStageEffectRegistry` 会自动扫描
- 剧本行填 `customEffect: "my_effect"`，参数写 `customEffectParams: "dist=8;time=0.5"`
- 效果和默认演出同时开始，不阻塞推进
- 编辑器下拉在「自定义效果（插件）」组里，点 `↻` 重新扫描

`VNEffectContext` 提供的可用节点：

| 成员 | 类型 |
|---|---|
| `player` | `VNPlayer` |
| `line` / `lineIndex` / `parameters` | `VNLine` / `int` / `string` |
| `stageRoot` | `RectTransform` |
| `backgroundA` / `backgroundB` | `Image` |
| `actorLeft` / `actorCenter` / `actorRight` | `Image` |
| `dialogueRoot` | `RectTransform` |
| `GetActor(slot)` / `GetParam(key, def)` / `GetParamFloat(key, def)` | 便捷方法 |

示例见 `Assets/VN/Examples/VNExampleEffects.cs`（4 个可直接抄的实现）。

> 实现细节：`Assets/VN/Scripts/VNStageEffect.cs`。
> 打包成 IL2CPP 时如果不引用到插件类型，可能被裁剪——Mono 后端（Windows Standalone）不受影响。

---

## 注意事项

- 字号建议用 **12 的倍数**（24 / 36 / 48 / 60），像素字体在这个网格下最锐利
- Unity 编辑器在窗口失焦时会暂停主循环，导致淡入淡出/打字机停在半路——
  已设置 `PlayerSettings.runInBackground = true`，但若仍遇到，点一下 Game 视图即可
- 立绘建议透明 PNG、画布高度一致，换表情时脚不会跳
- 打包发布时，素材若不在 `Resources` 下需要用 Addressables 或放进 `Resources`；
  当前实现编辑器与 Play 模式用 `AssetDatabase` 直接按路径读
