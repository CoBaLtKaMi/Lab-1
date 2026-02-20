namespace WpfApp1
{
    public class CpuInfo
    {
        public string Name { get; set; } = "—";
        public int NumberOfCores { get; set; }
        public int NumberOfLogicalProcessors { get; set; }
        public int MaxClockSpeed { get; set; }
        public double LoadPercent { get; set; }
    }
}