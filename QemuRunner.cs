using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace WinqEmuLauncher
{
    // Builds the QEMU command line from a VmConfig and runs it.
    // Reuses the validated logic from the old WINQ-EMU.cs: explicit
    // virtio-blk-pci, EFI pflash (skip when no disk), -vga none, Venus/VA-API,
    // plus port auto-allocation and stderr capture.
    public class QemuRunner
    {
        public string QemuExeW { get; set; }   // qemu-system-x86_64w.exe（无窗口版，启动 VM）
        public string QemuExe { get; set; }    // qemu-system-x86_64.exe（控制台版，导出 bat）
        public Action<string> Log { get; set; }

        public string BinDir => string.IsNullOrWhiteSpace(QemuExeW)
            ? AppPaths.BaseDir
            : (Path.GetDirectoryName(QemuExeW) ?? AppPaths.BaseDir);

        // 解析两个 QEMU 可执行文件路径：优先用设置里显式指定的 exe；
        // 窗口版缺失则回退到启动器目录/bin 或启动器目录；控制台版缺失则取窗口版同目录探测。
        public static void Resolve(QemuRunner runner)
        {
            string w = ProfileStore.Data?.QemuExeW;
            string c = ProfileStore.Data?.QemuExe;
            bool wOk = !string.IsNullOrWhiteSpace(w) && File.Exists(w);
            bool cOk = !string.IsNullOrWhiteSpace(c) && File.Exists(c);
            if (wOk)
            {
                if (!cOk) c = Path.Combine(Path.GetDirectoryName(w), "qemu-system-x86_64.exe");
            }
            else
            {
                // 回退：启动器目录/bin 或 启动器目录
                string bin = Path.Combine(AppPaths.BaseDir, "bin");
                if (File.Exists(Path.Combine(bin, "qemu-system-x86_64w.exe"))) w = Path.Combine(bin, "qemu-system-x86_64w.exe");
                else if (File.Exists(Path.Combine(AppPaths.BaseDir, "qemu-system-x86_64w.exe"))) w = Path.Combine(AppPaths.BaseDir, "qemu-system-x86_64w.exe");
                else w = Path.Combine(bin, "qemu-system-x86_64w.exe"); // 猜测路径（用于报错提示）
                if (!cOk) c = Path.Combine(Path.GetDirectoryName(w) ?? AppPaths.BaseDir, "qemu-system-x86_64.exe");
            }
            runner.QemuExeW = w;
            runner.QemuExe = c;
        }

        // 仅取 bin 目录（qemu-img / share/edk2 所在目录），供新建磁盘等使用。
        public static string ResolveBinDir()
        {
            string w = ProfileStore.Data?.QemuExeW;
            if (!string.IsNullOrWhiteSpace(w) && File.Exists(w)) return Path.GetDirectoryName(w);
            string bin = Path.Combine(AppPaths.BaseDir, "bin");
            if (File.Exists(Path.Combine(bin, "qemu-system-x86_64w.exe")) ||
                File.Exists(Path.Combine(bin, "qemu-system-x86_64.exe"))) return bin;
            if (File.Exists(Path.Combine(AppPaths.BaseDir, "qemu-system-x86_64w.exe")) ||
                File.Exists(Path.Combine(AppPaths.BaseDir, "qemu-system-x86_64.exe"))) return AppPaths.BaseDir;
            return bin;
        }

        public string ComputeEfivarsPath(VmConfig vm)
        {
            if (!string.IsNullOrEmpty(vm.ExplicitEfivarsPath)) return vm.ExplicitEfivarsPath;
            foreach (var d in vm.Disks)
            {
                if (!string.IsNullOrWhiteSpace(d.Path))
                    return Path.ChangeExtension(d.Path, null) + "-efivars.fd";
            }
            return null;
        }

        public void EnsureEfivarsFile(VmConfig vm)
        {
            if (!vm.UseEfi) return;
            var vars = ComputeEfivarsPath(vm);
            if (vars == null || File.Exists(vars)) return;
            using (var fs = File.Create(vars)) fs.SetLength(4 * 1024 * 1024);
        }

        // Find a TCP port that can actually be bound (skips ports reserved by
        // VMware NAT / WinNAT that netstat won't show but bind() rejects).
        static int FindFreePort(int start)
        {
            for (int p = start; p < start + 2000; p++)
            {
                try
                {
                    var l = new TcpListener(IPAddress.Loopback, p);
                    l.Start();
                    int port = ((IPEndPoint)l.LocalEndpoint).Port;
                    l.Stop();
                    return port;
                }
                catch (SocketException) { }
            }
            return start;
        }

        string BuildPortForwards(VmConfig vm)
        {
            var sb = new StringBuilder();
            foreach (var pf in vm.PortForwards)
            {
                var proto = (pf.Protocol == "udp") ? "udp" : "tcp";
                int hp;
                if (!int.TryParse(pf.HostPort, out hp) || hp < 1) hp = 2220;
                hp = FindFreePort(hp);
                int gp;
                if (!int.TryParse(pf.GuestPort, out gp) || gp < 1) gp = 22;
                sb.Append(",hostfwd=" + proto + "::" + hp + "-:" + gp);
            }
            return sb.ToString();
        }

        public List<string> BuildArgs(VmConfig vm)
        {
            var args = new List<string>();
            string accel = (vm.Accel == 1) ? "tcg" : "whpx";
            string cpu = (vm.Accel == 1) ? "max" : "host";
            args.Add("-machine q35,accel=" + accel);
            args.Add("-cpu " + cpu);

            int cores = vm.CpuCores >= 1 ? vm.CpuCores : 1;
            args.Add("-smp " + cores);
            int ram = vm.RamGb >= 1 ? vm.RamGb : 4;
            args.Add("-m " + ram + "G");

            // 启动顺序：用 bootindex 统一控制（EFI/BIOS 都按 fw_cfg bootorder 排序；
            // -boot 在 OVMF 写入 NVRAM 后失效）。被选中的设备类型给最小的一组 bootindex（从 1 递增），
            // 其余类型给较大的一组（从 100 递增）——QEMU 要求每个设备的 bootindex 必须全局唯一，
            // 重复会直接报错导致启动失败。光驱挂载方式对齐现有批处理：virtio-blk-pci + id=cd0。
            int topBoot = 1;   // 选中类型设备的 bootindex 计数（越小越先启动）
            int lowBoot = 100; // 非选中类型的 bootindex 计数
            int DiskBi() => (vm.BootDevice == 0) ? topBoot++ : lowBoot++;
            int CdBi() => (vm.BootDevice == 1) ? topBoot++ : lowBoot++;
            int NetBi() => (vm.BootDevice == 2) ? topBoot++ : lowBoot++;

            int diskIdx = 0;
            foreach (var d in vm.Disks)
            {
                if (string.IsNullOrWhiteSpace(d.Path)) continue;
                string path = d.Path.Trim();
                string fmt = d.Format;
                if (fmt == "auto" || fmt.Length == 0)
                    fmt = path.EndsWith(".qcow2", StringComparison.OrdinalIgnoreCase) ? "qcow2" : "raw";
                string iface = d.Iface;
                if (iface.Length == 0) iface = "virtio";

                if (iface == "virtio")
                {
                    string id = "hd" + diskIdx;
                    args.Add("-drive if=none,id=" + id + ",file=\"" + path + "\",format=" + fmt);
                    args.Add("-device virtio-blk-pci,drive=" + id + ",bootindex=" + DiskBi());
                }
                else
                {
                    args.Add("-drive file=\"" + path + "\",format=" + fmt + ",if=" + iface + ",bootindex=" + DiskBi());
                }
                diskIdx++;
            }

            if (!string.IsNullOrWhiteSpace(vm.IsoImage))
            {
                args.Add("-drive if=none,id=cd0,file=\"" + vm.IsoImage.Trim() + "\",format=raw,readonly=on");
                args.Add("-device virtio-blk-pci,drive=cd0,bootindex=" + CdBi());
            }

            // UEFI (EFI) firmware boot. Requires a disk to derive the per-VM
            // NVRAM; skip pflash entirely when none exists.
            if (vm.UseEfi)
            {
                string varsFd = ComputeEfivarsPath(vm);
                if (varsFd != null)
                {
                    string codeFd = Path.Combine(BinDir, "share", "edk2-x86_64-code.fd");
                    args.Add("-drive if=pflash,format=raw,unit=0,readonly=on,file=\"" + codeFd + "\"");
                    args.Add("-drive if=pflash,format=raw,unit=1,file=\"" + varsFd + "\"");
                }
            }

            // 显卡/显示后端：由 GpuMode 决定
            switch (vm.GpuMode)
            {
                case 1: // virtio-gpu (2D)
                    args.Add("-device virtio-vga-gl");
                    args.Add("-display sdl,gl=on");
                    break;
                case 2: // 标准 VGA (兼容)
                    args.Add("-vga std");
                    args.Add("-display sdl");
                    break;
                case 3: // 无显示 (headless)
                    args.Add("-display none");
                    break;
                default: // 0: virtio-gpu (Venus 3D)，推荐默认
                    int hostmem = vm.HostMemGb >= 1 ? vm.HostMemGb : 4;
                    args.Add("-device virtio-vga-gl,blob=on,hostmem=" + hostmem + "G,venus=on");
                    args.Add("-display sdl,gl=on");
                    break;
            }

            switch (vm.Sound)
            {
                case 0: args.Add("-device virtio-sound-pci"); break;
                case 1: args.Add("-device intel-hda"); args.Add("-device hda-duplex"); break;
                case 2: args.Add("-device AC97"); break;
            }

            string netdev = "";
            switch (vm.Network)
            {
                case 0: netdev = "virtio-net-pci"; break;
                case 1: netdev = "e1000"; break;
            }
            if (netdev.Length > 0)
            {
                args.Add("-device " + netdev + ",netdev=net0,bootindex=" + NetBi());
                args.Add("-netdev user,id=net0" + BuildPortForwards(vm));
            }

            args.Add("-usb");
            args.Add("-device usb-tablet");

            int fsIdx = 0;
            foreach (var f in vm.SharedFolders)
            {
                if (string.IsNullOrWhiteSpace(f.HostPath) || string.IsNullOrWhiteSpace(f.Tag)) continue;
                string sec = string.IsNullOrWhiteSpace(f.SecurityModel) ? "mapped-xattr" : f.SecurityModel;
                string id = "fsdev" + fsIdx;
                args.Add("-fsdev local,id=" + id + ",path=\"" + f.HostPath + "\",security_model=" + sec);
                args.Add("-device virtio-9p-pci,fsdev=" + id + ",mount_tag=" + f.Tag);
                fsIdx++;
            }

            // 除"标准 VGA"模式已显式指定外，抑制内建 VGA
            if (vm.GpuMode != 2)
                args.Add("-vga none");

            return args;
        }

        public Process Launch(VmConfig vm)
        {
            EnsureEfivarsFile(vm);
            var args = BuildArgs(vm);
            var psi = new ProcessStartInfo
            {
                FileName = QemuExeW,
                Arguments = string.Join(" ", args),
                UseShellExecute = false,
                WorkingDirectory = BinDir,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            if (vm.UseVaapi) psi.EnvironmentVariables["WINQ_VAAPI"] = "1";

            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.ErrorDataReceived += (s, e) => { if (e.Data != null) Log?.Invoke(e.Data); };
            proc.OutputDataReceived += (s, e) => { if (e.Data != null) Log?.Invoke(e.Data); };
            proc.Start();
            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();
            return proc;
        }
    }
}
