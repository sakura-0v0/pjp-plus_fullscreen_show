# 迫击炮测距Plus 全屏绘制组件

一个专为 **Xbox Game Bar** 设计的轻量级全屏覆盖层绘制插件。通过极速轮询本地 JSON 文件，在游戏画面上实时绘制战术标记、文字提示、十字准星等元素，特别适用于战术射击游戏的辅助开发与信息展示。

## ✨ 功能特性

- **高性能覆盖绘制**：基于 UWP `Canvas` 与 Xbox Game Bar Widget SDK，可在全屏游戏上层流畅绘制。
- **物理坐标对齐**：自动处理屏幕 DPI 缩放与窗口偏移，保证标记精准锚定游戏画面位置。
- **即插即用对接**：只需向指定临时文件夹写入 `xbox_elements.json`，无需 HTTP 服务器，零网络开销。
- **自适应刷新**：窗口大小改变时自动重绘，支持 Game Bar 钉选、穿透点击等交互模式。
- **Python 客户端示例**：提供 `XboxDrawServer` 类库，API 与原版 HTTP 模式完全兼容。

## 🧱 工作原理

`
[Python 客户端]                     [UWP 全屏绘制组件]
     │                                    │
     ├─ 更新标签数据                        │
     │   set_label / set_many_labels      │
     ▼                                    │
 写入 JSON 文件 ◄─────────────────── 轮询读取 (16ms 间隔)
 (xbox_elements.json)                     │
     │                                    │
     └──────────────► 绘制到 Game Bar 覆盖层 Canvas
`

- **客户端**：负责生成标签数据（位置、颜色、文字、线条等），并写入固定路径的 JSON 文件。
- **绘制组件**：作为 Xbox Game Bar Widget 运行，高频读取该 JSON 文件，将其内容转化为 UI 元素并实时渲染。

## 📁 文件结构

- `CanvasPage.xaml / .xaml.cs`：主绘图页面，处理 JSON 解析与 UI 渲染。
- `DrawProtocol.cs`：定义 `DrawLabelRequest`、`DrawFrameResponse` 等数据契约（用于 JSON 反序列化）。
- `xbox_elements.json`：通信媒介，存放当前需要绘制的所有标签列表。

## 🔧 环境要求

- **操作系统**：Windows 10/11（需支持 Xbox Game Bar SDK）
- **开发环境**：Visual Studio 2022+，安装 **Xbox Game Bar SDK** 扩展
- **目标平台**：UWP（通用 Windows 平台）

## 🚀 快速上手

### 1. 部署绘制组件

1. 在 Visual Studio 中打开 UWP 项目。
2. 确保已安装 `Microsoft.Gaming.XboxGameBar` NuGet 包。
3. 编译并部署到本地计算机。
4. 按 `Win + G` 打开 Game Bar，在“小组件商店”中启用 **迫击炮测距Plus 全屏绘制组件**。

### 2. 使用 Python 客户端发送绘图指令

将提供的 `XboxDrawServer` 类集成到你的 Python 项目中：

```python
from xbox_draw_server import xbox_draw_server
```

# 初始化并启动服务（会自动创建临时文件夹）
```Python
xbox_draw_server.start()
```

# 在 (100, 100) 到 (500, 200) 区域显示文字
```Python
xbox_draw_server.show_text(
    label_id="my_text",
    box=(100, 100, 500, 200),
    text="Hello, Game Bar!",
    background="#FF0000",
    font_size=32
)
```

# 绘制一个绿色矩形框
```Python
xbox_draw_server.show_box(
    label_id="my_box",
    box=(200, 300, 400, 500),
    color="#00FF00",
    width=3
)xxxxxxxxxx xbox_draw_server.show_box(    label_id="my_box",    box=(200, 300, 400, 500),    color="#00FF00",    width=3)
```

# 在屏幕中心绘制十字准星
```python
xbox_draw_server.show_crosshair(
    label_id="cross",
    center_xy=(960, 540),
    size=120,
    color="#FF0000"
)
```

# 移除特定标签
```python
xbox_draw_server.remove_label("my_text")
```

# 程序退出时停止服务（会清空所有标签）
```python
xbox_draw_server.stop()
```



### 3. 配置偏移量（可选）

若需调整标记在屏幕上的整体偏移（例如配合游戏内 UI 偏移），可在 Python 端设置：

```python
xbox_draw_server.config_app = Config()  # 你的配置对象
xbox_draw_server.config_app.set("xbox_offset_x", 10)
xbox_draw_server.config_app.set("xbox_offset_y", -5)
```

之后所有通过 `set_label` 添加的标签都会自动应用该偏移。

## 📡 通信协议（JSON 格式）

绘制组件读取的 JSON 结构如下：

```json
{
  "labels": [
    {
      "id": "label_1",
      "box": [100, 100, 500, 200],
      "background": "#FF0000",
      "foreground": "#FFFFFF",
      "alpha": 180,
      "font_size": 24,
      "text": "示例文字",
      "text_background": "#000000",
      "box_line": { "width": 2, "offset": 0, "color": "#FFFF00" },
      "lines": [
        {
          "width": 3,
          "color": "#00FF00",
          "points": [[0, 0], [100, 100], [200, 0]]
        }
      ]
    }
  ]
}
```

> **注意**：坐标均使用**屏幕物理像素**，绘制组件会自动根据当前窗口位置与 DPI 进行转换。

## ⚙️ 控制面板说明

- **状态栏**：显示当前轮询状态、标签数量、错误计数及临时文件夹路径。
- **居中按钮**：将 Game Bar 小组件窗口重新居中。
- **清空按钮**：立即清除画布上所有已绘制标签（仅清除 UI，不影响 JSON 文件）。

当小组件处于 **钉选+穿透点击** 模式时，控制面板会自动隐藏，确保不遮挡游戏画面。

## 🐛 故障排查

| 现象 | 可能原因 | 解决方案 |
|------|----------|----------|
| 小组件无任何绘制 | JSON 文件路径不正确 | 检查 `ApplicationData.Current.TemporaryFolder` 下是否存在 `迫击炮测距-Plus\xbox_elements.json` |
| 标签位置偏移 | 屏幕 DPI 缩放 ≠ 100% 或窗口未居中 | 点击小组件上的 **居中按钮**；检查 Python 端是否设置了正确的偏移量 |
| 绘制闪烁或卡顿 | JSON 写入频率过高或文件被锁定 | 降低客户端 `set_label` 调用频率；确保使用原子写入（`tempfile` + `os.replace`） |
| `FileNotFoundException` | 客户端尚未创建 JSON 文件 | 启动 Python 服务端后稍等片刻，或手动创建对应文件夹 |

## 📦 依赖项说明

- **UWP 组件**：
  - `Microsoft.Gaming.XboxGameBar` (≥ 5.0)
  - Windows 10 SDK (≥ 10.0.19041.0)

- **Python 客户端**：
  - 仅标准库（`json`, `os`, `tempfile`, `threading`, `queue`）

## 📄 许可

本项目基于 MIT 协议开源，欢迎自由使用与二次开发。

## 🤝 贡献

如有问题或改进建议，欢迎提交 Issue 或 Pull Request。

---

> **提示**：本文档基于你提供的代码片段整理。若实际项目中包含更多细节（如 `DrawProtocol` 的具体实现、配置项说明等），建议一并补充到 README 中。
