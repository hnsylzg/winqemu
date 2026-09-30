using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace WinqEmuLauncher
{
    public partial class App : Application
    {
        public App()
        {
            Lang.Init();
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            // 非 UI 线程（进程退出回调、输出回调、线程池）的未处理异常会导致进程直接终止，
            // DispatcherUnhandledException 抓不到，故这里落盘崩溃日志以便定位。
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        // UI 线程未处理异常：弹窗告知并继续运行，同时落盘。
        void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            WriteCrashLog("DispatcherUnhandledException (UI thread)", e.Exception);
            try
            {
                MessageBox.Show(e.Exception?.ToString(), Lang.T("AppErrorTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
            e.Handled = true;
        }

        // 非 UI 线程致命异常：进程将终止，尽量落盘 + 提示。
        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            WriteCrashLog("AppDomain.UnhandledException (non-UI, IsTerminating=" + e.IsTerminating + ")",
                e.ExceptionObject as Exception);
            try
            {
                var ex = e.ExceptionObject as Exception;
                MessageBox.Show((ex?.ToString() ?? "Unknown fatal error") +
                    "\n\n崩溃日志已写入：" + CrashLogPath, Lang.T("AppErrorTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
        }

        void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            WriteCrashLog("TaskScheduler.UnobservedTaskException", e.Exception);
            try { e.SetObserved(); } catch { }
        }

        // 崩溃日志统一放在 config/ 目录（与配置集中管理）
        static string CrashLogPath => Path.Combine(AppPaths.BaseDir, "config", "crash.log");

        // 把异常堆栈追加写入 config/crash.log（自身必须永不抛异常）。
        static void WriteCrashLog(string header, Exception ex)
        {
            try
            {
                var path = CrashLogPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var sb = new StringBuilder();
                sb.AppendLine("==== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ====");
                sb.AppendLine("Type: " + header);
                sb.AppendLine(ex == null ? "(no exception object)" : ex.ToString());
                sb.AppendLine();
                File.AppendAllText(path, sb.ToString());
            }
            catch { }
        }
    }
}
