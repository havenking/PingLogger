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

namespace PingLogger
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private static string LOGFILENAME = "PingLogger";
        private static string LOGFILEEXTENSION = ".log";
        private static string START = "Start";
        private static string STOP = "Stop";

        private readonly ObservableCollection<PingLog> textLog;

        private bool isRunning;
        private bool autoScrollEnabled;
        private bool configEnabled;
        private StreamWriter logfileStream;
        private Semaphore logSemaphore;
        private Queue<Task> taskQueue;
        private Semaphore taskListSemaphore;

        private string startButtonText;
        private long maxLatency;
        private long minLatency;
        private float avgLatency;
        private int totalRequests;
        private int successRequests;
        private int timedOutRequests;
        private int errorRequests;
        private float successRate;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = this;

            isRunning = false;
            autoScrollEnabled = true;
            startButtonText = START;

            textLog = new ObservableCollection<PingLog>();
            logSemaphore = new Semaphore(initialCount: 1, maximumCount: 1);
            taskListSemaphore = new Semaphore(initialCount: 1, maximumCount: 1);
            taskQueue = new Queue<Task>();

            StartButtonCommand = new DelegateCommand(Run);
            CloseButtonCommand = new DelegateCommand(Close);
            ScrollBottomCommand = new DelegateCommand(ScrollToBottom);

            Closing += OnWindowClosing;

            string path = Directory.GetCurrentDirectory() + "/" + DateTime.Now.ToString("yyyy-MM-dd") + "_" + LOGFILENAME + LOGFILEEXTENSION;

            if (File.Exists(path))
            {
                using (StreamReader reader = File.OpenText(path))
                {
                    string? line = reader.ReadLine();

                    while (line != null)
                    {
                        TextLog.Add(new PingLog(line));

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

                if (TotalRequests != 0)
                    SuccessRate = SuccessRequests * 100f / TotalRequests;
            }
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

        public ObservableCollection<PingLog> TextLog
        {
            get { return textLog; }
        }

        private void Run()
        {
            isRunning = !isRunning;

            if(isRunning)
            {
                StartButtonText = STOP;

                Task.Factory.StartNew(() => PingLoop());
            }
            else
            {
                StartButtonText = START;
            }
        }

        private async Task PingLoop()
        {
            try
            {
                while (isRunning)
                {
                    await Task.Delay(1000);

                    if (!isRunning)
                        return;

                    Task task = Task.Factory.StartNew(() => PingRequestAsync());

                    taskListSemaphore.WaitOne();
                    taskQueue.Enqueue(task);
                    taskListSemaphore.Release();
                }

                return;
            }
            catch(Exception)
            {
                Close();
                return;
            }
        }

        private Task PingRequestAsync()
        {
            DateTime pingTime = DateTime.Now;
            Ping ping = new Ping();
            Task<PingReply> replyTask = ping.SendPingAsync("8.8.8.8", 1000, new byte[32], new PingOptions());

            logSemaphore.WaitOne();

            replyTask.Wait();

            if (replyTask.IsCompleted)
            {
                PingReply reply = replyTask.Result;

                Application.Current.Dispatcher.Invoke(() => UpdateLog(pingTime, reply));
            }

            logSemaphore.Release();

            return Task.CompletedTask;
        }

        private void UpdateLog(DateTime pingTime, PingReply reply)
        {
            TotalRequests += 1;

            if (reply.Status == IPStatus.Success)
            {
                SuccessRequests += 1;

                if (reply.RoundtripTime > MaxLatency)
                    MaxLatency = reply.RoundtripTime;
                else if (reply.RoundtripTime < MinLatency || MinLatency == 0)
                    MinLatency = reply.RoundtripTime;

                AvgLatency = (TotalRequests * AvgLatency + reply.RoundtripTime) / (TotalRequests + 1);
            }
            else if (reply.Status == IPStatus.TimedOut)
            {
                TimedOutRequests += 1;
            }
            else
            {
                ErrorRequests += 1;
            }

            SuccessRate = SuccessRequests * 100f / TotalRequests;

            PingLog log = new PingLog(pingTime, reply);

            string path = Directory.GetCurrentDirectory() + "/" + DateTime.Now.ToString("yyyy-MM-dd") + "_" + LOGFILENAME + LOGFILEEXTENSION;
            logfileStream = File.AppendText(path);
            logfileStream.WriteLine(log.ToString());
            logfileStream.Close();

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
            ScrollContentPresenter? presenter = null;

            for (Visual? visual = container; visual != null && visual != listBox; visual = VisualTreeHelper.GetParent(visual) as Visual)
                if ((presenter = visual as ScrollContentPresenter) != null)
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
            double height = (container as ListBoxItem).ActualHeight;
            var lbHeight = listBox.ActualHeight;
            var showCount = (int)(lbHeight / height) - 1;

            //Set the scrollbar
            if (scrollInfo.CanVerticallyScroll)
                scrollInfo.SetVerticalOffset(index - showCount);
        }

        private static DependencyObject? FirstVisualChild(Visual visual)
        {
            if (visual == null)
                return null;

            if (VisualTreeHelper.GetChildrenCount(visual) == 0)
                return null;

            return VisualTreeHelper.GetChild(visual, 0);
        }

        private void OnWindowClosing(object? sender, CancelEventArgs e)
        {
            isRunning = false;
            Task.WaitAll(taskQueue.ToArray(), millisecondsTimeout: 3000);
        }
    }
}
