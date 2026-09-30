# WINQEMU

WINQ-EMU 启动器，winq-emu-qemu 项目的 GUI 前端。用图形界面在本机配置并启动 QEMU 虚拟机。

## 功能

- 管理多台虚拟机（磁盘、ISO、启动顺序、BIOS / UEFI 引导）
- 网卡、端口转发、共享文件夹（virtio-9p）
- 显卡模式：virtio-gpu（Venus 3D）/ 2D / 标准 VGA / 无显示
- 声卡、加速方式（WHPX / TCG）
- 中英文界面，记忆窗口位置与语言
- 一键导出等价启动批处理

## 构建

需要 .NET 9 SDK 与 Windows。

    dotnet publish -c Release -p:PublishSingleFile=true -p:SelfContained=false -r win-x64

产物为单文件 `WINQEMU.exe`。QEMU 可执行文件（`qemu-system-x86_64w.exe` 等）放在同目录 `bin/`，或在设置里指定路径。

## 运行

直接运行 `WINQEMU.exe`。首次启动在同目录生成 `config/config.json`，保存窗口几何、语言与各虚拟机配置。
