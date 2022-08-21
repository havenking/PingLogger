using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace InternetLogger
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private static string LOGFILENAME = "InternetLogger";
        private static string LOGFILEEXTENSION = ".log";
        private static string START = "Start";
        private static string STOP = "Stop";

        private readonly ObservableCollection<string> textLog;

        private bool isRunning;
        private bool autoScrollEnabled;
        private bool configEnabled;
        private StreamWriter logfileStream;

        private string startButtonText;
        private long maxLatency;
        private long minLatency;
        private float avgLatency;
        private int totalRequests;
        private int successRequests;
        private int timedOutRequests;
        private int errorRequests;
        private float successRate;

        private DateTime currentday;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = this;

            isRunning = false;
            autoScrollEnabled = true;

            textLog = new ObservableCollection<string>();
            startButtonText = START;
            currentday = DateTime.Today;


            StartButtonCommand = new DelegateCommand(Run);

            CloseButtonCommand = new DelegateCommand(Close);

            ScrollBottomCommand = new DelegateCommand(ScrollToBottom);

            Closing += OnWindowClosing;

            string path = Directory.GetCurrentDirectory() + "/" + DateTime.Now.ToString("yyyy-MM-dd") + LOGFILENAME + LOGFILEEXTENSION;

            if (File.Exists(path))
            {
                using (StreamReader reader = File.OpenText(path))
                {
                    string? line = reader.ReadLine();

                    while (line != null)
                    {
                        TextLog.Add(line);

                        TotalRequests += 1;

                        string[] splits = line.Split(" - ");

                        if (splits.Length == 3)
                        {
                            int latency = int.Parse(splits[2].Where(Char.IsDigit).ToArray());

                            if (latency > MaxLatency)
                                MaxLatency = latency;
                            else if (latency < MinLatency || MinLatency == 0)
                                MinLatency = latency;

                            AvgLatency = (TotalRequests * AvgLatency + latency) / (TotalRequests + 1);
                        }

                        if (line.Contains("Reply"))
                            SuccessRequests += 1;
                        else if (line.Contains("Request"))
                            TimedOutRequests += 1;
                        else
                            ErrorRequests += 1;

                        line = reader.ReadLine();
                    }
                }
            }

            if (TotalRequests != 0)
                SuccessRate = SuccessRequests * 100f / TotalRequests;

            logfileStream = File.AppendText(path);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ICommand StartButtonCommand { get; }

        public ICommand CloseButtonCommand { get; }

        public ICommand ScrollBottomCommand { get; }


        public bool AutoScrollEnabled
        {
            get
            {
                return autoScrollEnabled;
            }

            set
            {
                if (autoScrollEnabled != value)
                {
                    autoScrollEnabled = value;

                    OnPropertyChanged();
                }
            }
        }

        public bool ConfigEnabled
        {
            get
            {
                return configEnabled;
            }

            set
            {
                if (configEnabled != value)
                {
                    configEnabled = value;

                    OnPropertyChanged();
                }
            }
        }

        public string StartButtonText
        { 
            get
            { 
                return startButtonText;
            }

            set
            {
                if (startButtonText != value)
                {
                    startButtonText = value;

                    OnPropertyChanged();
                }
            }
        }

        public long MaxLatency
        {
            get
            {
                return maxLatency;
            }

            set
            {
                if (maxLatency != value)
                {
                    maxLatency = value;

                    OnPropertyChanged();
                }
            }
        }

        public long MinLatency
        {
            get
            {
                return minLatency;
            }

            set
            {
                if (minLatency != value)
                {
                    minLatency = value;

                    OnPropertyChanged();
                }
            }
        }

        public float AvgLatency
        {
            get
            {
                return avgLatency;
            }

            set
            {
                if (avgLatency != value)
                {
                    avgLatency = value;

                    OnPropertyChanged();
                }
            }
        }

        public int TotalRequests
        {
            get
            {
                return totalRequests;
            }

            set
            {
                if (totalRequests != value)
                {
                    totalRequests = value;

                    OnPropertyChanged();
                }
            }
        }

        public int SuccessRequests
        {
            get
            {
                return successRequests;
            }

            set
            {
                if (successRequests != value)
                {
                    successRequests = value;

                    OnPropertyChanged();
                }
            }
        }

        public int TimedOutRequests
        {
            get
            {
                return timedOutRequests;
            }

            set
            {
                if (timedOutRequests != value)
                {
                    timedOutRequests = value;

                    OnPropertyChanged();
                }
            }
        }

        public int ErrorRequests
        {
            get
            {
                return errorRequests;
            }

            set
            {
                if (errorRequests != value)
                {
                    errorRequests = value;

                    OnPropertyChanged();
                }
            }
        }

        public float SuccessRate
        {
            get
            {
                return successRate;
            }

            set
            {
                if (successRate != value)
                {
                    successRate = value;

                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<string> TextLog
        {
            get { return textLog; }
        }

        private void Run()
        {
            isRunning = !isRunning;

            if(isRunning)
            {
                StartButtonText = STOP;

                PingLoop();
            }
            else
            {
                StartButtonText = START;
            }
        }

        private async void PingLoop()
        {
            try
            {
                while (isRunning)
                {
                    await Task.Delay(1000);//Espera 1 segundo

                    if (!isRunning)
                        return;

                    PingRequest();
                }

                return;
            }
            catch(Exception)
            {
                Close();
                return;
            }
        }

        private async void PingRequest()
        {
            Ping myPing = new Ping();
            String host = "8.8.8.8";
            byte[] buffer = new byte[32];
            int timeout = 2000;
            PingOptions pingOptions = new PingOptions();
            string log;
            PingReply reply = myPing.Send(host, timeout, buffer, pingOptions);

            TotalRequests += 1;

            if (reply.Status == IPStatus.Success)
            {
                log = string.Format("{0} - Reply from {1} - time={2}ms ", DateTime.Now.ToString("HH:mm:ss"), reply.Address.ToString(), reply.RoundtripTime.ToString());

                SuccessRequests += 1;

                if (reply.RoundtripTime > MaxLatency)
                    MaxLatency = reply.RoundtripTime;
                else if (reply.RoundtripTime < MinLatency || MinLatency == 0)
                    MinLatency = reply.RoundtripTime;

                AvgLatency = (TotalRequests * AvgLatency + reply.RoundtripTime) / (TotalRequests + 1);
            }
            else if (reply.Status == IPStatus.TimedOut)
            {
                log = string.Format("{0} - Request time out.", DateTime.Now.ToString("HH:mm:ss"));

                TimedOutRequests += 1;
            }
            else
            {
                log = string.Format("{0} - {1} Error", DateTime.Now.ToString("HH:mm:ss"), reply.Status.ToString());

                ErrorRequests += 1;
            }

            SuccessRate = SuccessRequests * 100f / TotalRequests;

            if (currentday != DateTime.Today)
            {
                string path = Directory.GetCurrentDirectory() + "/" + DateTime.Now.ToString("yyyy-MM-dd") + LOGFILENAME + LOGFILEEXTENSION;
                logfileStream = File.AppendText(path);
            }

            logfileStream.WriteLine(log);
            TextLog.Add(log);

            if (AutoScrollEnabled)
                ScrollToBottom();
        }
        
        private void ScrollToBottom()
        {
            AutoScrollToCurrentItem(listBoxTextLog, listBoxTextLog.Items.Count);
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private static void AutoScrollToCurrentItem(ListBox listBox, int index)
        {
            UIElement container = null;

            for (int i = index; i > 0; i--)
            {
                container = listBox.ItemContainerGenerator.ContainerFromIndex(i) as UIElement;
                if (container != null)
                {
                    break;
                }
            }

            if (container == null)
                return;

            // Find the ScrollContentPresenter
            ScrollContentPresenter presenter = null;

            for (Visual vis = container; vis != null && vis != listBox; vis = VisualTreeHelper.GetParent(vis) as Visual)
                if ((presenter = vis as ScrollContentPresenter) != null)
                    break;

            if (presenter == null)
                return;

            // Find the IScrollInfo
            var scrollInfo =
                !presenter.CanContentScroll ? presenter :
                presenter.Content as IScrollInfo ??
                FirstVisualChild(presenter.Content as ItemsPresenter) as IScrollInfo ??
                presenter;

            // Find the amount of items that is "Visible" in the ListBox
            var height = (container as ListBoxItem).ActualHeight;
            var lbHeight = listBox.ActualHeight;
            var showCount = (int)(lbHeight / height) - 1;

            //Set the scrollbar
            if (scrollInfo.CanVerticallyScroll)
                scrollInfo.SetVerticalOffset(index - showCount);
        }

        private static DependencyObject FirstVisualChild(Visual visual)
        {
            if (visual == null)
                return null;

            if (VisualTreeHelper.GetChildrenCount(visual) == 0)
                return null;

            return VisualTreeHelper.GetChild(visual, 0);
        }

        private void OnWindowClosing(object? sender, CancelEventArgs e)
        {
            logfileStream?.Close();
        }
    }
}
