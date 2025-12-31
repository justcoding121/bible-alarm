#nullable enable
using System.Text.Json;

namespace Bible.Alarm.Views.Shared;

/// <summary>
/// Simple Lottie animation data model for parsing the loading_spinner.json file.
/// </summary>
internal class LottieAnimationData
{
    public int FrameRate { get; set; }
    public int InPoint { get; set; }
    public int OutPoint { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public LottieLayer[] Layers { get; set; } = [];

    public static LottieAnimationData? Parse(Stream jsonStream)
    {
        using var reader = new StreamReader(jsonStream);
        var json = reader.ReadToEnd();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var data = new LottieAnimationData
        {
            FrameRate = root.TryGetProperty("fr", out var fr) ? fr.GetInt32() : 60,
            InPoint = root.TryGetProperty("ip", out var ip) ? ip.GetInt32() : 0,
            OutPoint = root.TryGetProperty("op", out var op) ? op.GetInt32() : 60,
            Width = root.TryGetProperty("w", out var w) ? w.GetInt32() : 60,
            Height = root.TryGetProperty("h", out var h) ? h.GetInt32() : 60
        };

        if (root.TryGetProperty("layers", out var layers) && layers.ValueKind == JsonValueKind.Array)
        {
            var layerList = new List<LottieLayer>();
            foreach (var layer in layers.EnumerateArray())
            {
                var lottieLayer = ParseLayer(layer);
                if (lottieLayer != null)
                {
                    layerList.Add(lottieLayer);
                }
            }
            data.Layers = layerList.ToArray();
        }

        return data;
    }

    private static LottieLayer? ParseLayer(JsonElement layerElement)
    {
        if (!layerElement.TryGetProperty("ks", out var ks))
            return null;

        var layer = new LottieLayer();

        // Parse rotation keyframes
        if (ks.TryGetProperty("r", out var r) && r.TryGetProperty("k", out var rk))
        {
            if (rk.ValueKind == JsonValueKind.Array)
            {
                var keyframes = new List<LottieKeyframe>();
                foreach (var kf in rk.EnumerateArray())
                {
                    if (kf.TryGetProperty("t", out var t) && kf.TryGetProperty("s", out var s))
                    {
                        var frame = t.GetSingle();
                        var value = s[0].GetSingle();
                        keyframes.Add(new LottieKeyframe { Frame = frame, Value = value });
                    }
                }
                layer.RotationKeyframes = keyframes.ToArray();
            }
        }

        // Parse position
        if (ks.TryGetProperty("p", out var p) && p.TryGetProperty("k", out var pk))
        {
            if (pk.ValueKind == JsonValueKind.Array && pk.GetArrayLength() >= 2)
            {
                layer.PositionX = pk[0].GetSingle();
                layer.PositionY = pk[1].GetSingle();
            }
        }

        // Parse shapes to get circle properties
        if (layerElement.TryGetProperty("shapes", out var shapes) && shapes.ValueKind == JsonValueKind.Array)
        {
            foreach (var shape in shapes.EnumerateArray())
            {
                if (shape.TryGetProperty("ty", out var ty) && ty.GetString() == "gr")
                {
                    // Group shape - contains the circle
                    if (shape.TryGetProperty("it", out var it) && it.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in it.EnumerateArray())
                        {
                            if (item.TryGetProperty("ty", out var itemTy))
                            {
                                var itemType = itemTy.GetString();
                                if (itemType == "el")
                                {
                                    // Ellipse
                                    if (item.TryGetProperty("s", out var s) && s.TryGetProperty("k", out var sk))
                                    {
                                        if (sk.ValueKind == JsonValueKind.Array && sk.GetArrayLength() >= 2)
                                        {
                                            layer.CircleWidth = sk[0].GetSingle();
                                            layer.CircleHeight = sk[1].GetSingle();
                                        }
                                    }
                                }
                                else if (itemType == "st")
                                {
                                    // Stroke
                                    if (item.TryGetProperty("c", out var c) && c.TryGetProperty("k", out var ck))
                                    {
                                        if (ck.ValueKind == JsonValueKind.Array && ck.GetArrayLength() >= 4)
                                        {
                                            layer.StrokeColor = new LottieColor
                                            {
                                                R = ck[0].GetSingle(),
                                                G = ck[1].GetSingle(),
                                                B = ck[2].GetSingle(),
                                                A = ck[3].GetSingle()
                                            };
                                        }
                                    }
                                    if (item.TryGetProperty("w", out var w) && w.TryGetProperty("k", out var wk))
                                    {
                                        layer.StrokeWidth = wk.GetSingle();
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return layer;
    }
}

internal class LottieLayer
{
    public LottieKeyframe[] RotationKeyframes { get; set; } = [];
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float CircleWidth { get; set; }
    public float CircleHeight { get; set; }
    public LottieColor? StrokeColor { get; set; }
    public float StrokeWidth { get; set; }
}

internal class LottieKeyframe
{
    public float Frame { get; set; }
    public float Value { get; set; }
}

internal class LottieColor
{
    public float R { get; set; }
    public float G { get; set; }
    public float B { get; set; }
    public float A { get; set; }
}

