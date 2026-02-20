using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;

namespace WpfApp1
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly HardwareInfoService _service = new HardwareInfoService();

        private CpuInfo _cpu = new CpuInfo();
        public CpuInfo Cpu { get => _cpu; set { _cpu = value; OnPropertyChanged(); } }

        private MemoryInfo _memory = new MemoryInfo();
        public MemoryInfo Memory { get => _memory; set { _memory = value; OnPropertyChanged(); } }

        private DiskInfo _disks = new DiskInfo();
        public DiskInfo Disks { get => _disks; set { _disks = value; OnPropertyChanged(); } }

        private VideoControllerInfo _gpu = new VideoControllerInfo();
        public VideoControllerInfo Gpu { get => _gpu; set { _gpu = value; OnPropertyChanged(); } }

        private List<NetworkAdapterInfo> _networkAdapters = new List<NetworkAdapterInfo>();
        public List<NetworkAdapterInfo> NetworkAdapters { get => _networkAdapters; set { _networkAdapters = value; OnPropertyChanged(); } }

        private SystemInfo _systemInfo = new SystemInfo();
        public SystemInfo SystemInfo { get => _systemInfo; set { _systemInfo = value; OnPropertyChanged(); } }

        private string _status = "Готов к работе. Запустите от имени администратора";
        public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }

        private bool _autoUpdateEnabled = true;
        public bool AutoUpdateEnabled
        {
            get => _autoUpdateEnabled;
            set
            {
                _autoUpdateEnabled = value;
                OnPropertyChanged();
                UpdateTimerInterval();
            }
        }

        private string _updateIntervalSeconds = "5";
        public string UpdateIntervalSeconds
        {
            get => _updateIntervalSeconds;
            set
            {
                _updateIntervalSeconds = value;
                OnPropertyChanged();
                UpdateTimerInterval();
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand ExportTxtCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand ExportJsonCommand { get; }

        private System.Windows.Threading.DispatcherTimer _timer;

        public MainViewModel()
        {
            RefreshCommand = new RelayCommand(async _ => await RefreshAllAsync());
            ExportTxtCommand = new RelayCommand(_ => ExportToTxt());
            ExportCsvCommand = new RelayCommand(_ => ExportToCsv());
            ExportJsonCommand = new RelayCommand(_ => ExportToJson());

            _timer = new System.Windows.Threading.DispatcherTimer();
            _timer.Tick += async (s, e) =>
            {
                if (AutoUpdateEnabled)
                    await RefreshAllAsync();
            };

            UpdateTimerInterval(); // начальный интервал
            _timer.Start();

            _ = RefreshAllAsync();
        }

        private void UpdateTimerInterval()
        {
            if (int.TryParse(UpdateIntervalSeconds, out int seconds) && seconds >= 1)
            {
                _timer.Interval = TimeSpan.FromSeconds(seconds);
            }
            else
            {
                UpdateIntervalSeconds = "5";
                _timer.Interval = TimeSpan.FromSeconds(5);
            }
        }

        private async Task RefreshAllAsync()
        {
            Status = "Обновление данных...";

            try
            {
                Cpu = await Task.Run(_service.GetCpuInfo);
                Memory = await Task.Run(_service.GetMemoryInfo);
                Disks = await Task.Run(_service.GetDiskInfo);
                Gpu = await Task.Run(_service.GetGpuInfo);
                NetworkAdapters = await Task.Run(_service.GetNetworkAdapters);
                SystemInfo = await Task.Run(_service.GetSystemInfo);

                Status = $"Обновлено {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                Status = "Ошибка: " + ex.Message;
            }
        }

        private void ExportToTxt()
        {
            SaveFile("txt", "Текстовые файлы (*.txt)|*.txt", GenerateTxtReport());
        }

        private void ExportToCsv()
        {
            SaveFile("csv", "CSV файлы (*.csv)|*.csv", GenerateCsvReport());
        }

        private void ExportToJson()
        {
            SaveFile("json", "JSON файлы (*.json)|*.json", GenerateJsonReport());
        }

        private void SaveFile(string ext, string filter, string content)
        {
            var dlg = new SaveFileDialog
            {
                Filter = filter,
                FileName = $"HardwareReport_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}"
            };

            if (dlg.ShowDialog() == true)
            {
                File.WriteAllText(dlg.FileName, content);
                Status = $"Сохранено: {System.IO.Path.GetFileName(dlg.FileName)}";
            }
        }

        private string GenerateTxtReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("ОТЧЁТ ПО ОБОРУДОВАНИЮ");
            sb.AppendLine($"Дата: {DateTime.Now}");
            sb.AppendLine();
            sb.AppendLine("Процессор:");
            sb.AppendLine($"  {Cpu.Name}");
            sb.AppendLine($"  Ядер: {Cpu.NumberOfCores}");
            sb.AppendLine($"  Потоков: {Cpu.NumberOfLogicalProcessors}");
            sb.AppendLine($"  Частота: {Cpu.MaxClockSpeed} МГц");
            sb.AppendLine($"  Загрузка: {Cpu.LoadPercent:F1}%");
            sb.AppendLine();
            sb.AppendLine("Память:");
            sb.AppendLine($"  Всего: {Memory.TotalVisibleMemoryMB:N0} МБ");
            sb.AppendLine($"  Свободно: {Memory.FreePhysicalMemoryMB:N0} МБ");
            sb.AppendLine($"  Использовано: {Memory.UsedMemoryMB:N0} МБ");
            sb.AppendLine($"  Загрузка: {Memory.UsagePercent:F1}%");
            sb.AppendLine("Модули:");
            foreach (var m in Memory.Modules) sb.AppendLine($"  • {m}");
            sb.AppendLine();
            sb.AppendLine("Диски:");
            foreach (var d in Disks.LogicalDisksInfo) sb.AppendLine("  " + d);
            sb.AppendLine();
            sb.AppendLine("Графический процессор:");
            sb.AppendLine($"  {Gpu.Name}");
            sb.AppendLine($"  Видеопамять: {Gpu.AdapterRAMGB}");
            sb.AppendLine($"  Разрешение: {Gpu.Resolution}");
            sb.AppendLine();
            sb.AppendLine("Сетевые адаптеры:");
            foreach (var a in NetworkAdapters)
            {
                sb.AppendLine($"  {a.Name}");
                sb.AppendLine($"    MAC: {a.MACAddress}");
                sb.AppendLine($"    Статус: {a.NetConnectionStatus}");
                sb.AppendLine($"    Скорость: {a.SpeedMbps}");
            }
            sb.AppendLine();
            sb.AppendLine("Система:");
            sb.AppendLine($"  ОС: {SystemInfo.OSName}");
            sb.AppendLine($"  Версия ОС: {SystemInfo.OSVersion}");
            sb.AppendLine($"  Архитектура: {SystemInfo.OSArchitecture}");
            sb.AppendLine($"  Компьютер: {SystemInfo.ComputerName}");
            sb.AppendLine($"  Пользователь: {SystemInfo.CurrentUser}");

            return sb.ToString();
        }

        private string GenerateCsvReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Категория,Параметр,Значение");

            sb.AppendLine($"Процессор,Модель,\"{CsvEscape(Cpu.Name)}\"");
            sb.AppendLine($"Процессор,Ядер,{Cpu.NumberOfCores}");
            sb.AppendLine($"Процессор,Потоков,{Cpu.NumberOfLogicalProcessors}");
            sb.AppendLine($"Процессор,Частота,{Cpu.MaxClockSpeed}");
            sb.AppendLine($"Процессор,Загрузка,{Cpu.LoadPercent:F1}%");

            sb.AppendLine($"Память,Всего,{Memory.TotalVisibleMemoryMB:N0} МБ");
            sb.AppendLine($"Память,Свободно,{Memory.FreePhysicalMemoryMB:N0} МБ");
            sb.AppendLine($"Память,Использовано,{Memory.UsedMemoryMB:N0} МБ");
            sb.AppendLine($"Память,Загрузка,{Memory.UsagePercent:F1}%");

            int idx = 1;
            foreach (var m in Memory.Modules)
                sb.AppendLine($"Память,Модуль {idx++},\"{CsvEscape(m)}\"");

            idx = 1;
            foreach (var d in Disks.LogicalDisksInfo)
                sb.AppendLine($"Диски,Диск {idx++},\"{CsvEscape(d)}\"");

            sb.AppendLine($"Графический процессор,Модель,\"{CsvEscape(Gpu.Name)}\"");
            sb.AppendLine($"Графический процессор,Видеопамять,\"{Gpu.AdapterRAMGB}\"");
            sb.AppendLine($"Графический процессор,Разрешение,\"{CsvEscape(Gpu.Resolution)}\"");

            idx = 1;
            foreach (var a in NetworkAdapters)
            {
                sb.AppendLine($"Сеть {idx},Адаптер,\"{CsvEscape(a.Name)}\"");
                sb.AppendLine($"Сеть {idx},MAC,\"{CsvEscape(a.MACAddress)}\"");
                sb.AppendLine($"Сеть {idx},Статус,\"{CsvEscape(a.NetConnectionStatus)}\"");
                sb.AppendLine($"Сеть {idx},Скорость,\"{CsvEscape(a.SpeedMbps)}\"");
                idx++;
            }

            sb.AppendLine($"Система,ОС,\"{CsvEscape(SystemInfo.OSName)}\"");
            sb.AppendLine($"Система,Версия,\"{CsvEscape(SystemInfo.OSVersion)}\"");
            sb.AppendLine($"Система,Архитектура,\"{CsvEscape(SystemInfo.OSArchitecture)}\"");
            sb.AppendLine($"Система,Компьютер,\"{CsvEscape(SystemInfo.ComputerName)}\"");
            sb.AppendLine($"Система,Пользователь,\"{CsvEscape(SystemInfo.CurrentUser)}\"");

            return sb.ToString();
        }

        private string GenerateJsonReport()
        {
            var report = new
            {
                Timestamp = DateTime.Now,
                ComputerName = SystemInfo.ComputerName,
                Cpu,
                Memory,
                Disks = Disks.LogicalDisksInfo,
                Gpu,
                Network = NetworkAdapters,
                System = SystemInfo
            };

            return JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        }

        private static string CsvEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
                return $"\"{s.Replace("\"", "\"\"")}\"";
            return s;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object parameter) => _execute(parameter);
    }
}