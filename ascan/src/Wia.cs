using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Ascan
{
    sealed class ScannerInfo
    {
        public readonly string Id;
        public readonly string Name;
        public ScannerInfo(string id, string name) { Id = id; Name = name; }
    }

    sealed class WiaUnavailableException : Exception
    {
        public WiaUnavailableException(string message) : base(message) { }
    }

    /// <summary>Windows Image Acquisition via the WIA Automation COM library (wiaaut.dll), late bound.</summary>
    static class Wia
    {
        const int ScannerDeviceType = 1;
        const int PropIntent = 6146;
        const int PropXResolution = 6147;
        const int PropYResolution = 6148;

        static dynamic Create(string progId)
        {
            var type = Type.GetTypeFromProgID(progId);
            if (type == null) throw new WiaUnavailableException("služba WIA (Získávání obrazu systému Windows) není v systému dostupná.");
            return Activator.CreateInstance(type);
        }

        public static List<ScannerInfo> ListScanners()
        {
            var result = new List<ScannerInfo>();
            dynamic manager = Create("WIA.DeviceManager");
            foreach (dynamic info in manager.DeviceInfos)
            {
                if ((int)info.Type != ScannerDeviceType) continue;
                string id = (string)info.DeviceID;
                string name = PropertyString(info.Properties, "Name");
                result.Add(new ScannerInfo(id, string.IsNullOrEmpty(name) ? id : name));
            }
            return result;
        }

        /// <summary>Shows the Windows scanner selection dialog; returns null when cancelled.</summary>
        public static ScannerInfo SelectWithWindowsDialog()
        {
            dynamic dialog = Create("WIA.CommonDialog");
            dynamic device = dialog.ShowSelectDevice(ScannerDeviceType, true, false);
            if (device == null) return null;
            string id = (string)device.DeviceID;
            string name = PropertyString(device.Properties, "Name");
            return new ScannerInfo(id, string.IsNullOrEmpty(name) ? id : name);
        }

        public static void Scan(string deviceId, string outputPath, string formatId, int dpi, ColorMode color, Action<string> warn)
        {
            dynamic manager = Create("WIA.DeviceManager");
            dynamic info = null;
            foreach (dynamic d in manager.DeviceInfos)
                if (string.Equals((string)d.DeviceID, deviceId, StringComparison.OrdinalIgnoreCase)) { info = d; break; }
            if (info == null) throw new COMException("Skener nenalezen", unchecked((int)0x80210015));

            dynamic device = info.Connect();
            dynamic item = null;
            foreach (dynamic i in device.Items) { item = i; break; }
            if (item == null) throw new COMException("Skener nemá žádnou položku ke skenování", unchecked((int)0x80210001));

            if (!TrySetProperty(item.Properties, PropIntent, (int)color))
                warn("skener nepodporuje režim \"" + Cli.Describe(color) + "\", použije se jeho výchozí nastavení.");
            if (!TrySetProperty(item.Properties, PropXResolution, dpi) | !TrySetProperty(item.Properties, PropYResolution, dpi))
                warn("skener nepodporuje rozlišení " + dpi + " DPI, použije se jeho výchozí nastavení.");

            dynamic image = item.Transfer(ImageFormats.Bmp);
            if (!string.Equals((string)image.FormatID, formatId, StringComparison.OrdinalIgnoreCase))
                image = Convert(image, formatId);

            if (File.Exists(outputPath)) File.Delete(outputPath);
            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            image.SaveFile(outputPath);
        }

        static dynamic Convert(dynamic image, string formatId)
        {
            dynamic process = Create("WIA.ImageProcess");
            foreach (dynamic f in process.FilterInfos)
                if ((string)f.Name == "Convert") { process.Filters.Add(f.FilterID); break; }
            foreach (dynamic filter in process.Filters)
            {
                foreach (dynamic p in filter.Properties)
                {
                    if ((string)p.Name == "FormatID") p.Value = formatId;
                    else if ((string)p.Name == "Quality") p.Value = 90;
                }
            }
            return process.Apply(image);
        }

        static bool TrySetProperty(dynamic properties, int id, object value)
        {
            try
            {
                foreach (dynamic p in properties)
                {
                    if ((int)p.PropertyID != id) continue;
                    p.Value = value;
                    return true;
                }
            }
            catch (COMException) { }
            catch (ArgumentException) { }
            return false;
        }

        static string PropertyString(dynamic properties, string name)
        {
            foreach (dynamic p in properties)
                if ((string)p.Name == name) return System.Convert.ToString(p.Value);
            return null;
        }
    }
}
