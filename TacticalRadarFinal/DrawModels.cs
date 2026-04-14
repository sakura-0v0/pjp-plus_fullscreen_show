using System;
using System.Collections.Generic;
using Windows.UI;

namespace TacticalRadarFinal
{
    public sealed class DrawLineDefinition
    {
        public Color Color { get; set; } = Colors.Red;
        public double Width { get; set; } = 2;
        public IReadOnlyList<PointDefinition> Points { get; set; } = Array.Empty<PointDefinition>();
    }

    public sealed class PointDefinition
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public sealed class BoxLineDefinition
    {
        public Color Color { get; set; } = Colors.Red;
        public double Width { get; set; } = 2;
        public double Offset { get; set; }
    }

    public sealed class DrawLabelRequest
    {
        public string Id { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public Color Background { get; set; } = Colors.Transparent;
        public Color Foreground { get; set; } = Colors.White;
        public Color? TextBackground { get; set; }
        public string Text { get; set; }
        public double FontSize { get; set; } = 18;
        public IReadOnlyList<DrawLineDefinition> Lines { get; set; } = Array.Empty<DrawLineDefinition>();
        public BoxLineDefinition BoxLine { get; set; }
    }

    public sealed class RemoveLabelRequest
    {
        public string Id { get; set; } = string.Empty;
    }

    public sealed class DrawFrameResponse
    {
        public IReadOnlyList<DrawLabelRequest> Labels { get; set; } = Array.Empty<DrawLabelRequest>();
    }
}
