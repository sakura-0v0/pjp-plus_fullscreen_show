using System;
using System.Collections.Generic;
using System.Globalization;
using Windows.Data.Json;
using Windows.UI;

namespace TacticalRadarFinal
{
    internal static class DrawProtocol
    {
        public static DrawLabelRequest ParseDrawLabel(string json)
        {
            var root = JsonObject.Parse(json);
            return ParseDrawLabelObject(root);
        }

        public static DrawFrameResponse ParseFrameResponse(string json)
        {
            var root = JsonObject.Parse(json);
            var response = new DrawFrameResponse();

            if (!root.TryGetValue("labels", out IJsonValue labelsValue) || labelsValue.ValueType != JsonValueType.Array)
            {
                return response;
            }

            var labels = new List<DrawLabelRequest>();
            foreach (var item in labelsValue.GetArray())
            {
                if (item.ValueType != JsonValueType.Object)
                {
                    continue;
                }

                labels.Add(ParseDrawLabelObject(item.GetObject()));
            }

            response.Labels = labels;
            return response;
        }

        public static RemoveLabelRequest ParseRemoveLabel(string json)
        {
            var root = JsonObject.Parse(json);
            return new RemoveLabelRequest { Id = ReadRequiredString(root, "id") };
        }

        private static DrawLabelRequest ParseDrawLabelObject(JsonObject root)
        {
            var request = new DrawLabelRequest
            {
                Id = ReadRequiredString(root, "id"),
                FontSize = ReadNumber(root, "font_size", 18),
                Text = ReadOptionalString(root, "text"),
                Background = ParseColor(root, "background", Colors.Transparent),
                Foreground = ParseColor(root, "foreground", Colors.White),
                TextBackground = TryParseColor(root, "text_background")
            };

            ApplyBox(root, request);
            ApplyAlpha(root, request);
            request.BoxLine = ParseBoxLine(root);
            request.Lines = ParseLines(root);

            return request;
        }

        private static void ApplyBox(JsonObject root, DrawLabelRequest request)
        {
            if (!root.TryGetValue("box", out IJsonValue boxValue) || boxValue.ValueType != JsonValueType.Array)
            {
                throw new InvalidOperationException("Missing required array: box");
            }

            var box = boxValue.GetArray();
            if (box.Count != 4)
            {
                throw new InvalidOperationException("box must contain [x1, y1, x2, y2]");
            }

            var x1 = box[0].GetNumber();
            var y1 = box[1].GetNumber();
            var x2 = box[2].GetNumber();
            var y2 = box[3].GetNumber();
            request.X = Math.Min(x1, x2);
            request.Y = Math.Min(y1, y2);
            request.Width = Math.Max(1, Math.Abs(x2 - x1));
            request.Height = Math.Max(1, Math.Abs(y2 - y1));
        }

        private static void ApplyAlpha(JsonObject root, DrawLabelRequest request)
        {
            if (!root.TryGetValue("alpha", out IJsonValue alphaValue))
            {
                return;
            }

            byte alpha = Convert.ToByte(Math.Max(0, Math.Min(255, ReadByteLike(alphaValue))));
            request.Background = Color.FromArgb(alpha, request.Background.R, request.Background.G, request.Background.B);

            if (request.TextBackground.HasValue)
            {
                var textBackground = request.TextBackground.Value;
                request.TextBackground = Color.FromArgb(alpha, textBackground.R, textBackground.G, textBackground.B);
            }
        }

        private static BoxLineDefinition ParseBoxLine(JsonObject root)
        {
            if (!root.TryGetValue("box_line", out IJsonValue value) || value.ValueType != JsonValueType.Object)
            {
                return null;
            }

            var obj = value.GetObject();
            return new BoxLineDefinition
            {
                Width = ReadNumber(obj, "width", 2),
                Offset = ReadNumber(obj, "offset", 0),
                Color = ParseColor(obj, "color", Colors.Red)
            };
        }

