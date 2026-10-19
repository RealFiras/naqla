using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Naqla.Core;

namespace Naqla.App;

/// <summary>Transient contextual controls; uses the shared native pixel buttons.</summary>
internal sealed class ProcessingSettingsWindow : Window
{
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Silver, Margin = new Thickness(0, 12, 0, 8) };
    public ImageToolOptions ImageOptions { get; private set; } = new();
    public MediaToolOptions MediaOptions { get; private set; } = new();
    public int Dpi { get; private set; } = 144;
    public ProcessingSettingsWindow(DraggedFile file, string operation, int count)
    {
        Title = operation == "ROTATE" ? "Naqla · تدوير" : operation is "PNG" or "JPG" ? "Naqla · دقة PDF" : "Naqla · ضغط";
        Width = 410; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Paint("#121820"); Foreground = Paint("#F5F0E6"); FontFamily = new("Segoe UI"); FontSize = 13;
        PixelControls.Apply(Resources);
        var root = new StackPanel { Margin = new Thickness(20), FlowDirection = FlowDirection.RightToLeft }; Content = root;
        root.Children.Add(new TextBlock { Text = Title.Replace("Naqla · ", ""), FontSize = 21, Foreground = Paint("#FFC35A"), Margin = new Thickness(0, 0, 0, 12) });
        root.Children.Add(new TextBlock { Text = $"{file.Extension} · {file.DisplaySize}" + (count > 1 ? $" · {count} ملفات" : ""), Foreground = Brushes.Silver, Margin = new Thickness(0, 0, 0, 12) });
        var quality = new Slider { Minimum = 1, Maximum = 100, Value = 78, TickFrequency = 1, IsSnapToTickEnabled = true, Margin = new Thickness(0, 12, 0, 5), Foreground = Paint("#FFC35A") };
        var qualityText = new TextBlock { Text = "جودة 78 / 100" }; quality.ValueChanged += (_, _) => qualityText.Text = $"جودة {quality.Value:0} / 100";
        var choice = new ComboBox { Margin = new Thickness(0, 6, 0, 6), Padding = new Thickness(7), Background = Paint("#1C2530"), Foreground = Paint("#F5F0E6") };
        if (operation == "ROTATE") { root.Children.Add(new TextBlock { Text = "الزاوية" }); choice.ItemsSource = new[] { "90° مع الساعة", "90° عكس الساعة", "180°" }; choice.SelectedIndex = 0; root.Children.Add(choice); }
        else if (file.Extension == "PDF") { root.Children.Add(new TextBlock { Text = "الدقة · DPI" }); choice.ItemsSource = new[] { "72", "144", "200", "300" }; choice.SelectedIndex = 1; root.Children.Add(choice); }
        else if (file.Category == FileCategory.Image)
        {
            root.Children.Add(qualityText); root.Children.Add(quality);
            root.Children.Add(new TextBlock { Text = "الأبعاد محفوظة. في PNG وTIFF يكون الضغط دون فقد؛ الجودة تؤثر في الصيغ ذات الفقد فقط.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Silver, Margin = new Thickness(0, 8, 0, 0) });
        }
        else { root.Children.Add(new TextBlock { Text = "مستوى الضغط" }); choice.ItemsSource = new[] { "جودة أعلى", "متوازن", "حجم أصغر" }; choice.SelectedIndex = 1; root.Children.Add(choice); }
        root.Children.Add(status); status.Text = "يظهر الحجم الفعلي بعد الترميز والتحقق. إذا لم يصغر الملف فلن يُعرض نجاح أو تُحفظ نسخة مضللة.";
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left }; root.Children.Add(buttons);
        var cancel = new PixelButton { Content = "إلغاء", IsCancel = true, Tone = PixelButtonTone.Coral, Glyph = PixelButtonGlyph.Cancel, Margin = new Thickness(0, 0, 8, 0) };
        var confirm = new PixelButton { Content = "إنشاء نسخة", IsDefault = true, Tone = PixelButtonTone.Amber, Glyph = PixelButtonGlyph.Check };
        buttons.Children.Add(confirm); buttons.Children.Add(cancel);
        confirm.Click += (_, _) =>
        {
            var angle = choice.SelectedIndex switch { 1 => -90, 2 => 180, _ => 90 };
            ImageOptions = new((int)quality.Value, operation == "ROTATE" ? angle : 90);
            MediaOptions = new((MediaQuality)Math.Clamp(choice.SelectedIndex, 0, 2), operation == "ROTATE" ? angle : 90);
            if (file.Extension == "PDF") Dpi = int.Parse(choice.SelectedItem!.ToString()!, CultureInfo.InvariantCulture);
            DialogResult = true;
        };
    }
    private static SolidColorBrush Paint(string text) => new((Color)ColorConverter.ConvertFromString(text));
}
