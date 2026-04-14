using System;
using System.Collections.Generic;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Microsoft.Gaming.XboxGameBar;

namespace TacticalRadarFinal
{
    sealed partial class App : Application
    {
        private readonly Dictionary<string, XboxGameBarWidget> activeWidgets = new Dictionary<string, XboxGameBarWidget>();

        public App()
        {
            InitializeComponent();
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            try
            {
                if (args.Kind == ActivationKind.Protocol)
                {
                    var protocolArgs = args as IProtocolActivatedEventArgs;
                    if (protocolArgs != null && protocolArgs.Uri.Scheme.Equals("ms-gamebarwidget"))
                    {
                        var widgetArgs = args as XboxGameBarWidgetActivatedEventArgs;
                        if (widgetArgs != null && widgetArgs.IsLaunchActivation)
                        {
                            var rootFrame = new Frame();
                            rootFrame.Background = new SolidColorBrush(Windows.UI.Colors.Transparent);

                            Window.Current.Content = rootFrame;

                            var widget = new XboxGameBarWidget(widgetArgs, Window.Current.CoreWindow, rootFrame);
                            activeWidgets[widgetArgs.AppExtensionId] = widget;

                            if (widgetArgs.AppExtensionId == "OverlayCanvasWidgetExt")
                            {
                                rootFrame.Navigate(typeof(CanvasPage), widget);
                            }

                            Window.Current.Activate();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var errorFrame = new Frame();
                errorFrame.Background = new SolidColorBrush(Windows.UI.Colors.DarkRed);
                errorFrame.Content = new TextBlock
                {
                    Text = ex.ToString(),
                    Foreground = new SolidColorBrush(Windows.UI.Colors.White),
                    TextWrapping = TextWrapping.Wrap
                };
                Window.Current.Content = errorFrame;
                Window.Current.Activate();
            }
        }
    }
}
