using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using md5WinForms.utils;

namespace md5WinForms
{
    public partial class Form1 : Form
    {
        private BlockingCollection<string> fileQueue = new();
        private BlockingCollection<string> filesizeQueue = new();
        private ConcurrentDictionary<long, ConcurrentBag<string>> fileSizeMap = new();
        private ConcurrentDictionary<string, ConcurrentBag<string>> md5ToFileMap = new();
        private HashSet<string> displayedMd5s = new();

        private CancellationTokenSource cancellationTokenSource;
        private Stopwatch stopwatch;
        private volatile bool isScanning = false;
        private int activeFileSizeThreads = 0; // 跟踪活跃的文件大小处理线程

        public Form1()
        {
            InitializeComponent();
        }

        private async void btnSelectFolder_Click(object sender, EventArgs e)
        {
            if (isScanning)
            {
                await StopPreviousScan();
            }

            using var folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                string selectedPath = folderBrowserDialog.SelectedPath;

                ResetData();
                ResetUI();

                cancellationTokenSource = new CancellationTokenSource();
                stopwatch = new Stopwatch();
                isScanning = true;

                lblStatus.Text = "扫描中...";
                stopwatch.Start();

                // 启动文件扫描
                Task.Run(() => ScanFiles(selectedPath, cancellationTokenSource.Token));
                
                // 启动文件大小处理线程
                int numThreads = Environment.ProcessorCount;
                activeFileSizeThreads = numThreads;
                for (int i = 0; i < numThreads; i++)
                {
                    Task.Run(() => ProcessFilesizeQueue(cancellationTokenSource.Token));
                }

                // 启动MD5处理和UI更新
                Task.Run(() => ProcessFiles(cancellationTokenSource.Token));
                Task.Run(() => UpdateStatusLabels(cancellationTokenSource.Token));
                Task.Run(() => PeriodicRefreshListView(cancellationTokenSource.Token));
            }
        }

        private async Task StopPreviousScan()
        {
            cancellationTokenSource?.Cancel();
            stopwatch?.Stop();
            isScanning = false;
            await Task.Delay(500);
            lblStatus.Text = "上次扫描已停止";
        }

        private void ResetData()
        {
            fileQueue = new BlockingCollection<string>();
            filesizeQueue = new BlockingCollection<string>();
            fileSizeMap = new ConcurrentDictionary<long, ConcurrentBag<string>>();
            md5ToFileMap = new ConcurrentDictionary<string, ConcurrentBag<string>>();
            displayedMd5s = new HashSet<string>();
            activeFileSizeThreads = 0;
        }

        private void ResetUI()
        {
            listView1.Items.Clear();
            lblScanTime.Text = "扫描时间：0 秒";
            lblMd5Count.Text = "MD5 相同文件数量：0";
        }

        private async Task UpdateStatusLabels(CancellationToken token)
        {
            while (!token.IsCancellationRequested && isScanning)
            {
                Invoke(new Action(() =>
                {
                    TimeSpan elapsed = stopwatch.Elapsed;
                    string formattedTime =
                        $"{elapsed.Hours}小时 {elapsed.Minutes}分钟 {elapsed.Seconds + elapsed.Milliseconds / 1000.0:F1}秒";
                    lblScanTime.Text = $"扫描时间：{formattedTime}";
                    lblMd5Count.Text = $"MD5 相同文件数量：{GetDuplicateMd5Count()}";
                }));
                await Task.Delay(1000);
            }
        }

        private async Task PeriodicRefreshListView(CancellationToken token)
        {
            while (!token.IsCancellationRequested && isScanning)
            {
                RefreshListView();
                await Task.Delay(1000);
            }
        }

        private int GetDuplicateMd5Count()
        {
            return md5ToFileMap.Values.Count(v => v.Count > 1);
        }

        private void ScanFiles(string folderPath, CancellationToken token)
        {
            try
            {
                IEnumerable<string> files = new List<string>();

                try
                {
                    files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
                }
                catch (UnauthorizedAccessException ex)
                {
                    LogError($"将降级为递归扫描目录: {ex.Message}");
                    files = ScanFilesTopDirectoryOnly(folderPath, token);
                }
                catch (Exception ex)
                {
                    LogError($"扫描异常: {ex.Message}");
                    return;
                }

                foreach (var file in files)
                {
                    if (token.IsCancellationRequested) break;
                    fileQueue.Add(file);
                }
            }
            finally
            {
                fileQueue.CompleteAdding();
            }
        }

        private IEnumerable<string> ScanFilesTopDirectoryOnly(string folderPath, CancellationToken token)
        {
            var files = new List<string>();

            try
            {
                files.AddRange(Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly));

                foreach (var subDirectory in Directory.GetDirectories(folderPath))
                {
                    if (token.IsCancellationRequested) break;
                    files.AddRange(ScanFilesTopDirectoryOnly(subDirectory, token));
                }
            }
            catch (Exception ex)
            {
                LogError($"递归扫描异常: {ex.Message}");
            }

