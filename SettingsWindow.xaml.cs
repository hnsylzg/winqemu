using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace WinqEmuLauncher
{
    // 全局设置：语言 + QEMU 两个可执行文件 + 默认磁盘目录 + 默认 ISO 目录。
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            txtQemuExeW.Text = ProfileStore.Data.QemuExeW ?? "";
            txtQemuExe.Text = ProfileStore.Data.QemuExe ?? "";
            txtVmDir.Text = ProfileStore.Data.DefaultVmDir ?? "";
            txtIsoDir.Text = ProfileStore.Data.DefaultIsoDir ?? "";
            cmbLang.SelectedIndex = (Lang.Current == Lang.Code.En) ? 1 : 0;
        }

        // 浏览文件夹（默认磁盘/ISO 目录）
        void PickFolder(System.Windows.Controls.TextBox tb, string fallback)
        {
            var dlg = new FolderBrowserDialog();
            var start = (!string.IsNullOrWhiteSpace(tb.Text) && Directory.Exists(tb.Text)) ? tb.Text : fallback;
            if (!string.IsNullOrWhiteSpace(start) && Directory.Exists(start))
                dlg.SelectedPath = start;
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                tb.Text = dlg.SelectedPath;
        }

        // 浏览文件（QEMU 可执行文件 *.exe）
        void PickFile(System.Windows.Controls.TextBox tb, string fallback)
        {
            var dlg = new OpenFileDialog
            {
                Filter = Lang.T("ExeFilter"),
                Multiselect = false
            };
            string dir = null;
            if (!string.IsNullOrWhiteSpace(tb.Text) && File.Exists(tb.Text)) dir = Path.GetDirectoryName(tb.Text);
            else if (!string.IsNullOrWhiteSpace(tb.Text) && Directory.Exists(tb.Text)) dir = tb.Text;
            else if (!string.IsNullOrWhiteSpace(fallback) && Directory.Exists(fallback)) dir = fallback;
            if (!string.IsNullOrWhiteSpace(dir)) dlg.InitialDirectory = dir;
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                tb.Text = dlg.FileName;
        }

        static string QemuFallback()
        {
            string bin = Path.Combine(AppPaths.BaseDir, "bin");
            return Directory.Exists(bin) ? bin : AppPaths.BaseDir;
        }

        void btnQemuExeWBrowse_Click(object s, RoutedEventArgs e) => PickFile(txtQemuExeW, QemuFallback());
        void btnQemuExeBrowse_Click(object s, RoutedEventArgs e) => PickFile(txtQemuExe, QemuFallback());
        void btnVmBrowse_Click(object s, RoutedEventArgs e) => PickFolder(txtVmDir, Path.Combine(AppPaths.BaseDir, "vm"));
        void btnIsoBrowse_Click(object s, RoutedEventArgs e) => PickFolder(txtIsoDir, Path.Combine(AppPaths.BaseDir, "vm"));

        // 语言实时切换并持久化（Lang.Set 触发全局 Changed，主窗与设置窗的 Loc 绑定自动刷新）
        void cmbLang_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cmbLang.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            {
                var tag = item.Tag as string;
                Lang.Set(tag == "En" ? Lang.Code.En : Lang.Code.Zh);
            }
        }

        void btnOk_Click(object s, RoutedEventArgs e)
        {
            ProfileStore.Data.QemuExeW = txtQemuExeW.Text.Trim();
            ProfileStore.Data.QemuExe = txtQemuExe.Text.Trim();
            ProfileStore.Data.DefaultVmDir = txtVmDir.Text.Trim();
            ProfileStore.Data.DefaultIsoDir = txtIsoDir.Text.Trim();
            ProfileStore.Save();
            DialogResult = true;
        }

        void btnCancel_Click(object s, RoutedEventArgs e) => DialogResult = false;
    }
}
