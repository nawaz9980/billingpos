using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace QuickPos.Services;

/// <summary>
/// Generates crisp, scannable Code 39 vector barcode visuals for WPF printing.
/// </summary>
public static class BarcodeGenerator
{
    private static readonly Dictionary<char, string> Code39Patterns = new()
    {
        ['0'] = "nnnwwnwnn",
        ['1'] = "wnnwnnnnw",
        ['2'] = "nnwwnnnnw",
        ['3'] = "wnwwnnnnn",
        ['4'] = "nnnwwnnnw",
        ['5'] = "wnnwwnnnn",
        ['6'] = "nnwwwnnnn",
        ['7'] = "nnnwnnwnw",
        ['8'] = "wnnwnnwnn",
        ['9'] = "nnwwnnwnn",
        ['A'] = "wnnnnwnnw",
        ['B'] = "nnwnnwnnw",
        ['C'] = "wnwnnwnnn",
        ['D'] = "nnnnwwnnw",
        ['E'] = "wnnnwwnnn",
        ['F'] = "nnwnwwnnn",
        ['G'] = "nnnnnwwnw",
        ['H'] = "wnnnnwwnn",
        ['I'] = "nnwnnwwnn",
        ['J'] = "nnnnwwwnn",
        ['K'] = "wnnnnnnww",
        ['L'] = "nnwnnnnww",
        ['M'] = "wnwnnnnwn",
        ['N'] = "nnnnwnnww",
        ['O'] = "wnnnwnnwn",
        ['P'] = "nnwnwnnwn",
        ['Q'] = "nnnnnnwww",
        ['R'] = "wnnnnnwwn",
        ['S'] = "nnwnnnwwn",
        ['T'] = "nnnnwnwwn",
        ['U'] = "wwnnnnnnw",
        ['V'] = "nwwnnnnnw",
        ['W'] = "wwwnnnnnn",
        ['X'] = "nwnnwnnnw",
        ['Y'] = "wwnnwnnnn",
        ['Z'] = "nwwnwnnnn",
        ['-'] = "nwnnnnwnw",
        ['.'] = "wwnnnnwnn",
        [' '] = "nwwnnnwnn",
        ['$'] = "nwnwnwnnn",
        ['/'] = "nwnwnnnwn",
        ['+'] = "nwnnnwnwn",
        ['%'] = "nnnwnwnwn",
        ['*'] = "nwnnwnwnn"
    };

    public static UIElement CreateBarcodeElement(string value, double height = 34, double narrowWidth = 1.0, double wideMultiplier = 2.4)
    {
        string clean = string.IsNullOrWhiteSpace(value) ? "000000" : value.Trim().ToUpperInvariant();
        string encoded = $"*{clean}*";

        double wideWidth = narrowWidth * wideMultiplier;
        double quietZone = narrowWidth * 8;
        double x = quietZone;

        var canvas = new Canvas
        {
            Height = height,
            Background = Brushes.White,
            SnapsToDevicePixels = true
        };

        for (int cIdx = 0; cIdx < encoded.Length; cIdx++)
        {
            char ch = encoded[cIdx];
            if (!Code39Patterns.TryGetValue(ch, out string? pattern))
                pattern = Code39Patterns['-'];

            for (int i = 0; i < pattern.Length; i++)
            {
                double w = pattern[i] == 'w' ? wideWidth : narrowWidth;
                bool isBar = (i % 2) == 0; // Even indices are black bars, odd are spaces

                if (isBar)
                {
                    var rect = new Rectangle
                    {
                        Width = w,
                        Height = height,
                        Fill = Brushes.Black
                    };
                    Canvas.SetLeft(rect, x);
                    Canvas.SetTop(rect, 0);
                    canvas.Children.Add(rect);
                }

                x += w;
            }

            // Inter-character narrow gap
            if (cIdx < encoded.Length - 1)
                x += narrowWidth;
        }

        canvas.Width = x + quietZone;

        var panel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 3, 0, 0)
        };
        panel.Children.Add(canvas);
        panel.Children.Add(new TextBlock
        {
            Text = clean,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.Black,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        });

        return panel;
    }
}
