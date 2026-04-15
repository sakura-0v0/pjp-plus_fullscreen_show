"""
XboxDrawServer - Xbox 屏幕远程绘图服务模块 (本地文件读写版)
提供基于本地 JSON 文件的远程绘图服务，去除 HTTP 依赖。
核心机制：
- 服务端维护一个标签字典 (_labels)，每次变动立即序列化写入本地 JSON 文件
- 客户端通过极速轮询读取该 JSON 文件获取最新的标签列表

用法示例（与原版完全一致，无需修改调用代码）：
from xbox_draw_server import xbox_draw_server
xbox_draw_server.start()
xbox_draw_server.show_text("my_text", box=(100, 100, 500, 200), text="Hello")
xbox_draw_server.show_box("my_box", box=(100, 100, 500, 200), color="#FF0000")
xbox_draw_server.show_crosshair("cross", center_xy=(960, 540))
xbox_draw_server.remove_label("my_text")
xbox_draw_server.stop()

Attributes:
    xbox_draw_server (XboxDrawServer): 模块级全局单例实例，方便直接 import 使用
"""
import copy
import json
import os
import queue
import tempfile
import threading
import time

from config import Config


class XboxDrawServer:
    """基于本地 JSON 文件的远程屏幕绘图服务。

    启动时会自动在用户目录下创建 "迫击炮测距-Plus/xbox_elements.json" 文件，
    并在后续的标签变动中立即覆写该文件。

    Args:
        无 (为了保持调用兼容性，忽略了原版的 host 和 port 参数)

    Attributes:
        file_path (str): JSON 文件的绝对路径。
        _labels (dict[str, dict]): 内部标签字典，key 为标签 ID 字符串，value 为标签数据字典。
        _is_running (bool): 标记服务是否处于运行状态，防止未启动时写入文件。
    """
    config_app: Config | None = None
    def __init__(self, host=None, port=None):
        self.process_queue = queue.Queue()
        self.stop_event = threading.Event()
        # 忽略传入的 host 和 port，从环境变量获取用户目录，兼容 Windows 和 Linux
        app_data = os.path.expanduser("~\AppData\Local\Packages\d7488ba8-6f62-4db1-965c-633de8e74a68_1ywxxbab8r040\TempState")

        self.file_path = os.path.join(app_data, "迫击炮测距-Plus", "xbox_elements.json")
        print(self.file_path)
        self._labels = {}
        self._is_running = False
        self._update_flag = False
        self.stop_flag = False
        self.thread = None

    # ─────────────────────────────────────────────
    # 服务器生命周期（保持原接口兼容）
    # ─────────────────────────────────────────────
    def start(self):
        """启动绘图服务。
        在本地文件模式下，仅执行目录创建和状态标记，不启动 HTTP 服务。
        如果服务已在运行则直接返回。
        """
        if self._is_running:
            return
        # 确保目标文件夹存在
        os.makedirs(os.path.dirname(self.file_path), exist_ok=True)
        self._is_running = True
        if not self.thread or not self.thread.is_alive():
            self.thread = threading.Thread(
                target=self._save_to_file_process,
                daemon=True
            )
            self.thread.start()

        print(f"XboxDrawServer started (File Mode): {self.file_path}")

    def stop(self):
        """停止绘图服务并清除所有标签。
        会将 JSON 文件内容清空。
        """
        if not self._is_running:
            return

        self.clear()
        self.stop_flag = True
        self.stop_event.wait()
        self.stop_event.clear()
        self._is_running = False

    def update(self):
        self._update_flag = True

    # ─────────────────────────────────────────────
    # 标签 CRUD（内部操作）
    # ─────────────────────────────────────────────
    def _save_to_file(self):
        """将当前标签列表原子地写入 JSON 文件（先写临时文件，再替换）。"""
        return
        self.process_queue.put(copy.deepcopy(list(self._labels.values())))

    def _save_to_file_process(self):
        last_time = time.time()
        last_values = None
        while True:
            try:
                if not self._is_running and not self.stop_flag and not self._update_flag:
                    time.sleep(3)
                    continue
                self._update_flag = False
                # values = self.process_queue.get()
                values = list(self._labels.values())

                now = time.time()
                if values == last_values and now - last_time < 2:
                    continue

                # 获取目标文件所在目录
                dir_path = os.path.dirname(self.file_path)
                os.makedirs(dir_path, exist_ok=True)  # 确保目录存在

                # 创建临时文件（在同一目录下，保证 rename 操作是原子的）
                with tempfile.NamedTemporaryFile(
                        mode='w',
                        encoding='utf8',
                        dir=dir_path,
                        delete=False,
                        suffix='.tmp'
                ) as tmp_file:
                    json.dump({"labels": values}, tmp_file, ensure_ascii=False)
                    tmp_file.flush()
                    tmp_file_path = tmp_file.name
                # 原子替换：Windows 上 os.replace 是原子的
                os.replace(tmp_file_path, self.file_path)
                last_time = now
                last_values = copy.deepcopy(values)
                if self.stop_flag:
                    break

            except Exception as e:
                print(f"写入文件失败: {e}")
                # 清理可能残留的临时文件
                try:
                    if 'tmp_file_path' in locals() and os.path.exists(tmp_file_path):
                        os.unlink(tmp_file_path)
                except:
                    pass
            finally:
                if not self.stop_flag:
                    time.sleep(0.01)

        self.thread = None
        self.stop_flag = False
        self.stop_event.set()

    def use_offset(self, label: dict):
        xbox_offset_x = self.config_app.configget("xbox_offset_x")
        xbox_offset_y = self.config_app.configget("xbox_offset_y")
        x1, y1, x2, y2 = label["box"]
        label['box'] = [
            x1 + xbox_offset_x,
            y1 + xbox_offset_y,
            x2 + xbox_offset_x,
            y2 + xbox_offset_y
        ]
        return label

    def set_label(self, label: dict):
        """添加或更新一个绘图标签，并立即写入文件。
        Args:
            label (dict): 标签数据字典，必须包含 "id" 字段。
        """
        label_id = str(label["id"])
        label = self.use_offset(label.copy())
        self._labels[label_id] = label
        self._save_to_file()

    def set_many_label(self, labels: list):
        """批量添加或更新多个绘图标签，并立即写入文件。
        Args:
            labels (list[dict]): 标签数据字典列表。
        """
        for label in labels:
            label_id = str(label["id"])
            new_label = self.use_offset(label.copy())
            self._labels[label_id] = new_label
        self._save_to_file()

    def remove_label(self, label_id: str):
        """根据 ID 移除一个绘图标签，并立即更新文件。"""
        if not self._is_running:
            return
        if self._labels.pop(str(label_id), None) is not None:
            self._save_to_file()

    def remove_labels(self, label_ids: list):
        """根据 ID 列表移除多个绘图标签，并立即更新文件。"""
        if not self._is_running:
            return
        changed = False
        for label_id in label_ids:
            if self._labels.pop(str(label_id), None) is not None:
                changed = True
        if changed:
            self._save_to_file()

    def clear(self):
        """清除所有绘图标签，并立即更新文件。"""
        if not self._is_running:
            return
        if self._labels:
            self._labels.clear()
            self._save_to_file()

    # ─────────────────────────────────────────────
    # 配置查询
    # ─────────────────────────────────────────────
    def use_dxcam(self, config_app):
        """查询配置，判断是否使用 dxcam 截图方式。"""
        return config_app.configget("use_dxcam")

    def xbox_show_accept(self, config_app):
        """查询配置，判断是否启用 Xbox 屏幕服务端绘图。"""
        return config_app.configget("xbox_screen_server")

    def qt_show_accept(self, config_app):
        """查询配置，判断是否允许在本地 Qt 界面上显示绘图。"""
        return not(config_app.configget("xbox_screen_server") and config_app.configget("xbox_only_show"))

    # ─────────────────────────────────────────────
    # 通用绘图方法
    # ─────────────────────────────────────────────
    def show_a_label(
        self,
        label_id,
        box,
        text=None,
        background="#FFFFFF",
        foreground="#000000",
        alpha=180,
        font_size=24,
        text_background=None,
        lines=None,
        box_line=None,
        lazy_draw=False,
    ):
        """构建并发送（或仅构建）一个绘图标签。
        Args:
            label_id: 标签唯一标识符。
            box: 绘制区域，格式 (x1, y1, x2, y2)。
            text: 要显示的文字内容。
            background: 背景色。
            foreground: 前景色（文字颜色）。
            alpha: 背景透明度，0-255。
            font_size: 字体大小（像素）。
            text_background: 文字背景色。
            lines: 线条列表。
            box_line: 边框线样式。
            lazy_draw: 如果为 True，只构建标签字典并返回，不实际发送。
        Returns:
            lazy_draw=True 时返回标签字典，否则返回 None。
        """
        if not self._is_running:
            return

        x1, y1, x2, y2 = box
        label = {
            "id": str(label_id),
            "box": [int(x1), int(y1), int(x2), int(y2)],
            "background": background,
            "foreground": foreground,
            "alpha": int(alpha),
            "font_size": int(font_size),
        }
        if text is not None:
            label["text"] = str(text)
        if text_background is not None:
            label["text_background"] = text_background
        if lines:
            label["line"] = lines
        if box_line:
            label["box_line"] = box_line

        if lazy_draw:
            return label

        self.set_label(label)
        return None

    def show_text(
        self,
        label_id,
        box,
        text,
        background="#FFFFFF",
        foreground="#000000",
        alpha=180,
        font_size=24,
        text_background=None,
    ):
        """在指定区域显示一段文字（带可选的背景色块）。"""
        self.show_a_label(
            label_id=label_id,
            box=box,
            text=text,
            background=background,
            foreground=foreground,
            alpha=alpha,
            font_size=font_size,
            text_background=text_background,
        )

    def show_box(
        self,
        label_id,
        box,
        color="#FFFF00",
        width=2,
        background="#000000",
        alpha=0,
        text=None,
        font_size=24,
        text_background=None,
    ):
        """在指定区域绘制一个矩形边框（可附带文字）。"""
        self.show_a_label(
            label_id=label_id,
            box=box,
            text=text,
            background=background,
            foreground=color,
            alpha=alpha,
            font_size=font_size,
            text_background=text_background,
            box_line={
                "width": width,
                "offset": 0,
                "color": color,
            },
        )

    def show_crosshair(
        self,
        label_id,
        center_xy,
        size=120,
        color="#FF0000",
        width=2,
    ):
        """在指定坐标绘制一个十字准星。"""
        cx, cy = center_xy
        half = size // 2
        x1 = cx - half
        y1 = cy - half
        x2 = cx + half
        y2 = cy + half

        self.show_a_label(
            label_id=label_id,
            box=(x1, y1, x2, y2),
            background="#000000",
            alpha=0,
            lines=[
                # 垂直线：从顶部中心到底部中心
                {"width": width, "color": color, "points": [[half, 0], [half, size]]},
                # 水平线：从左侧中心到右侧中心
                {"width": width, "color": color, "points": [[0, half], [size, half]]},
            ],
            box_line={"width": 1, "offset": 0, "color": color},
        )


# 模块级全局单例，方便直接 import 后使用
xbox_draw_server = XboxDrawServer()
