using System;
using System.Collections.Generic;

namespace WpfApp1
{
    public class MemoryInfo
    {
        public long TotalVisibleMemoryKB { get; set; }
        public long FreePhysicalMemoryKB { get; set; }

        public long TotalVisibleMemoryMB => TotalVisibleMemoryKB / 1024;
        public long FreePhysicalMemoryMB => FreePhysicalMemoryKB / 1024;
        public long UsedMemoryMB => TotalVisibleMemoryMB - FreePhysicalMemoryMB;

        public double UsagePercent
        {
            get
            {
                if (TotalVisibleMemoryKB <= 0) return 0;
                return Math.Round(100.0 * UsedMemoryMB / TotalVisibleMemoryMB, 1);
            }
        }

        public List<string> Modules { get; set; } = new List<string>();
    }
}