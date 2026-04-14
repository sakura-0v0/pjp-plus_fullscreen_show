using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using Microsoft.Gaming.XboxGameBar;

namespace TacticalRadarFinal
{
    public sealed partial class CanvasPage : Page
    {
        private const double TopInteractionMargin = 72;
        private readonly Dictionary<string, FrameworkElement> labels = new Dictionary<string, FrameworkElement>();
        private XboxGameBarWidget widget;
        private bool isPolling;
        private bool isUnloaded;

        private int _errCount = 0;
        private int _frameCount = 0;
        private string _lastError = "None";

        private string js_text;
        private DrawFrameResponse _lastFrame;   // 缓存最近一帧数据，用于窗口大小改变时刷新

        private StorageFolder jsonFolder;
        private string jsonFileName = "xbox_elements.json";

        private double _dpiScale = 1.0;   // 屏幕缩放因子 (RawPixelsPerViewPixel)

        public CanvasPage()
        {
            InitializeComponent();
            Loaded += CanvasPage_Loaded;
            Unloaded += CanvasPage_Unloaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            widget = e.Parameter as XboxGameBarWidget;
        }

        private async void CanvasPage_Loaded(object sender, RoutedEventArgs e)
        {
            isUnloaded = false;
            try
            {
                var display = DisplayInformation.GetForCurrentView();
                _dpiScale = display.RawPixelsPerViewPixel;
                if (widget != null)
                {
                    widget.GameBarDisplayModeChanged += WidgetStateChanged;
                    widget.PinnedChanged += WidgetStateChanged;
                    widget.ClickThroughEnabledChanged += WidgetStateChanged;

                    //// 1. 设置窗口大小（物理像素，铺满宽度，高度最小400）
                    //var bounds = GetCanvasWindowSize();
                    //await widget.TryResizeWindowAsync(bounds);

                    // 2. 窗口居中
                    await widget.CenterWindowAsync();
                }

                // 订阅窗口大小改变事件，用于强制刷新标签位置
                Window.Current.SizeChanged += OnWindowSizeChanged;

                // 使用临时文件夹
                jsonFolder = ApplicationData.Current.TemporaryFolder;
                jsonFolder = await jsonFolder.CreateFolderAsync("迫击炮测距-Plus", CreationCollisionOption.OpenIfExists);

                UpdateControlPanel();
                Log($"临时文件夹已就绪: {jsonFolder.Path}");
                StartPollingLoop();
            }
            catch (Exception ex)
            {
                Log($"FATAL INIT: {ex.Message}");
            }
        }

        private void CanvasPage_Unloaded(object sender, RoutedEventArgs e)
        {
            isUnloaded = true;
            if (widget != null)
            {
                widget.GameBarDisplayModeChanged -= WidgetStateChanged;
                widget.PinnedChanged -= WidgetStateChanged;
                widget.ClickThroughEnabledChanged -= WidgetStateChanged;
            }
            Window.Current.SizeChanged -= OnWindowSizeChanged;
        }

