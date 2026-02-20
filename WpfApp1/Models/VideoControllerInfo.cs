namespace WpfApp1
{
    public class VideoControllerInfo
    {
        public string Name { get; set; } = "—";
        public long AdapterRAMBytes { get; set; }
        public string AdapterRAMGB => AdapterRAMBytes > 0 ? $"{AdapterRAMBytes / 1073741824:N1} ГБ" : "—";
        public string Resolution { get; set; } = "—";
        public double LoadPercent { get; set; } = 0;
    }
}