        private static IReadOnlyList<DrawLineDefinition> ParseLines(JsonObject root)
        {
            if (!root.TryGetValue("line", out IJsonValue lineValue) || lineValue.ValueType != JsonValueType.Array)
            {
                return Array.Empty<DrawLineDefinition>();
            }

            var result = new List<DrawLineDefinition>();
            foreach (var item in lineValue.GetArray())
            {
                if (item.ValueType != JsonValueType.Object)
                {
                    continue;
                }

                var obj = item.GetObject();
                JsonArray rawPoints = null;
                if (obj.TryGetValue("points", out IJsonValue pointsValue) && pointsValue.ValueType == JsonValueType.Array)
                {
                    rawPoints = pointsValue.GetArray();
                }
                else if (obj.TryGetValue("x_y", out IJsonValue xyValue) && xyValue.ValueType == JsonValueType.Array)
                {
                    rawPoints = xyValue.GetArray();
                }

                if (rawPoints == null || rawPoints.Count < 2)
                {
                    continue;
                }

                var points = new List<PointDefinition>();
                foreach (var rawPoint in rawPoints)
                {
                    if (rawPoint.ValueType != JsonValueType.Array)
                    {
                        continue;
                    }

                    var pair = rawPoint.GetArray();
                    if (pair.Count < 2)
                    {
                        continue;
                    }

                    points.Add(new PointDefinition
                    {
                        X = pair[0].GetNumber(),
                        Y = pair[1].GetNumber()
                    });
                }

                if (points.Count < 2)
                {
                    continue;
                }

                result.Add(new DrawLineDefinition
                {
                    Width = ReadNumber(obj, "width", 2),
                    Color = ParseColor(obj, "color", Colors.Red),
                    Points = points
                });
            }

            return result;
        }

        private static string ReadRequiredString(JsonObject root, string key)
        {
            var value = ReadOptionalString(root, key);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Missing required string: {key}");
            }

            return value;
        }

        private static string ReadOptionalString(JsonObject root, string key)
        {
            if (!root.TryGetValue(key, out IJsonValue value))
            {
                return null;
            }

            if (value.ValueType == JsonValueType.String)
            {
                return value.GetString();
            }

            return value.Stringify().Trim('"');
        }

        private static double ReadNumber(JsonObject root, string key, double fallback)
        {
            if (!root.TryGetValue(key, out IJsonValue value))
            {
                return fallback;
            }

            return value.ValueType == JsonValueType.Number
                ? value.GetNumber()
                : double.Parse(value.Stringify().Trim('"'), CultureInfo.InvariantCulture);
        }

        private static double ReadByteLike(IJsonValue value)
        {
            return value.ValueType == JsonValueType.Number
                ? value.GetNumber()
                : double.Parse(value.Stringify().Trim('"'), CultureInfo.InvariantCulture);
        }

        private static Color ParseColor(JsonObject root, string key, Color fallback)
        {
            var parsed = TryParseColor(root, key);
            return parsed ?? fallback;
        }

        private static Color? TryParseColor(JsonObject root, string key)
        {
            var raw = ReadOptionalString(root, key);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            return ParseColorString(raw);
        }

        private static Color ParseColorString(string raw)
        {
            var text = raw.Trim();
            if (text.StartsWith("#", StringComparison.Ordinal))
            {
                text = text.Substring(1);
            }

            if (text.Length == 6)
            {
                return Color.FromArgb(
                    255,
                    byte.Parse(text.Substring(0, 2), NumberStyles.HexNumber),
                    byte.Parse(text.Substring(2, 2), NumberStyles.HexNumber),
                    byte.Parse(text.Substring(4, 2), NumberStyles.HexNumber));
            }

            if (text.Length == 8)
            {
                return Color.FromArgb(
                    byte.Parse(text.Substring(0, 2), NumberStyles.HexNumber),
                    byte.Parse(text.Substring(2, 2), NumberStyles.HexNumber),
                    byte.Parse(text.Substring(4, 2), NumberStyles.HexNumber),
                    byte.Parse(text.Substring(6, 2), NumberStyles.HexNumber));
            }

            throw new InvalidOperationException($"Unsupported color format: {raw}");
        }
    }
}
