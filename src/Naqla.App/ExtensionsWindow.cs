using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Naqla.Conversion;
using Naqla.Core;

namespace Naqla.App;

internal sealed class ExtensionsWindow : ToolDialog
{
    private readonly ComponentStore store = new(ComponentStore.DefaultRoot);
    private readonly ListBox components = new() { Height = 150, DisplayMemberPath = nameof(ComponentLine.Label) };
    private readonly ListBox plugins = new() { Height = 115, DisplayMemberPath = nameof(PluginLine.Label) };
    private sealed record ComponentLine(ComponentStatus Value) { public string Label => $"{Value.Feature} · {(Value.Installed ? "مثبت" : "غير مثبت")} · {Value.Bytes / 1048576d:0.#} MB"; }
    private sealed record PluginLine(InstalledPlugin Value) { public string Label => $"{Value.Manifest.Name} · {Value.Manifest.Version} · {(Value.Enabled ? "مفعّل" : "معطّل")}"; }
    public ExtensionsWindow(LocalConversionService converter, BatchProcessor queue, CancellationToken cancellation) : base("الحزم المحلية والإضافات", cancellation)
    {
        Height = 700; Confirm.Visibility = Visibility.Collapsed;
        components.Background = plugins.Background = Paint("#1C2530"); components.Foreground = plugins.Foreground = Foreground;
        components.BorderBrush = plugins.BorderBrush = Paint("#415266");
        Note("استيراد ZIP محلي فقط؛ لا تنزيل تلقائي. حزم المعالجة منفصلة عن المثبّت، وتُطابق ملفاتها فهرس هذه النسخة وبصماته.");
        Body.Children.Add(components);
        Action("استيراد حزمة معالجة…", () =>
        {
            var dialog = new OpenFileDialog { Filter = "Naqla component ZIP|*.zip", CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            PendingWork = WorkAsync(async ct => { await queue.RunExclusiveAsync(async t => { await store.InstallAsync(dialog.FileName, new Progress<int>(percent => { if (!ClosedAlready) Status.Text = $"تحقق واستيراد · {percent}%"; }), t); return true; }, ct); converter.RefreshExtensions(); if (!ClosedAlready) { Refresh(); Status.Text = "اكتمل التحقق والاستيراد المحلي"; } });
        });
        Action("إزالة الحزمة المختارة", () =>
        {
            if (components.SelectedItem is not ComponentLine line || !line.Value.Installed) return;
            if (MessageBox.Show(this, "إزالة ملفات الحزمة المعروفة فقط؟ لا تُحذف ملفات المستخدم أو مخرجاته.", "Naqla", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            PendingWork = WorkAsync(async ct => { await queue.RunExclusiveAsync(t => Task.Run(() => { t.ThrowIfCancellationRequested(); store.Uninstall(line.Value.Feature); return true; }, t), ct); converter.RefreshExtensions(); if (!ClosedAlready) Refresh(); });
        });
        Note("الإضافات: حزم تصريحية بإصدار API محدد. الإصدار الحالي يسمح فقط بحساب SHA-256 وإحصاءات النص عبر عامل منفصل موثوق، ولا يشغّل كوداً من الحزمة. العامل ليس sandbox أمنياً لنظام التشغيل.");
        Body.Children.Add(plugins);
        Action("تثبيت إضافة محلية…", () =>
        {
            var dialog = new OpenFileDialog { Filter = "Naqla plugin ZIP|*.zip", CheckFileExists = true }; if (dialog.ShowDialog(this) != true) return;
            PendingWork = WorkAsync(async ct => { await queue.RunExclusiveAsync(t => converter.Plugins.InstallAsync(dialog.FileName, t), ct); converter.RefreshExtensions(); if (!ClosedAlready) { Refresh(); Status.Text = "ثُبّتت الإضافة معطّلة. راجع الصلاحيات ثم فعّلها بنفسك."; } });
        });
        Action("تفعيل / تعطيل الإضافة", () =>
        {
            if (plugins.SelectedItem is not PluginLine line) return;
            if (!line.Value.Enabled && MessageBox.Show(this, $"{line.Value.Manifest.Name}\nالصلاحيات: قراءة الملفات المختارة وكتابة تقرير جديد بجانبها.\nالعمليات: {string.Join("، ", line.Value.Manifest.Actions.Select(x => x.Label))}\nتفعيل هذه الحزمة المحلية؟", "Naqla · صلاحيات الإضافة", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            PendingWork = WorkAsync(async ct => { await queue.RunExclusiveAsync(t => Task.Run(() => { t.ThrowIfCancellationRequested(); converter.Plugins.SetEnabled(line.Value.Manifest.Id, !line.Value.Enabled); return true; }, t), ct); converter.RefreshExtensions(); if (!ClosedAlready) Refresh(); });
        });
        Action("إزالة الإضافة", () =>
        {
            if (plugins.SelectedItem is not PluginLine line) return;
            if (MessageBox.Show(this, "إزالة حزمة الإضافة المختارة؟", "Naqla", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            PendingWork = WorkAsync(async ct => { await queue.RunExclusiveAsync(t => Task.Run(() => { t.ThrowIfCancellationRequested(); converter.Plugins.Uninstall(line.Value.Manifest.Id); return true; }, t), ct); converter.RefreshExtensions(); if (!ClosedAlready) Refresh(); });
        });
        Refresh();
        void Refresh()
        {
            components.ItemsSource = store.Status().Select(x => new ComponentLine(x));
            plugins.ItemsSource = converter.Plugins.List().Select(x => new PluginLine(x));
        }
    }
}
