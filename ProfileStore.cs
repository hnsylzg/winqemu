using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace WinqEmuLauncher
{
    // 单一配置文件 config/config.json，集中托管：语言 + VM 列表 + 运行态 + 上次选中。
    // 旧的分散文件（lang.txt / vms/vms.json / vms/running.json / vms/last.json）在首次运行、
    // 且 config.json 尚不存在时，自动合并进 config.json（只读取、不删除旧文件）。
    public static class ProfileStore
    {
        const string ConfigDirName = "config";
        const string ConfigFileName = "config.json";
        static string ConfigDir => Path.Combine(AppPaths.BaseDir, ConfigDirName);
        public static string ConfigFile => Path.Combine(ConfigDir, ConfigFileName);

        // 内存中的完整配置（启动加载一次，运行时修改后整体落盘）
        public static AppConfig Data { get; private set; } = new AppConfig();
        static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            if (File.Exists(ConfigFile))
            {
                try
                {
                    var c = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigFile));
                    if (c != null) { Data = c; return; }
                }
                catch { }
            }
            Data = MigrateLegacy();
        }

        // 从旧分散文件合并（仅当 config.json 不存在时调用）
        static AppConfig MigrateLegacy()
        {
            var cfg = new AppConfig();
            var legacyDir = Path.Combine(AppPaths.BaseDir, "vms");
            try
            {
                var vms = Path.Combine(legacyDir, "vms.json");
                if (File.Exists(vms))
                {
                    var list = JsonSerializer.Deserialize<List<VmConfig>>(File.ReadAllText(vms));
                    if (list != null) cfg.Vms = new ObservableCollection<VmConfig>(list);
                }
                var run = Path.Combine(legacyDir, "running.json");
                if (File.Exists(run))
                {
                    var d = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(run));
                    if (d != null) cfg.Running = d;
                }
                var last = Path.Combine(legacyDir, "last.json");
                if (File.Exists(last))
                {
                    var s = JsonSerializer.Deserialize<string>(File.ReadAllText(last));
                    if (!string.IsNullOrEmpty(s)) cfg.LastSelected = s;
                }
            }
            catch { }
            try
            {
                var lang = Path.Combine(AppPaths.BaseDir, "lang.txt");
                if (File.Exists(lang) && string.IsNullOrEmpty(cfg.Language))
                    cfg.Language = File.ReadAllText(lang).Trim().ToLowerInvariant();
            }
            catch { }
            return cfg;
        }

        public static void Save()
        {
            Directory.CreateDirectory(ConfigDir);
            var opts = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(ConfigFile, JsonSerializer.Serialize(Data, opts));
        }

        // ---- 细粒度接口（供 MainWindow / Lang 调用，全部路由到单一 config.json）----
        public static ObservableCollection<VmConfig> LoadAll() { Load(); return Data.Vms; }
        public static void SaveAll() { Load(); Save(); }
        public static Dictionary<string, int> LoadRunning() { Load(); return Data.Running; }
        public static void SaveRunning(Dictionary<string, int> map) { Load(); Data.Running = map ?? new Dictionary<string, int>(); Save(); }
        public static string LoadLastSelected() { Load(); return Data.LastSelected ?? ""; }
        public static void SaveLastSelected(string name) { Load(); Data.LastSelected = name; Save(); }
        // 语言读写（供 Lang 使用）
        public static string LoadLanguage() { Load(); return Data.Language; }
        public static void SaveLanguage(string lang) { Load(); Data.Language = lang; Save(); }
    }
}
