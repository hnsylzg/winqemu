using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace WinqEmuLauncher
{
    public class DiskEntry : INotifyPropertyChanged
    {
        private string _path = "";
        private string _format = "auto"; // auto / qcow2 / raw
        private string _iface = "virtio"; // virtio / scsi / ide

        public string Path { get => _path; set { _path = value; OnPropertyChanged(); } }
        public string Format { get => _format; set { _format = value; OnPropertyChanged(); } }
        public string Iface { get => _iface; set { _iface = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string n = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class PortForward : INotifyPropertyChanged
    {
        private string _protocol = "tcp"; // tcp / udp
        private string _hostPort = "2220";
        private string _guestPort = "22";

        public string Protocol { get => _protocol; set { _protocol = value; OnPropertyChanged(); } }
        public string HostPort { get => _hostPort; set { _hostPort = value; OnPropertyChanged(); } }
        public string GuestPort { get => _guestPort; set { _guestPort = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string n = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class SharedFolder : INotifyPropertyChanged
    {
        private string _hostPath = "";
        private string _tag = "shared";
        private string _securityModel = "mapped-xattr";

        public string HostPath { get => _hostPath; set { _hostPath = value; OnPropertyChanged(); } }
        public string Tag { get => _tag; set { _tag = value; OnPropertyChanged(); } }
        public string SecurityModel { get => _securityModel; set { _securityModel = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string n = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class VmConfig : INotifyPropertyChanged
    {
        private string _name = "New VM";
        private string _isoImage = "";
        private int _cpuCores = 4;
        private int _ramGb = 8;
        private int _bootDevice = 0; // 0 disk, 1 cdrom, 2 net
        private bool _useEfi = false;
        private string _explicitEfivarsPath = "";
        private int _gpuMode = 0; // 0 virtio-gpu(Venus 3D), 1 virtio-gpu(2D), 2 标准 VGA, 3 无显示
        private int _hostMemGb = 4;
        private bool _useVaapi = false;
        private int _sound = 0; // 0 virtio, 1 intel-hda, 2 ac97, 3 none
        private int _network = 0; // 0 virtio-net, 1 e1000, 2 none
        private int _accel = 0; // 0 whpx, 1 tcg

        public int Accel { get => _accel; set { _accel = value; OnPropertyChanged(); } }

        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        public string IsoImage { get => _isoImage; set { _isoImage = value; OnPropertyChanged(); } }
        public int CpuCores { get => _cpuCores; set { _cpuCores = value; OnPropertyChanged(); } }
        public int RamGb { get => _ramGb; set { _ramGb = value; OnPropertyChanged(); } }
        public int BootDevice { get => _bootDevice; set { _bootDevice = value; OnPropertyChanged(); } }
        public bool UseEfi { get => _useEfi; set { _useEfi = value; OnPropertyChanged(); } }
        public string ExplicitEfivarsPath { get => _explicitEfivarsPath; set { _explicitEfivarsPath = value; OnPropertyChanged(); } }
        public int GpuMode { get => _gpuMode; set { _gpuMode = value; OnPropertyChanged(); } }
        public int HostMemGb { get => _hostMemGb; set { _hostMemGb = value; OnPropertyChanged(); } }
        public bool UseVaapi { get => _useVaapi; set { _useVaapi = value; OnPropertyChanged(); } }
        public int Sound { get => _sound; set { _sound = value; OnPropertyChanged(); } }
        public int Network { get => _network; set { _network = value; OnPropertyChanged(); } }

        // 运行时状态，不随配置持久化
        [JsonIgnore]
        public bool IsRunning { get => _isRunning; set { _isRunning = value; OnPropertyChanged(); } }
        private bool _isRunning = false;

        // 列表内重命名时的原地编辑态，不持久化
        [JsonIgnore]
        public bool IsEditing { get => _isEditing; set { _isEditing = value; OnPropertyChanged(); } }
        private bool _isEditing = false;

        public ObservableCollection<DiskEntry> Disks { get; set; } = new ObservableCollection<DiskEntry>();
        public ObservableCollection<PortForward> PortForwards { get; set; } = new ObservableCollection<PortForward>();
        public ObservableCollection<SharedFolder> SharedFolders { get; set; } = new ObservableCollection<SharedFolder>();

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string n = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
