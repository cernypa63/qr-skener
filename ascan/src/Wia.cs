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

            bool pdf = formatId == ImageFormats.Pdf;
            bool feeder = pdf && UseFeederIfLoaded(device);

            if (!TrySetProperty(item.Properties, PropIntent, (int)color))
                warn("skener nepodporuje režim \"" + Cli.Describe(color) + "\", použije se jeho výchozí nastavení.");
            if (!TrySetProperty(item.Properties, PropXResolution, dpi) | !TrySetProperty(item.Properties, PropYResolution, dpi))
                warn("skener nepodporuje rozlišení " + dpi + " DPI, použije se jeho výchozí nastavení.");

            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(outputPath)) File.Delete(outputPath);

            if (!pdf)
            {
                dynamic image = ToFormat(item.Transfer(ImageFormats.Bmp), formatId);
                image.SaveFile(outputPath);
                return;
            }

            var pages = new List<byte[]>();
            double resX = 0, resY = 0;
            while (true)
            {
                dynamic image;
                try
                {
                    image = ToFormat(item.Transfer(ImageFormats.Bmp), ImageFormats.Jpeg);
                }
                catch (COMException) when (pages.Count > 0)
                {
                    break; // feeder empty
                }
                if (pages.Count == 0)
                {
                    resX = System.Convert.ToDouble(image.HorizontalResolution);
                    resY = System.Convert.ToDouble(image.VerticalResolution);
                }
                pages.Add((byte[])image.FileData.BinaryData);
                if (!feeder) break;
            }
            File.WriteAllBytes(outputPath, Pdf.FromJpegPages(pages, resX > 0 ? resX : dpi, resY > 0 ? resY : dpi));
        }

        /// <summary>Switches the device to its document feeder when paper is loaded there.</summary>
        static bool UseFeederIfLoaded(dynamic device)
        {
            const int PropHandlingCapabilities = 3086, PropHandlingStatus = 3087, PropHandlingSelect = 3088;
            const int Feeder = 1, FeedReady = 1;
            try
            {
                int capabilities = PropertyInt(device.Properties, PropHandlingCapabilities);
                int status = PropertyInt(device.Properties, PropHandlingStatus);
                if ((capabilities & Feeder) == 0 || (status & FeedReady) == 0) return false;
                return TrySetProperty(device.Properties, PropHandlingSelect, Feeder);
            }
            catch (COMException) { return false; }
        }

        static dynamic ToFormat(dynamic image, string formatId) =>
            string.Equals((string)image.FormatID, formatId, StringComparison.OrdinalIgnoreCase) ? image : Convert(image, formatId);

        static int PropertyInt(dynamic properties, int id)
        {
            foreach (dynamic p in properties)
                if ((int)p.PropertyID == id) return System.Convert.ToInt32(p.Value);
            return 0;
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
