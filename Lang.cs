using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;

namespace WinqEmuLauncher
{
    // 配置/数据目录：取真实 exe 所在目录。
    // 单文件版运行时 AppContext.BaseDirectory 指向临时解压目录，用它会导致 lang.txt、
    // vms.json 等写进临时目录、重启丢失；故统一用 Environment.ProcessPath 解析真实目录。
    public static class AppPaths
    {
        public static string BaseDir
        {
            get
            {
                try
                {
                    var p = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(p))
                    {
                        var d = Path.GetDirectoryName(p);
                        if (!string.IsNullOrEmpty(d)) return d;
                    }
                }
                catch { }
                try { return AppContext.BaseDirectory; } catch { }
                return ".";
            }
        }
    }

    // 双语资源：zh-CN / en-US。启动按配置文件或系统语言定默认，界面内下拉可实时切换。
    public static class Lang
    {
        public enum Code { Zh, En }

        static Code _current = Code.Zh;
        public static Code Current => _current;

        static readonly Dictionary<string, string> Zh = new Dictionary<string, string>
        {
            ["AppTitle"] = "WINQ-EMU 启动器",
            ["AppErrorTitle"] = "WINQ-EMU 启动器 - 错误",
            ["VmList"] = "虚拟机列表",
            ["BtnNew"] = "新建",
            ["BtnDup"] = "复制",
            ["BtnDel"] = "删除",
            ["BtnSave"] = "保存",
            ["BtnLaunch"] = "启动",
            ["BtnStop"] = "停止",
            ["BtnOk"] = "确定",
            ["BtnCancel"] = "取消",
            ["CtxStart"] = "开机",
            ["CtxStop"] = "关机",
            ["CtxRestart"] = "重启",
            ["CtxRename"] = "重命名",
            ["CtxDuplicate"] = "复制",
            ["CtxDelete"] = "删除",
            ["CtxExportBat"] = "导出 bat",
            ["TabHardware"] = "核心硬件",
            ["TabStorage"] = "存储与镜像",
            ["TabNetwork"] = "网络与共享",
            ["LblCpu"] = "CPU 核数",
            ["LblRam"] = "内存(GB)",
            ["SecGpu"] = "显卡",
            ["LblHostMem"] = "宿主显存(GB)",
            ["LblGpuMode"] = "显卡模式",
            ["GpuMode0"] = "virtio-gpu (Venus 3D)",
            ["GpuMode1"] = "virtio-gpu (2D)",
            ["GpuMode2"] = "Standard VGA (compatible)",
            ["GpuMode3"] = "无显示 (headless)",
            ["ChkVaapi"] = "VA-API 视频解码",
            ["LblAccel"] = "加速方式",
            ["Accel0"] = "WHPX（Windows Hypervisor Platform）",
            ["Accel1"] = "TCG（Tiny Code Generator）",
            ["LblSound"] = "声卡",
            ["Sound0"] = "virtio-sound-pci",
            ["Sound1"] = "intel-hda",
            ["Sound2"] = "AC97",
            ["Sound3"] = "无",
            ["SecBootOrder"] = "启动设备顺序",
            ["LblBootDevice"] = "优先启动",
            ["Boot0"] = "磁盘",
            ["Boot1"] = "光盘",
            ["Boot2"] = "网络",
            ["SecBootMode"] = "引导模式",
            ["ChkUefi"] = "UEFI 启动（否则传统 BIOS）",
            ["SecDisk"] = "磁盘",
            ["DiskBtnNew"] = "新建",
            ["DiskBtnAdd"] = "添加",
            ["DiskBtnBrowse"] = "浏览",
            ["DiskBtnRemove"] = "移除",
            ["ColDiskPath"] = "磁盘镜像 (Path)",
            ["ColFormat"] = "格式",
            ["ColIface"] = "接口",
            ["PortColProtocol"] = "协议",
            ["PortColHost"] = "主机端口",
            ["PortColGuest"] = "客户机端口",
            ["FolderColHost"] = "主机路径",
            ["FolderColTag"] = "挂载标签",
            ["FolderColSec"] = "安全模型",
            ["SecIso"] = "ISO 镜像",
            ["IsoBtnBrowse"] = "浏览",
            ["SecNic"] = "网卡",
            ["LblNetwork"] = "网络模式",
            ["Net0"] = "virtio-net-pci",
            ["Net1"] = "e1000",
            ["Net2"] = "无",
            ["SecPortFwd"] = "端口转发",
            ["PortBtnAdd"] = "添加",
            ["PortBtnRemove"] = "移除",
            ["SecSharedFolder"] = "共享文件夹",
            ["FolderBtnAdd"] = "添加",
            ["FolderBtnBrowse"] = "浏览",
            ["FolderBtnRemove"] = "移除",
            ["LogTitle"] = "日志",
            ["CreateDiskTitle"] = "新建磁盘镜像",
            ["CdPath"] = "路径",
            ["CdSize"] = "大小(GB)",
            ["CdFormat"] = "格式",
            ["CdBrowse"] = "浏览",
            ["CdCreate"] = "创建",
            ["DlgTitle"] = "输入",
            ["MsgTitle"] = "提示",
            ["NewVmTitle"] = "新建虚拟机",
            ["NewVmPrompt"] = "请输入虚拟机名称：",
            ["InfoTitle"] = "提示",
            ["ErrTitle"] = "错误",
            ["ConfirmTitle"] = "确认",

            ["StatusEditing"] = "正在编辑：{0}",
            ["AppLoaded"] = "已加载 {0} 个虚拟机配置。",
            ["Saved"] = "配置已保存。",
            ["SaveStatus"] = "已保存 {0} 个虚拟机到 {1}",
            ["RunningConflict"] = "该虚拟机已在运行。",
            ["SelectVmFirst"] = "请先选择或创建虚拟机。",
            ["QemuNotFound"] = "错误：在以下路径找不到 QEMU 可执行文件：{0}",
            ["QemuNotFoundDetail"] = "在以下路径找不到 QEMU：\n{0}\n请将启动器放在包含 qemu-system-x86_64w.exe 的 bin 目录同级。",
            ["Launching"] = "正在启动 \"{0}\"，QEMU 路径：{1}",
            ["Launched"] = "进程已启动。画面将在独立 QEMU 窗口中显示。",
            ["SelectVmRightClick"] = "请先右键选中一个虚拟机。",
            ["Exported"] = "已导出启动脚本：{0}",
            ["ExportFailed"] = "导出失败：{0}",
            ["NeedPath"] = "请先选择路径。",
            ["NeedSize"] = "大小必须是正整数(GB)。",
            ["DiskCreated"] = "已创建磁盘：{0}（{1}G，{2}）",
            ["DiskCreateFailed"] = "创建磁盘失败：\n{0}",
            ["ImgNotFound"] = "错误：在以下路径找不到 qemu-img.exe：{0}",
            ["ImgNotFoundDetail"] = "在以下路径找不到 qemu-img.exe：\n{0}",
            ["ImgFailed"] = "qemu-img 失败：{0}",
            ["ImgError"] = "qemu-img 错误：{0}",
            ["DelConfirm"] = "确定删除虚拟机 \"{0}\"？",
            ["DelRunning"] = "该虚拟机正在运行，请先停止。",
            ["DupSelectFirst"] = "请先选择要复制的虚拟机。",
            ["CreateDiskNoVm"] = "请先创建或选择虚拟机。",
            ["UnsavedChanges"] = "有未保存的修改，是否保存？",
            ["Reattached"] = "已重新挂接 {0} 个运行中的虚拟机。",
            ["VmStopped"] = "虚拟机 \"{0}\" 已停止。",
            ["LaunchFailed"] = "启动失败：{0}",
            ["CopySuffix"] = " (副本)",
            ["DiskBrowseFilter"] = "磁盘镜像 (*.qcow2;*.raw;*.img;*.vhdx)|*.qcow2;*.raw;*.img;*.vhdx|所有文件 (*.*)|*.*",
            ["IsoBrowseFilter"] = "ISO 镜像 (*.iso)|*.iso|所有文件 (*.*)|*.*",
            ["CdSaveFilter"] = "qcow2 (*.qcow2)|*.qcow2|raw (*.raw;*.img)|*.raw;*.img|所有文件 (*.*)|*.*",
            ["BatSaveFilter"] = "批处理文件 (*.bat)|*.bat|所有文件 (*.*)|*.*",
            ["SettingsTitle"] = "设置",
            ["LblLanguage"] = "语言",
            ["SetQemuExeW"] = "QEMU GUI 版",
            ["SetQemuExe"] = "QEMU 控制台版",
            ["SetDefaultVmDir"] = "默认磁盘目录",
            ["SetDefaultIsoDir"] = "默认 ISO 目录",
            ["SetLeaveEmptyHint"] = "留空则自动探测启动器目录（bin/ 或启动器目录）；磁盘/ISO 默认 vm 子目录",
            ["ExeFilter"] = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
        };

        static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            ["AppTitle"] = "WINQ-EMU Launcher",
            ["AppErrorTitle"] = "WINQ-EMU Launcher - Error",
            ["VmList"] = "Virtual Machines",
            ["BtnNew"] = "New",
            ["BtnDup"] = "Duplicate",
            ["BtnDel"] = "Delete",
            ["BtnSave"] = "Save",
            ["BtnLaunch"] = "Launch",
            ["BtnStop"] = "Stop",
            ["BtnOk"] = "OK",
            ["BtnCancel"] = "Cancel",
            ["CtxStart"] = "Start",
            ["CtxStop"] = "Shut Down",
            ["CtxRestart"] = "Restart",
            ["CtxRename"] = "Rename",
            ["CtxDuplicate"] = "Duplicate",
            ["CtxDelete"] = "Delete",
            ["CtxExportBat"] = "Export .bat",
            ["TabHardware"] = "Core Hardware",
            ["TabStorage"] = "Storage & Images",
            ["TabNetwork"] = "Network & Sharing",
            ["LblCpu"] = "CPU Cores",
            ["LblRam"] = "Memory (GB)",
            ["SecGpu"] = "Graphics",
            ["LblHostMem"] = "Host VRAM (GB)",
            ["LblGpuMode"] = "GPU Mode",
            ["GpuMode0"] = "virtio-gpu (Venus 3D)",
            ["GpuMode1"] = "virtio-gpu (2D)",
            ["GpuMode2"] = "Standard VGA (compatible)",
            ["GpuMode3"] = "No display (headless)",
            ["ChkVaapi"] = "VA-API video decode",
            ["LblAccel"] = "Acceleration",
            ["Accel0"] = "WHPX (Windows Hypervisor Platform)",
            ["Accel1"] = "TCG (Tiny Code Generator)",
            ["LblSound"] = "Audio",
            ["Sound0"] = "virtio-sound-pci",
            ["Sound1"] = "intel-hda",
            ["Sound2"] = "AC97",
            ["Sound3"] = "None",
            ["SecBootOrder"] = "Boot Order",
            ["LblBootDevice"] = "Boot From",
            ["Boot0"] = "Disk",
            ["Boot1"] = "CD-ROM",
            ["Boot2"] = "Network",
            ["SecBootMode"] = "Boot Mode",
            ["ChkUefi"] = "UEFI boot (otherwise legacy BIOS)",
            ["SecDisk"] = "Disks",
            ["DiskBtnNew"] = "New",
            ["DiskBtnAdd"] = "Add",
            ["DiskBtnBrowse"] = "Browse",
            ["DiskBtnRemove"] = "Remove",
            ["ColDiskPath"] = "Disk Image (Path)",
            ["ColFormat"] = "Format",
            ["ColIface"] = "Interface",
            ["PortColProtocol"] = "Protocol",
            ["PortColHost"] = "Host Port",
            ["PortColGuest"] = "Guest Port",
            ["FolderColHost"] = "Host Path",
            ["FolderColTag"] = "Mount Tag",
            ["FolderColSec"] = "Security Model",
            ["SecIso"] = "ISO Image",
            ["IsoBtnBrowse"] = "Browse",
            ["SecNic"] = "Network Card",
            ["LblNetwork"] = "Network Mode",
            ["Net0"] = "virtio-net-pci",
            ["Net1"] = "e1000",
            ["Net2"] = "None",
            ["SecPortFwd"] = "Port Forwarding",
            ["PortBtnAdd"] = "Add",
            ["PortBtnRemove"] = "Remove",
            ["SecSharedFolder"] = "Shared Folders",
            ["FolderBtnAdd"] = "Add",
            ["FolderBtnBrowse"] = "Browse",
            ["FolderBtnRemove"] = "Remove",
            ["LogTitle"] = "Log",
            ["CreateDiskTitle"] = "Create Disk Image",
            ["CdPath"] = "Path",
            ["CdSize"] = "Size (GB)",
            ["CdFormat"] = "Format",
            ["CdBrowse"] = "Browse",
            ["CdCreate"] = "Create",
            ["DlgTitle"] = "Input",
            ["MsgTitle"] = "Message",
            ["NewVmTitle"] = "New Virtual Machine",
            ["NewVmPrompt"] = "Enter the virtual machine name:",
            ["InfoTitle"] = "Info",
            ["ErrTitle"] = "Error",
            ["ConfirmTitle"] = "Confirm",

            ["StatusEditing"] = "Editing: {0}",
            ["AppLoaded"] = "Loaded {0} VM configuration(s).",
            ["Saved"] = "Configuration saved.",
            ["SaveStatus"] = "Saved {0} VM(s) to {1}",
            ["RunningConflict"] = "This virtual machine is already running.",
            ["SelectVmFirst"] = "Please select or create a virtual machine first.",
            ["QemuNotFound"] = "Error: QEMU executable not found at: {0}",
            ["QemuNotFoundDetail"] = "QEMU not found at:\n{0}\nPlace the launcher next to the bin directory containing qemu-system-x86_64w.exe.",
            ["Launching"] = "Launching \"{0}\", QEMU path: {1}",
            ["Launched"] = "Process started. The display will appear in a separate QEMU window.",
            ["SelectVmRightClick"] = "Please right-click to select a virtual machine first.",
            ["Exported"] = "Startup script exported: {0}",
            ["ExportFailed"] = "Export failed: {0}",
            ["NeedPath"] = "Please choose a path first.",
            ["NeedSize"] = "Size must be a positive integer (GB).",
            ["DiskCreated"] = "Disk created: {0} ({1}G, {2})",
            ["DiskCreateFailed"] = "Failed to create disk:\n{0}",
            ["ImgNotFound"] = "Error: qemu-img.exe not found at: {0}",
            ["ImgNotFoundDetail"] = "qemu-img.exe not found at:\n{0}",
            ["ImgFailed"] = "qemu-img failed: {0}",
            ["ImgError"] = "qemu-img error: {0}",
            ["DelConfirm"] = "Delete virtual machine \"{0}\"?",
            ["DelRunning"] = "This virtual machine is running. Stop it first.",
            ["DupSelectFirst"] = "Please select a virtual machine to duplicate.",
            ["CreateDiskNoVm"] = "Please create or select a virtual machine first.",
            ["UnsavedChanges"] = "You have unsaved changes. Save them?",
            ["Reattached"] = "Reattached {0} running virtual machine(s).",
            ["VmStopped"] = "Virtual machine \"{0}\" stopped.",
            ["LaunchFailed"] = "Launch failed: {0}",
            ["CopySuffix"] = " (copy)",
            ["DiskBrowseFilter"] = "Disk images (*.qcow2;*.raw;*.img;*.vhdx)|*.qcow2;*.raw;*.img;*.vhdx|All files (*.*)|*.*",
            ["IsoBrowseFilter"] = "ISO images (*.iso)|*.iso|All files (*.*)|*.*",
            ["CdSaveFilter"] = "qcow2 (*.qcow2)|*.qcow2|raw (*.raw;*.img)|*.raw;*.img|All files (*.*)|*.*",
            ["BatSaveFilter"] = "Batch files (*.bat)|*.bat|All files (*.*)|*.*",
            ["SettingsTitle"] = "Settings",
            ["LblLanguage"] = "Language",
            ["SetQemuExeW"] = "QEMU GUI",
            ["SetQemuExe"] = "QEMU console",
            ["SetDefaultVmDir"] = "Default disk directory",
            ["SetDefaultIsoDir"] = "Default ISO directory",
            ["SetLeaveEmptyHint"] = "Leave empty to auto-detect the launcher directory (bin/ or launcher dir); disk/ISO default to the vm subfolder",
            ["ExeFilter"] = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
        };

        public static event Action Changed;

        // 语言配置统一存于 config/config.json（见 ProfileStore），由 ProfileStore.Load/SaveLanguage 读写。

        // 启动：优先读上次选择（config.json 的 Language）；无则按系统语言（中文系统默认中文，其它默认英文）。
        public static void Init()
        {
            ProfileStore.Load();
            var lang = ProfileStore.LoadLanguage();
            if (!string.IsNullOrEmpty(lang))
            {
                _current = lang == "en" ? Code.En : Code.Zh;
                return;
            }
            try
            {
                var name = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;
                _current = name.Equals("zh", StringComparison.OrdinalIgnoreCase) ? Code.Zh : Code.En;
            }
            catch { _current = Code.Zh; }
        }

        public static void Set(Code code)
        {
            if (_current == code) return;
            _current = code;
            try { ProfileStore.SaveLanguage(code == Code.En ? "en" : "zh"); } catch { }
            Changed?.Invoke();
        }

        public static string T(string key)
        {
            var dict = _current == Code.En ? En : Zh;
            return dict.TryGetValue(key, out var v) ? v : key;
        }

        public static string T(string key, params object[] args)
        {
            var s = T(key);
            if (args != null && args.Length > 0)
            {
                try { return string.Format(s, args); } catch { }
            }
            return s;
        }
    }

    // XAML 标记扩展：{loc:Loc Key} 绑定到当前语言文本，语言切换时自动刷新。
    [MarkupExtensionReturnType(typeof(string))]
    public class LocExtension : MarkupExtension
    {
        public LocExtension(string key) { Key = key; }
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var binding = new System.Windows.Data.Binding(nameof(LangProxy.Text))
            {
                Source = new LangProxy(Key),
                Mode = System.Windows.Data.BindingMode.OneWay
            };
            return binding.ProvideValue(serviceProvider);
        }
    }

    // 单个绑定代理：订阅 Lang.Changed，语言切换时通知 Text 变化。
    class LangProxy : INotifyPropertyChanged
    {
        readonly string _key;
        public LangProxy(string key) { _key = key; Lang.Changed += OnChanged; }
        void OnChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        public string Text => Lang.T(_key);
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
