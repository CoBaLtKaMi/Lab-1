using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading;

namespace WpfApp1
{
    public class HardwareInfoService
    {
        public CpuInfo GetCpuInfo()
        {
            var cpu = new CpuInfo { Name = "—" };
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor"))
                {
                    var obj = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                    if (obj != null)
                    {
                        cpu.Name = obj["Name"]?.ToString() ?? "—";
                        cpu.NumberOfCores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                        cpu.NumberOfLogicalProcessors = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0);
                        cpu.MaxClockSpeed = Convert.ToInt32(obj["MaxClockSpeed"] ?? 0);
                    }
                }

                double load1 = GetCpuLoad();
                Thread.Sleep(1200);
                double load2 = GetCpuLoad();
                cpu.LoadPercent = load2 > 0 ? load2 : load1;
            }
            catch { }
            return cpu;
        }

        private double GetCpuLoad()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'"))
                {
                    var obj = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                    if (obj != null)
                        return Convert.ToDouble(obj["PercentProcessorTime"] ?? 0);
                }
            }
            catch { }
            return 0;
        }

        public MemoryInfo GetMemoryInfo()
        {
            var mem = new MemoryInfo();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem"))
                {
                    var obj = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                    if (obj != null)
                    {
                        mem.TotalVisibleMemoryKB = Convert.ToInt64(obj["TotalVisibleMemorySize"] ?? 0);
                        mem.FreePhysicalMemoryKB = Convert.ToInt64(obj["FreePhysicalMemory"] ?? 0);
                    }
                }

                using (var searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        long cap = Convert.ToInt64(obj["Capacity"] ?? 0);
                        if (cap > 0) mem.Modules.Add($"{cap / 1073741824:N1} ГБ");
                    }
                }
            }
            catch { }
            return mem;
        }

        public DiskInfo GetDiskInfo()
        {
            var disks = new DiskInfo();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT DeviceID, Size, FreeSpace FROM Win32_LogicalDisk WHERE DriveType=3"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        long size = Convert.ToInt64(obj["Size"] ?? 0);
                        long free = Convert.ToInt64(obj["FreeSpace"] ?? 0);
                        string line = $"{obj["DeviceID"],-4}  {size / 1073741824,6:N1} ГБ  свободно {free / 1073741824,6:N1} ГБ";
                        disks.LogicalDisksInfo.Add(line);
                    }
                }
            }
            catch { }
            return disks;
        }

        public VideoControllerInfo GetGpuInfo()
        {
            var info = new VideoControllerInfo { Name = "—", AdapterRAMBytes = 0, Resolution = "—", LoadPercent = 0 };

            try
            {
                long maxRam = 0;
                string bestName = "—";

                using (var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = obj["Name"]?.ToString() ?? "—";
                        long ram = Convert.ToInt64(obj["AdapterRAM"] ?? 0);

                        if (ram > maxRam)
                        {
                            maxRam = ram;
                            bestName = name;
                        }
                    }
                }

                info.Name = bestName;
                info.AdapterRAMBytes = maxRam;

                using (var searcher = new ManagementObjectSearcher("SELECT CurrentHorizontalResolution, CurrentVerticalResolution FROM Win32_VideoController"))
                {
                    var obj = searcher.Get().Cast<ManagementObject>()
                        .FirstOrDefault(o => Convert.ToInt64(o["CurrentHorizontalResolution"] ?? 0) > 0);

                    if (obj != null)
                    {
                        long w = Convert.ToInt64(obj["CurrentHorizontalResolution"] ?? 0);
                        long h = Convert.ToInt64(obj["CurrentVerticalResolution"] ?? 0);
                        if (w > 0 && h > 0)
                            info.Resolution = $"{w} × {h}";
                    }
                }

                info.LoadPercent = GetGpuLoadFast();
            }
            catch (Exception ex)
            {
                info.Name = "Ошибка: " + ex.Message;
            }

            return info;
        }

        private double GetGpuLoadFast()
        {
            try
            {
                var category = new PerformanceCounterCategory("GPU Engine");
                var instances = category.GetInstanceNames()
                    .Where(n => n.Contains("engtype_3D") || n.Contains("3D") || n.Contains("CUDA") || n.Contains("Graphics"))
                    .Take(5) // ограничиваем 5 штуками — чтобы не ждать минуты
                    .ToArray();

                if (instances.Length == 0) return 0;

                double maxLoad = 0;

                foreach (var instance in instances)
                {
                    try
                    {
                        using (var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance, true))
                        {
                            counter.NextValue(); // первый вызов — игнорируем
                            Thread.Sleep(1500); // 1.5 секунды — оптимально для точности
                            double value = counter.NextValue();

                            if (value > maxLoad)
                                maxLoad = value;
                        }
                    }
                    catch { /* пропускаем ошибочный инстанс */ }
                }

                return Math.Min(100.0, maxLoad);
            }
            catch
            {
                return 0;
            }
        }

        public List<NetworkAdapterInfo> GetNetworkAdapters()
        {
            var list = new List<NetworkAdapterInfo>();
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Name, MACAddress, NetConnectionStatus, Speed FROM Win32_NetworkAdapter WHERE PhysicalAdapter = true"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        list.Add(new NetworkAdapterInfo
                        {
                            Name = obj["Name"]?.ToString() ?? "—",
                            MACAddress = obj["MACAddress"]?.ToString() ?? "—",
                            NetConnectionStatus = GetStatus(Convert.ToInt32(obj["NetConnectionStatus"] ?? -1)),
                            SpeedMbps = FormatSpeed(Convert.ToInt64(obj["Speed"] ?? 0))
                        });
                    }
                }
            }
            catch { }
            return list;
        }

        private string GetStatus(int code)
        {
            return code == 2 ? "Подключено" : (code == 0 ? "Отключено" : "—");
        }

        private string FormatSpeed(long bps)
        {
            if (bps <= 0) return "—";
            return bps >= 1000000000 ? $"{bps / 1000000000:N1} Гбит/с" :
                   (bps >= 1000000 ? $"{bps / 1000000:N1} Мбит/с" : $"{bps / 1000:N1} Кбит/с");
        }

        public SystemInfo GetSystemInfo()
        {
            var info = new SystemInfo();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Caption, Version, OSArchitecture FROM Win32_OperatingSystem"))
                {
                    var obj = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                    if (obj != null)
                    {
                        info.OSName = obj["Caption"]?.ToString() ?? "—";
                        info.OSVersion = obj["Version"]?.ToString() ?? "—";
                        info.OSArchitecture = obj["OSArchitecture"]?.ToString() ?? "—";
                    }
                }
                info.ComputerName = Environment.MachineName;
                info.CurrentUser = Environment.UserName;
            }
            catch { }
            return info;
        }
    }
}