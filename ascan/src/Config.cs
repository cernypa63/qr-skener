using System;
using System.IO;
using System.Text;

namespace Ascan
{
    sealed class Config
    {
        readonly string path;

        public string DeviceId { get; private set; }
        public string DeviceName { get; private set; }

        public Config(string path)
        {
            this.path = path;
            if (!File.Exists(path)) return;
            foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                if (key == "DeviceId") DeviceId = value;
                else if (key == "DeviceName") DeviceName = value;
            }
        }

        public static string DefaultPath() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ascan", "ascan.cfg");

        public void Save(string deviceId, string deviceName)
        {
            DeviceId = deviceId;
            DeviceName = deviceName;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "DeviceId=" + deviceId + "\r\nDeviceName=" + deviceName + "\r\n", new UTF8Encoding(false));
        }
    }
}
