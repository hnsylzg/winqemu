using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WinqEmuLauncher
{
    // 单一配置文件 config/config.json 的内存模型：语言 + VM 列表 + 运行态 + 上次选中。
    // Vms 用 ObservableCollection，UI 增删即时刷新；序列化时等同 JSON 数组。
    public class AppConfig
    {
        public string Language { get; set; }
        public ObservableCollection<VmConfig> Vms { get; set; } = new ObservableCollection<VmConfig>();
        public Dictionary<string, int> Running { get; set; } = new Dictionary<string, int>();
        public string LastSelected { get; set; }
        // 全局设置（与每台的 VM 配置区分）
        public string QemuExeW { get; set; } = "";  // qemu-system-x86_64w.exe（GUI 版，启动 VM 用）；空=自动探测
        public string QemuExe { get; set; } = "";   // qemu-system-x86_64.exe（控制台版，导出 bat 用）；空=同目录探测
        public string DefaultVmDir { get; set; } = "";   // 新建磁盘默认落盘目录；空=默认值
        public string DefaultIsoDir { get; set; } = "";  // 浏览 ISO 的初始目录；空=默认值
        // 主窗口几何：null=未记录，启动时居中；否则恢复到上次位置/大小
        public double? WindowLeft { get; set; }
        public double? WindowTop { get; set; }
        public double? WindowWidth { get; set; }
        public double? WindowHeight { get; set; }
    }
}