            return files;
        }

        // 修复：使用原子操作来管理线程完成状态
        private async Task ProcessFilesizeQueue(CancellationToken token)
        {
            try
            {
                while (!fileQueue.IsCompleted && !token.IsCancellationRequested)
                {
                    try
                    {
                        if (!fileQueue.TryTake(out var filePath, 100, token)) continue;

                        FileInfo fileInfo = new FileInfo(filePath);
                        long fileSize = fileInfo.Length;

                        // 使用ConcurrentDictionary的线程安全方法
                        fileSizeMap.AddOrUpdate(fileSize, 
                            new ConcurrentBag<string> { filePath },
                            (key, existingBag) =>
                            {
                                existingBag.Add(filePath);
                                
                                // 当文件数量>=2时，将所有文件添加到处理队列
                                if (existingBag.Count >= 2)
                                {
                                    // 只有在刚好等于2时才添加所有文件，避免重复添加
                                    if (existingBag.Count == 2)
                                    {
                                        foreach (var file in existingBag)
                                        {
                                            filesizeQueue.Add(file);
                                        }
                                    }
                                    else
                                    {
                                        // 大于2时只添加当前文件
                                        filesizeQueue.Add(filePath);
                                    }
                                }
                                
                                return existingBag;
                            });
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogError($"处理文件大小时出错: {ex.Message}");
                    }
                }
            }
            finally
            {
                // 使用原子操作来减少活跃线程计数
                if (Interlocked.Decrement(ref activeFileSizeThreads) == 0)
                {
                    // 最后一个线程负责标记完成
                    filesizeQueue.CompleteAdding();
                }
            }
        }

        private async Task ProcessFiles(CancellationToken token)
        {
            int numTasks = Environment.ProcessorCount;
            var tasks = new List<Task>();

            for (int i = 0; i < numTasks; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var batchFiles = new List<string>();
                    const int batchSize = 1000;

                    try
                    {
                        while (!token.IsCancellationRequested)
                        {
                            // 尝试取文件，设置超时避免无限等待
                            if (filesizeQueue.TryTake(out var filePath, 100, token))
                            {
                                batchFiles.Add(filePath);

                                // 达到批量大小或超时处理
                                if (batchFiles.Count >= batchSize)
                                {
                                    await ProcessBatch(batchFiles, token);
                                    batchFiles.Clear();
                                }
                            }
                            else if (filesizeQueue.IsCompleted)
                            {
                                // 队列完成，处理剩余文件并退出
                                if (batchFiles.Count > 0)
                                {
                                    await ProcessBatch(batchFiles, token);
                                }
                                break;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // 取消操作，处理剩余文件
                        if (batchFiles.Count > 0 && !token.IsCancellationRequested)
                        {
                            await ProcessBatch(batchFiles, token);
                        }
                    }
                }, token));
            }

            await Task.WhenAll(tasks);
            FinalizeProcessing();
        }

        private async Task ProcessBatch(List<string> batchFiles, CancellationToken token)
        {
            if (batchFiles.Count == 0 || token.IsCancellationRequested) return;

            try
            {
                // 使用您的MD5工具类
                string[] md5Results = MD5UtilsRust.CalculateMD5FromFiles(batchFiles.ToArray());
                
                for (int j = 0; j < batchFiles.Count && j < md5Results.Length; j++)
                {
                    if (token.IsCancellationRequested) break;

                    string md5Hash = md5Results[j];
                    string file = batchFiles[j];
                    
                    // 使用ConcurrentDictionary的线程安全方法
                    md5ToFileMap.AddOrUpdate(md5Hash,
                        new ConcurrentBag<string> { file },
                        (key, existingBag) =>
                        {
                            existingBag.Add(file);
                            return existingBag;
                        });
                }
            }
            catch (Exception ex)
            {
                LogError($"批量处理MD5时出错: {ex.Message}");
            }
        }

        private void FinalizeProcessing()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(FinalizeProcessing));
                return;
            }

            RefreshListView();
            isScanning = false;
            stopwatch.Stop();
            lblStatus.Text = "扫描完成";
            lblMd5Count.Text = $"MD5 相同文件数量：{GetDuplicateMd5Count()}";
            MessageBox.Show("扫描已完成！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string CalculateMD5(string filePath)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private void LogError(string message)
        {
            string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error_log.txt");
            string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}{Environment.NewLine}";

            try
            {
                File.AppendAllText(logFilePath, logMessage);
            }
            catch (Exception logEx)
            {
                Console.WriteLine($"日志写入失败: {logEx.Message}");
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            using var saveFileDialog = new SaveFileDialog { Filter = "CSV 文件 (*.csv)|*.csv" };
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                SaveResultsToFile(saveFileDialog.FileName);
            }
        }

        private void SaveResultsToFile(string filePath)
        {
            var csvContent = new StringBuilder();
            foreach (ListViewItem item in listView1.Items)
            {
                string address1 = item.Text;
                string address2 = item.SubItems[1].Text;
                string md5 = item.SubItems[2].Text;
                string moreThanTwo = item.SubItems[3].Text;
                csvContent.AppendLine($"{address1},{address2},{md5},{moreThanTwo}");
            }

            File.WriteAllText(filePath, csvContent.ToString());
            MessageBox.Show("MD5 对照表已保存！", "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void RefreshListView()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(RefreshListView));
                return;
            }

            lock (displayedMd5s)
            {
                foreach (var kvp in md5ToFileMap.Where(kvp => kvp.Value.Count > 1))
                {
                    if (displayedMd5s.Contains(kvp.Key)) continue;

                    var fileList = kvp.Value.ToList();
                    var item = new ListViewItem(fileList[0]);
                    item.SubItems.Add(fileList.Count > 1 ? fileList[1] : "无");
                    item.SubItems.Add(kvp.Key);
                    item.SubItems.Add(fileList.Count > 2 ? "是" : "否");
                    listView1.Items.Add(item);

                    displayedMd5s.Add(kvp.Key);
                }
            }
        }
    }
}