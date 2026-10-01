using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace WinqEmuLauncher
{
    public partial class MainWindow : Window
    {
        ObservableCollection<VmConfig> _vms = new ObservableCollection<VmConfig>();
        QemuRunner _runner = new QemuRunner();
        // 每个运行中的虚拟机 -> 对应 QEMU 进程
        Dictionary<VmConfig, Process> _procs = new Dictionary<VmConfig, Process>();
        // VM 名称 -> QEMU 进程 PID（持久化，启动器关闭后仍可恢复）
        Dictionary<string, int> _running = new Dictionary<string, int>();
        string _editOriginalName = "";
        VmConfig _restartVm = null;
        // 上次保存时的序列化快照，用于判断是否有未保存修改
        string _savedSnapshot = "";

        // 磁盘格式 / 接口下拉选项（供 DataGridComboBoxColumn 复用）
        public static readonly string[] DiskFormatOptions = { "auto", "qcow2", "raw" };
        public static readonly string[] DiskIfaceOptions = { "virtio", "scsi", "ide" };
        // 共享文件夹安全模型下拉选项（QEMU 9P local fsdev 合法值）
        public static readonly string[] SecurityModelOptions = { "passthrough", "mapped", "mapped-xattr", "none" };
        // 端口转发协议下拉选项（QEMU hostfwd 仅支持 tcp / udp）
        public static readonly string[] ProtocolOptions = { "tcp", "udp" };

        public MainWindow()
        {
            InitializeComponent();
            Lang.Changed += () => ApplyUiLanguage();
            RestoreWindowGeometry();
            txtLogTitle.Text = Lang.T("LogTitle");
            _vms = ProfileStore.LoadAll();
            lstVms.ItemsSource = _vms;
            // 恢复上次选中的虚拟机
            var last = ProfileStore.LoadLastSelected();
            if (!string.IsNullOrEmpty(last))
            {
                var vm = _vms.FirstOrDefault(v => v.Name == last);
                if (vm != null) lstVms.SelectedItem = vm;
            }
            _runner.Log = (line) => AppendLog(line);
            AppendLog(Lang.T("AppLoaded", _vms.Count));
            ReattachRunning();
            _savedSnapshot = SerializeAll();
        }

        VmConfig Current => ConfigRoot.DataContext as VmConfig;

        void lstVms_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ConfigRoot.DataContext = lstVms.SelectedItem as VmConfig;
            var vm = Current;
            txtStatus.Text = vm == null ? "" : Lang.T("StatusEditing", vm.Name);
            btnDup.IsEnabled = vm != null;
            btnDel.IsEnabled = vm != null;
            if (vm != null) ProfileStore.SaveLastSelected(vm.Name);
        }

        // 切换语言时：重设日志标题/状态文字，并清空日志重打当前语言的开机信息
        // （日志是实时诊断流，历史行不会自动翻译，重打最干净）
        void ApplyUiLanguage()
        {
            if (txtLogTitle == null || txtStatus == null || txtLog == null) return;
            txtLogTitle.Text = Lang.T("LogTitle");
            var vm = Current;
            txtStatus.Text = vm == null ? "" : Lang.T("StatusEditing", vm.Name);
            txtLog.Clear();
            AppendLog(Lang.T("AppLoaded", _vms.Count));
        }

        void btnNew_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new InputDialog(Lang.T("NewVmTitle"), Lang.T("NewVmPrompt"), Lang.T("NewVmTitle"));
            dlg.Owner = this;
            if (dlg.ShowDialog() != true) return;
            if (string.IsNullOrWhiteSpace(dlg.InputText)) return;
            var vm = new VmConfig { Name = dlg.InputText };
            _vms.Add(vm);
            lstVms.SelectedItem = vm;
        }

        void btnDup_Click(object sender, RoutedEventArgs e) => DupCurrent();
        void ctxDup_Click(object sender, RoutedEventArgs e) => DupCurrent();
        void DupCurrent()
        {
            var src = Current;
            if (src == null) { MsgDialog.Info(this, Lang.T("InfoTitle"), Lang.T("DupSelectFirst")); return; }
            var clone = JsonSerializer.Deserialize<VmConfig>(JsonSerializer.Serialize(src));
            clone.Name = src.Name + Lang.T("CopySuffix");
            _vms.Add(clone);
            lstVms.SelectedItem = clone;
        }

        void btnDel_Click(object sender, RoutedEventArgs e) => DelCurrent();
        void ctxDel_Click(object sender, RoutedEventArgs e) => DelCurrent();
        void DelCurrent()
        {
            var vm = Current;
            if (vm == null) return;
            if (_procs.ContainsKey(vm)) { MsgDialog.Info(this, Lang.T("InfoTitle"), Lang.T("DelRunning")); return; }
            if (MsgDialog.Confirm(this, Lang.T("ConfirmTitle"), Lang.T("DelConfirm", vm.Name)) == true)
            {
                _vms.Remove(vm);
                lstVms.SelectedItem = null;
                ConfigRoot.DataContext = null;
                btnDup.IsEnabled = false;
                btnDel.IsEnabled = false;
            }
        }

        void btnIsoBrowse_Click(object sender, RoutedEventArgs e)
        {
            var vm = Current;
            if (vm == null) return;
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = Lang.T("IsoBrowseFilter"), InitialDirectory = FirstValidDir(ProfileStore.Data.DefaultIsoDir) };
            if (dlg.ShowDialog() == true) vm.IsoImage = dlg.FileName;
        }

        void btnDiskAdd_Click(object sender, RoutedEventArgs e)
        {
            var vm = Current; if (vm == null) return;
            vm.Disks.Add(new DiskEntry());
        }
        void btnDiskRemove_Click(object sender, RoutedEventArgs e)
        {
            var vm = Current; if (vm == null) return;
            if (dgDisks.SelectedItem is DiskEntry d) vm.Disks.Remove(d);
        }

        void btnDiskBrowse_Click(object sender, RoutedEventArgs e)
        {
            var vm = Current; if (vm == null) return;
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = Lang.T("DiskBrowseFilter"),
                InitialDirectory = FirstValidDir(ProfileStore.Data.DefaultVmDir)
            };
            if (dlg.ShowDialog() != true) return;
            if (dgDisks.SelectedItem is DiskEntry d) d.Path = dlg.FileName;
            else { var nd = new DiskEntry { Path = dlg.FileName }; vm.Disks.Add(nd); dgDisks.SelectedItem = nd; }
        }

        void btnDiskCreate_Click(object sender, RoutedEventArgs e)
        {
            var vm = Current;
            if (vm == null) { MsgDialog.Info(this, Lang.T("InfoTitle"), Lang.T("CreateDiskNoVm")); return; }
            var dlg = new CreateDiskDialog { Owner = this, DefaultDirectory = FirstValidDir(ProfileStore.Data.DefaultVmDir) };
            if (dlg.ShowDialog() != true) return;

            string binDir = QemuRunner.ResolveBinDir();
            string img = Path.Combine(binDir, "qemu-img.exe");
            if (!File.Exists(img))
            {
                AppendLog(Lang.T("ImgNotFound", binDir));
                MsgDialog.Info(this, Lang.T("ErrTitle"), Lang.T("ImgNotFoundDetail", binDir));
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = img,
                Arguments = "create -f " + dlg.Format + " \"" + dlg.DiskPath + "\" " + dlg.SizeGB + "G",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WorkingDirectory = binDir
            };
            try
            {
                var p = new Process { StartInfo = psi };
                p.Start();
                p.WaitForExit();
                string err = p.StandardError.ReadToEnd();
                string outp = p.StandardOutput.ReadToEnd();
                if (p.ExitCode == 0)
                {
                    vm.Disks.Add(new DiskEntry { Path = dlg.DiskPath, Format = dlg.Format, Iface = "virtio" });
                    AppendLog(Lang.T("DiskCreated", dlg.DiskPath, dlg.SizeGB, dlg.Format));
                }
                else
                {
                    AppendLog(Lang.T("ImgFailed", err));
                    MsgDialog.Info(this, Lang.T("ErrTitle"), Lang.T("DiskCreateFailed", err));
                }
            }
            catch (Exception ex)
            {
                AppendLog(Lang.T("ImgError", ex.Message));
                MsgDialog.Info(this, Lang.T("ErrTitle"), ex.Message);
            }
        }

        void btnPortAdd_Click(object sender, RoutedEventArgs e)
        {
            var vm = Current; if (vm == null) return;
            vm.PortForwards.Add(new PortForward());
        }
        void btnPortRemove_Click(object sender, RoutedEventArgs e)
        {
            var vm = Current; if (vm == null) return;
            if (dgPorts.SelectedItem is PortForward p) vm.PortForwards.Remove(p);
        }

        void btnFolderAdd_Click(object sender, RoutedEventArgs e)
        {
            var vm = Current; if (vm == null) return;
            vm.SharedFolders.Add(new SharedFolder());
        }
        void btnFolderRemove_Click(object sender, RoutedEventArgs e)
        {
            var vm = Current; if (vm == null) return;
            if (dgFolders.SelectedItem is SharedFolder f) vm.SharedFolders.Remove(f);
        }

        void btnFolderBrowse_Click(object sender, RoutedEventArgs e)
        {
            var vm = Current; if (vm == null) return;
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            if (dgFolders.SelectedItem is SharedFolder f) f.HostPath = dlg.SelectedPath;
            else { var nf = new SharedFolder { HostPath = dlg.SelectedPath }; vm.SharedFolders.Add(nf); dgFolders.SelectedItem = nf; }
        }

        void btnSave_Click(object sender, RoutedEventArgs e)
        {
            ProfileStore.SaveAll();
            txtStatus.Text = Lang.T("SaveStatus", _vms.Count, ProfileStore.ConfigFile);
            AppendLog(Lang.T("Saved"));
            _savedSnapshot = SerializeAll();
        }

        // 将当前所有虚拟机序列化为 JSON，用于与上一次保存快照比对，判断是否有未保存修改
        string SerializeAll() => JsonSerializer.Serialize(_vms);
        bool IsDirty() => SerializeAll() != _savedSnapshot;

        // 关闭窗口时，若有未保存修改则提示保存/不保存/取消
        // 恢复上次窗口位置/大小；无记录或落点不在任何可见屏幕内则居中
        void RestoreWindowGeometry()
        {
            var cfg = ProfileStore.Data;
            if (!cfg.WindowLeft.HasValue || !cfg.WindowTop.HasValue)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                return;
            }
            double w = cfg.WindowWidth ?? Width;
            double h = cfg.WindowHeight ?? Height;
            bool onScreen = false;
            foreach (var s in System.Windows.Forms.Screen.AllScreens)
            {
                var wa = s.WorkingArea;
                if (cfg.WindowLeft.Value >= wa.Left && cfg.WindowTop.Value >= wa.Top &&
                    cfg.WindowLeft.Value < wa.Right && cfg.WindowTop.Value < wa.Bottom)
                {
                    onScreen = true;
                    break;
                }
            }
            if (onScreen)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = cfg.WindowLeft.Value;
                Top = cfg.WindowTop.Value;
                if (cfg.WindowWidth.HasValue) Width = cfg.WindowWidth.Value;
                if (cfg.WindowHeight.HasValue) Height = cfg.WindowHeight.Value;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        // 保存当前窗口位置/大小（关闭时调用；取消关闭时不会执行到这里）
        void SaveWindowGeometry()
        {
            var cfg = ProfileStore.Data;
            cfg.WindowLeft = Left;
            cfg.WindowTop = Top;
            cfg.WindowWidth = Width;
            cfg.WindowHeight = Height;
            ProfileStore.Save();
        }

        void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (IsDirty())
            {
                var r = MessageBox.Show(this, Lang.T("UnsavedChanges"), Lang.T("ConfirmTitle"),
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (r == MessageBoxResult.Cancel) { e.Cancel = true; return; }
                if (r == MessageBoxResult.Yes)
                {
                    ProfileStore.SaveAll();
                    AppendLog(Lang.T("Saved"));
                }
                // No：不保存 VM 改动，直接关闭
            }
            SaveWindowGeometry();
        }

        void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SettingsWindow { Owner = this };
            dlg.ShowDialog();
        }

        // 非空且（尽量）存在的目录才用作浏览初始目录；否则回退 vm/（磁盘镜像与 ISO 实际所在处）。
        static string FirstValidDir(string s) => string.IsNullOrWhiteSpace(s) ? Path.Combine(AppPaths.BaseDir, "vm") : s;

        void btnLaunch_Click(object sender, RoutedEventArgs e) => LaunchCurrent();
        void ctxStart_Click(object sender, RoutedEventArgs e) => LaunchCurrent();
        void LaunchCurrent() => LaunchVm(Current);
        void LaunchVm(VmConfig vm)
        {
            if (vm == null) { MsgDialog.Info(this, Lang.T("InfoTitle"), Lang.T("SelectVmFirst")); return; }
            if (_procs.ContainsKey(vm)) { MsgDialog.Info(this, Lang.T("InfoTitle"), Lang.T("RunningConflict")); return; }
            QemuRunner.Resolve(_runner);
            if (!File.Exists(_runner.QemuExeW))
            {
                AppendLog(Lang.T("QemuNotFound", _runner.BinDir));
                MsgDialog.Info(this, Lang.T("ErrTitle"), Lang.T("QemuNotFoundDetail", _runner.BinDir));
                return;
            }
            AppendLog(Lang.T("Launching", vm.Name, _runner.BinDir));
            try
            {
                var proc = _runner.Launch(vm);
                if (proc != null)
                {
                    vm.IsRunning = true;
                    proc.EnableRaisingEvents = true;
                    var captured = vm;
                    proc.Exited += (s, ev) => OnVmExited(captured);
                    _procs[vm] = proc;
                    _running[vm.Name] = proc.Id;
                    ProfileStore.SaveRunning(_running);
                    AppendLog(Lang.T("Launched"));
                }
            }
            catch (Exception ex)
            {
                AppendLog(Lang.T("LaunchFailed", ex.Message));
            }
        }

        // 右键菜单：导出 bat（按当前配置生成可直接启动 QEMU 的批处理脚本）
        void ctxExportBat_Click(object sender, RoutedEventArgs e)
        {
            var vm = lstVms.SelectedItem as VmConfig;
            if (vm == null) { MsgDialog.Info(this, Lang.T("InfoTitle"), Lang.T("SelectVmRightClick")); return; }
            QemuRunner.Resolve(_runner);
            // 预先创建 EFI NVRAM 文件（与启动器 Launch 一致），并在 bat 里加缺失保护
            _runner.EnsureEfivarsFile(vm);
            var args = _runner.BuildArgs(vm);
            var binDir = _runner.BinDir;
            var dlg = new SaveFileDialog
            {
                Filter = "批处理文件 (*.bat)|*.bat|所有文件 (*.*)|*.*",
                FileName = SanitizeFileName(vm.Name) + ".bat",
                InitialDirectory = FirstValidDir(ProfileStore.Data.DefaultVmDir)
            };
            if (dlg.ShowDialog() != true) return;
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("REM WINQ-EMU startup script - " + vm.Name);
            sb.AppendLine("REM Exported by WINQ-EMU launcher (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")");
            sb.AppendLine("setlocal");
            sb.AppendLine("cd /d \"" + binDir + "\"");
            if (vm.UseEfi)
            {
                var varsFd = _runner.ComputeEfivarsPath(vm);
                if (!string.IsNullOrEmpty(varsFd))
                    sb.AppendLine("if not exist \"" + varsFd + "\" fsutil file createnew \"" + varsFd + "\" 4194304");
            }
            if (vm.UseVaapi) sb.AppendLine("set WINQ_VAAPI=1");
            string consoleExe = File.Exists(_runner.QemuExe) ? _runner.QemuExe : _runner.QemuExeW;
            sb.AppendLine("\"" + consoleExe + "\" " + string.Join(" ", args));
            sb.AppendLine("endlocal");
            try
            {
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.Default);
                AppendLog(Lang.T("Exported", dlg.FileName));
            }
            catch (Exception ex)
            {
                AppendLog(Lang.T("ExportFailed", ex.Message));
                MsgDialog.Info(this, Lang.T("ErrTitle"), Lang.T("ExportFailed", ex.Message));
            }
        }

        static string SanitizeFileName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "vm";
            var invalid = Path.GetInvalidFileNameChars();
            var b = new StringBuilder(s.Length);
            foreach (var c in s) b.Append(invalid.Contains(c) ? '_' : c);
            var r = b.ToString().Trim();
            return r.Length == 0 ? "vm" : r;
        }

        // 右键菜单：关机/重启
        void ctxStop_Click(object sender, RoutedEventArgs e) => StopCurrentVm();
        void ctxRestart_Click(object sender, RoutedEventArgs e)
        {
            var vm = Current;
            if (vm == null) return;
            if (!_procs.ContainsKey(vm)) { LaunchVm(vm); return; }
            _restartVm = vm;
            StopCurrentVm();
        }

        void btnStop_Click(object sender, RoutedEventArgs e) => StopCurrentVm();

        void StopCurrentVm()
        {
            var vm = Current;
            if (vm == null || !_procs.TryGetValue(vm, out var proc)) return;
            try
            {
                // 友好关闭：先发 WM_CLOSE，超时再强杀
                if (!proc.CloseMainWindow()) proc.Kill();
                else if (!proc.WaitForExit(3000)) proc.Kill();
            }
            catch
            {
                try { proc.Kill(); } catch { }
            }
        }

        // 进程退出回调（后台线程触发，需切回 UI 线程）
        void OnVmExited(VmConfig vm)
        {
            // 窗口已关闭则不再操作 UI（否则 Dispatcher.Invoke 在已关闭的调度器上抛异常导致崩溃）
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            try
            {
                Dispatcher.Invoke(() =>
                {
                    vm.IsRunning = false;
                    if (_procs.ContainsKey(vm)) _procs.Remove(vm);
                    if (_running.ContainsKey(vm.Name)) { _running.Remove(vm.Name); ProfileStore.SaveRunning(_running); }
                    AppendLog(Lang.T("VmStopped", vm.Name));
                    if (_restartVm != null) { var r = _restartVm; _restartVm = null; LaunchVm(r); }
                });
            }
            catch { }
        }

        // 启动器重启后，按持久化的 PID 重新挂接仍在运行的 QEMU 进程
        void ReattachRunning()
        {
            var saved = ProfileStore.LoadRunning();
            var live = new Dictionary<string, int>();
            foreach (var kv in saved)
            {
                var vm = _vms.FirstOrDefault(v => v.Name == kv.Key);
                if (vm == null) continue;
                Process proc = null;
                try { proc = Process.GetProcessById(kv.Value); } catch { proc = null; }
                if (proc == null || proc.HasExited || !IsQemuProcess(proc)) continue;
                proc.EnableRaisingEvents = true;
                var captured = vm;
                proc.Exited += (s, ev) => OnVmExited(captured);
                _procs[captured] = proc;
                captured.IsRunning = true;
                live[kv.Key] = kv.Value;
            }
            _running = live;
            ProfileStore.SaveRunning(_running);
            if (_running.Count > 0) AppendLog(Lang.T("Reattached", _running.Count));
        }

        bool IsQemuProcess(Process p)
        {
            try
            {
                return p.ProcessName.Equals("qemu-system-x86_64w", StringComparison.OrdinalIgnoreCase)
                    || p.ProcessName.Equals("qemu-system-x86_64", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        // 右键选中该项，再弹菜单
        void lstVms_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var hit = VisualTreeHelper.HitTest(lstVms, e.GetPosition(lstVms))?.VisualHit;
            while (hit != null && !(hit is ListBoxItem)) hit = VisualTreeHelper.GetParent(hit);
            if (hit is ListBoxItem lbi) lstVms.SelectedItem = lbi.DataContext;
        }

        void lstVms_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Current != null) BeginRename();
        }

        void ctxRename_Click(object sender, RoutedEventArgs e) => BeginRename();

        // 原地编辑重命名（Windows 风格）
        void BeginRename()
        {
            var vm = Current;
            if (vm == null) return;
            _editOriginalName = vm.Name;
            vm.IsEditing = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var lbi = lstVms.ItemContainerGenerator.ContainerFromItem(vm) as ListBoxItem;
                var tb = lbi == null ? null : FindVisualChild<TextBox>(lbi, "txtEdit");
                if (tb != null) { tb.Focus(); tb.SelectAll(); }
            }), DispatcherPriority.Loaded);
        }

        void RenameTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var tb = sender as TextBox;
            var vm = tb?.DataContext as VmConfig;
            if (vm == null) return;
            if (e.Key == Key.Enter)
            {
                vm.IsEditing = false;
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                vm.Name = _editOriginalName;
                vm.IsEditing = false;
                e.Handled = true;
            }
        }

        void RenameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var tb = sender as TextBox;
            var vm = tb?.DataContext as VmConfig;
            if (vm != null && vm.IsEditing) vm.IsEditing = false; // 失焦即提交（名称已实时绑定）
        }

        static T FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var c = VisualTreeHelper.GetChild(parent, i);
                if (c is T t && t.Name == name) return t;
                var r = FindVisualChild<T>(c, name);
                if (r != null) return r;
            }
            return null;
        }

        // 日志标题条：点击折叠/展开
        void LogHeader_Click(object sender, RoutedEventArgs e)
        {
            bool collapsed = txtLog.Visibility == System.Windows.Visibility.Collapsed;
            txtLog.Visibility = collapsed ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            txtLogChevron.Data = collapsed
                ? Geometry.Parse("M2,6 L10,6")
                : Geometry.Parse("M2,6 L10,6 M6,2 L6,10");
        }

        void AppendLog(string line)
        {
            if (txtLog == null) return;
            // 后台线程（QEMU 进程退出/输出回调）调用时切回 UI 线程。
            // 若窗口已关闭、Dispatcher 正在/已经关闭，Invoke 会抛异常且发生在非 UI 线程，
            // 导致进程直接崩溃；此处直接丢弃日志，避免这类次生崩溃。
            if (!txtLog.Dispatcher.CheckAccess())
            {
                if (txtLog.Dispatcher.HasShutdownStarted || txtLog.Dispatcher.HasShutdownFinished) return;
                try { txtLog.Dispatcher.Invoke(() => AppendLog(line)); } catch { }
                return;
            }
            try
            {
                txtLog.AppendText(line + Environment.NewLine);
                txtLog.ScrollToEnd();
            }
            catch { }
        }
    }
}