        private async void StartPollingLoop()
        {
            if (isPolling) return;
            isPolling = true;

            while (!isUnloaded && jsonFolder == null)
                await Task.Delay(100);

            while (!isUnloaded)
            {
                try
                {
                    StorageFile jsonFile = await jsonFolder.GetFileAsync(jsonFileName);
                    string json = await FileIO.ReadTextAsync(jsonFile);
                    if (json == js_text) continue;
                    js_text = json;

                    if (string.IsNullOrWhiteSpace(json)) continue;

                    var frame = DrawProtocol.ParseFrameResponse(json);
                    _lastFrame = frame;   // 缓存帧数据
                    _frameCount++;

                    if (_frameCount % 50 == 0)
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, UpdateControlPanel);
                    }

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        ReplaceAllLabels(frame);
                    });
                }
                catch (FileNotFoundException)
                {
                    await Task.Delay(500);
                }
                catch (Exception ex) when (ex.HResult == -2147024864)
                {
                    await Task.Delay(20);
                }
                catch (Exception ex)
                {
                    _errCount++;
                    _lastError = $"{ex.GetType().Name}: {ex.Message} (HResult: 0x{ex.HResult:X8})";

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Log($"错误 #{_errCount}: \n{_lastError}\n{js_text}");
                    });

                    await Task.Delay(200);
                }
                finally
                {
                    await Task.Delay(16);
                }
            }
            isPolling = false;
        }

        private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            // 窗口大小改变时，如果有缓存的帧数据，则重新绘制（基于新的窗口尺寸）
            if (_lastFrame != null)
            {
                _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    ReplaceAllLabels(_lastFrame);
                    // 可选：更新状态栏提示（不覆盖重要信息）
                    StatusText.Text = "窗口大小已改变，已刷新标签。如位置偏移，请点击居中按钮。";
                });
            }
        }

        private void ReplaceAllLabels(DrawFrameResponse frame)
        {
            RootCanvas.Children.Clear();
            labels.Clear();
            foreach (var label in frame.Labels)
            {
                UpsertLabel(label);
            }
        }

        private void UpsertLabel(DrawLabelRequest request)
        {
            try
            {
                RemoveLabel(request.Id);

                // 获取当前窗口有效尺寸（UWP 逻辑像素）
                double windowEffW = Window.Current.Bounds.Width;
                double windowEffH = Window.Current.Bounds.Height;

                // 获取屏幕物理尺寸和缩放比例
                var display = DisplayInformation.GetForCurrentView();
                double screenPhysW = display.ScreenWidthInRawPixels;
                double screenPhysH = display.ScreenHeightInRawPixels;
                double scale = _dpiScale;

                // 绝对物理坐标（来自外部 JSON）
                double absX = request.X;
                double absY = request.Y;

                // 转换为以窗口中心为锚点的有效坐标
                // 公式：canvasCoord = windowCenterEff + (absPhys - screenCenterPhys) / scale
                double canvasX = windowEffW / 2 + (absX - screenPhysW / 2) / scale;
                double canvasY = windowEffH / 2 + (absY - screenPhysH / 2) / scale;

                // 尺寸和偏移量从物理像素转换为有效像素
                double invScale = 1.0 / scale;
                double width = request.Width * invScale;
                double height = request.Height * invScale;
                double padding = (request.BoxLine?.Offset ?? 0) * invScale;
                double boxOffset = (request.BoxLine?.Offset ?? 0) * invScale;

                var labelRoot = new Canvas
                {
                    Width = width + padding * 2,
                    Height = height + padding * 2,
                    IsHitTestVisible = false
                };

                // 背景矩形
                var background = new Rectangle
                {
                    Width = width,
                    Height = height,
                    Fill = new SolidColorBrush(request.Background),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(background, padding);
                Canvas.SetTop(background, padding);
                labelRoot.Children.Add(background);

                // 外框线
                if (request.BoxLine != null)
                {
                    var box = new Rectangle
                    {
                        Width = width + boxOffset * 2,
                        Height = height + boxOffset * 2,
                        Stroke = new SolidColorBrush(request.BoxLine.Color),
                        StrokeThickness = request.BoxLine.Width,
                        Fill = new SolidColorBrush(Colors.Transparent),
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(box, padding - boxOffset);
                    Canvas.SetTop(box, padding - boxOffset);
                    labelRoot.Children.Add(box);
                }

                // 绘制线条
                foreach (var line in request.Lines)
                {
                    for (int i = 1; i < line.Points.Count; i++)
                    {
                        var startPhys = line.Points[i - 1];
                        var endPhys = line.Points[i];
                        var segment = new Line
                        {
                            X1 = padding + startPhys.X * invScale,
                            Y1 = padding + startPhys.Y * invScale,
                            X2 = padding + endPhys.X * invScale,
                            Y2 = padding + endPhys.Y * invScale,
                            StrokeThickness = line.Width,
                            Stroke = new SolidColorBrush(line.Color),
                            StrokeStartLineCap = PenLineCap.Round,
                            StrokeEndLineCap = PenLineCap.Round,
                            IsHitTestVisible = false
                        };
                        labelRoot.Children.Add(segment);
                    }
                }

                // 绘制文本
                if (!string.IsNullOrWhiteSpace(request.Text))
                {
                    FrameworkElement textElement;
                    double fontSize = request.FontSize * invScale;
                    if (request.TextBackground.HasValue)
                    {
                        var border = new Border
                        {
                            Background = new SolidColorBrush(request.TextBackground.Value),
                            Padding = new Thickness(6, 2, 6, 2),
                            Child = new TextBlock
                            {
                                Text = request.Text,
                                Foreground = new SolidColorBrush(request.Foreground),
                                FontSize = fontSize,
                                FontWeight = FontWeights.Bold,
                                TextWrapping = TextWrapping.NoWrap,
                                IsHitTestVisible = false
                            }
                        };
                        textElement = border;
                    }
                    else
                    {
                        textElement = new TextBlock
                        {
                            Text = request.Text,
                            Foreground = new SolidColorBrush(request.Foreground),
                            FontSize = fontSize,
                            FontWeight = FontWeights.Bold,
                            TextWrapping = TextWrapping.NoWrap,
                            IsHitTestVisible = false
                        };
                    }

                    textElement.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    var size = textElement.DesiredSize;
                    Canvas.SetLeft(textElement, padding + Math.Max(0, (width - size.Width) / 2));
                    Canvas.SetTop(textElement, padding + Math.Max(0, (height - size.Height) / 2));
                    labelRoot.Children.Add(textElement);
                }

                // 设置 labelRoot 的位置（canvasX/canvasY 是内容区左上角，需减去 padding）
                Canvas.SetLeft(labelRoot, canvasX - padding);
                Canvas.SetTop(labelRoot, canvasY - padding);
                RootCanvas.Children.Add(labelRoot);
                labels[request.Id] = labelRoot;
            }
            catch (Exception ex)
            {
                Log($"Draw Err ID:{request.Id} {ex.Message}");
            }
        }

        private void RemoveLabel(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            if (labels.TryGetValue(id, out FrameworkElement element))
            {
                RootCanvas.Children.Remove(element);
                labels.Remove(id);
            }
        }

        private void ClearLabels()
        {
            RootCanvas.Children.Clear();
            labels.Clear();
            UpdateControlPanel();
        }

        private async void CenterButton_Click(object sender, RoutedEventArgs e)
        {
            if (widget == null) return;
            try
            {
                // 仅居中窗口，不改变大小
                await widget.CenterWindowAsync();
                Log("窗口已居中");
            }
            catch (Exception ex)
            {
                Log($"居中失败: {ex.Message}");
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearLabels();
        }

        private void UpdateControlPanel()
        {
            if (widget != null)
            {
                var isClickThroughPinned = widget.GameBarDisplayMode == XboxGameBarDisplayMode.PinnedOnly && widget.Pinned && widget.ClickThroughEnabled;
                var showPanel = widget.GameBarDisplayMode == XboxGameBarDisplayMode.Foreground && !isClickThroughPinned;
                ControlPanel.Visibility = showPanel ? Visibility.Visible : Visibility.Collapsed;
                if (isClickThroughPinned)
                {
                    StatusText.Text = $"Click-through Labels={labels.Count} Err={_errCount} LastError={_lastError}";
                    return;
                }
            }
            string pathDisplay = (jsonFolder != null) ? jsonFolder.Path : "未初始化";
            StatusText.Text = $"Polling Labels={labels.Count} Err={_errCount} LastErr={_lastError} Scale={_dpiScale:F2} \nPath=\"{pathDisplay}\" \ntext:{js_text?.Substring(0, Math.Min(50, js_text?.Length ?? 0))}";
        }

        private void Log(string message)
        {
            StatusText.Text = message;
        }

        private async void WidgetStateChanged(XboxGameBarWidget sender, object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, UpdateControlPanel);
        }

        // 返回物理像素尺寸，用于 TryResizeWindowAsync（铺满宽度，高度最小400）
        private Size GetCanvasWindowSize()
        {
            var display = DisplayInformation.GetForCurrentView();
            double physicalWidth = display.ScreenWidthInRawPixels;
            double physicalHeight = Math.Max(400, (display.ScreenHeightInRawPixels - TopInteractionMargin) * 0.88);
            return new Size(physicalWidth, physicalHeight);
        }
    }
}