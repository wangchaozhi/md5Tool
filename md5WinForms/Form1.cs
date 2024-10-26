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

namespace md5WinForms
{
    public partial class Form1 : Form
    {
        private BlockingCollection<string> fileQueue = new(); // 线程安全队列
        private BlockingCollection<string> filesizeQueue = new(); // 文件大小队列
        private Dictionary<long, List<string>> fileSizeMap = new(); // 文件大小映射表
        private Dictionary<string, List<string>> md5ToFileMap = new(); // MD5映射表
        private HashSet<string> displayedMd5s = new(); // 已显示的MD5值

        private CancellationTokenSource cancellationTokenSource;
        private Stopwatch stopwatch; // 计时器
        private bool isScanning = false; // 标志扫描状态

        public Form1()
        {
            InitializeComponent();
        }

        // 选择U盘目录
        private async void btnSelectFolder_Click(object sender, EventArgs e)
        {
            if (isScanning)
            {
                await StopPreviousScan(); // 停止上一次扫描
            }

            using var folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                string selectedPath = folderBrowserDialog.SelectedPath;

                ResetData(); // 重置数据
                ResetUI(); // 重置UI状态

                cancellationTokenSource = new CancellationTokenSource();
                stopwatch = new Stopwatch();
                isScanning = true;

                lblStatus.Text = "扫描中...";
                stopwatch.Start(); // 开始计时

                // 启动扫描和状态更新线程
                Task.Run(() => ScanFiles(selectedPath, cancellationTokenSource.Token));
                Task.Run(() => ProcessFilesizeQueue(cancellationTokenSource.Token));
                Task.Run(() => ProcessFiles(cancellationTokenSource.Token));
                Task.Run(() => UpdateStatusLabels(cancellationTokenSource.Token));
                Task.Run(() => PeriodicRefreshListView(cancellationTokenSource.Token));
            }
        }

        // 停止上一次扫描任务
        private async Task StopPreviousScan()
        {
            cancellationTokenSource?.Cancel(); // 取消任务
            stopwatch?.Stop(); // 停止计时
            isScanning = false;

            await Task.Delay(500); // 等待任务完成
            lblStatus.Text = "上次扫描已停止";
        }

        // 重置数据
        private void ResetData()
        {
            fileQueue = new BlockingCollection<string>(); // 重新实例化队列
            filesizeQueue = new BlockingCollection<string>(); // 重新实例化文件大小队列
            fileSizeMap = new Dictionary<long, List<string>>(); // 清空文件大小映射表
            md5ToFileMap = new Dictionary<string, List<string>>(); // 清空MD5映射表
            displayedMd5s = new HashSet<string>(); // 清空已显示的MD5集合
        }

        // 重置UI状态
        private void ResetUI()
        {
            listView1.Items.Clear();
            lblScanTime.Text = "扫描时间：0 秒";
            lblMd5Count.Text = "MD5 相同文件数量：0";
        }

        // 更新扫描时间和MD5数量标签
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
                await Task.Delay(1000); // 每秒更新一次
            }
        }

        // 每5秒刷新ListView
        private async Task PeriodicRefreshListView(CancellationToken token)
        {
            while (!token.IsCancellationRequested && isScanning)
            {
                RefreshListView();
                await Task.Delay(1000); // 每5秒刷新一次
            }
        }

        // 获取MD5相同文件的数量
        private int GetDuplicateMd5Count()
        {
            lock (md5ToFileMap)
            {
                return md5ToFileMap.Values.Count(v => v.Count > 1);
            }
        }

        // 扫描文件并将文件路径添加到队列
        private void ScanFiles(string folderPath, CancellationToken token)
        {
            try
            {
                IEnumerable<string> files = new List<string>();

                try
                {
                    // 尝试获取所有子目录中的文件
                    files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
                }
                catch (UnauthorizedAccessException ex)
                {
                    LogError($"将降级为递归扫描目录: {ex.Message}");
                    files = ScanFilesTopDirectoryOnly(folderPath, token); // 降级为递归扫描
                }
                catch (IOException ex)
                {
                    LogError($"IO 异常: {ex.Message}");
                }
                catch (Exception ex)
                {
                    LogError($"其他异常: {ex.Message}");
                    // 出错时跳过该目录
                }

                // 将文件加入队列，处理取消令牌
                foreach (var file in files)
                {
                    if (token.IsCancellationRequested) break;
                    fileQueue.Add(file); // 添加文件到队列
                }
            }
            finally
            {
                fileQueue.CompleteAdding(); // 通知消费者不再有新的文件
            }
        }

        private IEnumerable<string> ScanFilesTopDirectoryOnly(string folderPath, CancellationToken token)
        {
            var files = new List<string>();

            try
            {
                // 获取当前目录下的文件
                files.AddRange(Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly));
            }
            catch (Exception ex)
            {
                LogError($"降级扫描时发生异常: {ex.Message}");
                // 跳过该目录
            }

            // 获取所有子目录并递归扫描
            try
            {
                foreach (var subDirectory in Directory.GetDirectories(folderPath))
                {
                    if (token.IsCancellationRequested) break;

                    // 递归获取子目录中的文件
                    files.AddRange(ScanFilesTopDirectoryOnly(subDirectory, token));
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                LogError($"无法访问子目录: {ex.Message}");
            }
            catch (IOException ex)
            {
                LogError($"IO 异常: {ex.Message}");
            }
            catch (Exception ex)
            {
                LogError($"其他异常: {ex.Message}");
            }

            return files;
        }



        // 消费文件队列并将其添加到文件大小队列
        private async Task ProcessFilesizeQueue(CancellationToken token)
        {
            while (!fileQueue.IsCompleted)
            {
                try
                {
                    if (!fileQueue.TryTake(out var filePath, Timeout.Infinite, token)) continue;

                    FileInfo fileInfo = new FileInfo(filePath);
                    long fileSize = fileInfo.Length;

                    lock (fileSizeMap)
                    {
                        if (!fileSizeMap.ContainsKey(fileSize))
                        {
                            fileSizeMap[fileSize] = new List<string>();
                        }

                        fileSizeMap[fileSize].Add(filePath);

                        // 如果文件大小已存在且数量大于1，添加所有文件到文件大小队列
                        if (fileSizeMap[fileSize].Count == 2)
                        {
                            foreach (var file in fileSizeMap[fileSize])
                            {
                                filesizeQueue.Add(file);
                            }
                        }
                        else if (fileSizeMap[fileSize].Count > 2)
                        {
                            filesizeQueue.Add(filePath);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            filesizeQueue.CompleteAdding(); // 通知消费者不再有新的文件
        }

        // 消费文件大小队列并计算MD5
        private async Task ProcessFiles(CancellationToken token)
        {
            while (!filesizeQueue.IsCompleted)
            {
                try
                {
                    if (!filesizeQueue.TryTake(out var filePath, Timeout.Infinite, token)) continue;

                    string md5Hash = CalculateMD5(filePath);

                    lock (md5ToFileMap)
                    {
                        if (!md5ToFileMap.ContainsKey(md5Hash))
                        {
                            md5ToFileMap[md5Hash] = new List<string>();
                        }

                        md5ToFileMap[md5Hash].Add(filePath);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogError($"无法读取文件: {ex.Message}");
                }
            }

            await Task.Run(() => FinalizeProcessing());
        }

        // 最终处理和刷新UI
        private void FinalizeProcessing()
        {
            Invoke(new Action(() =>
            {
                RefreshListView(); // 最后一次刷新
                isScanning = false;
                stopwatch.Stop();
                lblStatus.Text = "扫描完成";
                lblMd5Count.Text = $"MD5 相同文件数量：{GetDuplicateMd5Count()}";
                // 弹出提示框通知用户扫描完成
                MessageBox.Show("扫描已完成！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }));
        }

        // 计算文件的MD5
        private string CalculateMD5(string filePath)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        // 日志记录函数
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

        // 保存结果到CSV
        private void btnSave_Click(object sender, EventArgs e)
        {
            using var saveFileDialog = new SaveFileDialog { Filter = "CSV 文件 (*.csv)|*.csv" };
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                SaveResultsToFile(saveFileDialog.FileName);
            }
        }

        // 保存ListView内容到CSV文件
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

        // 刷新ListView
        private void RefreshListView()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(RefreshListView));
                return;
            }

            lock (md5ToFileMap)
            {
                foreach (var kvp in md5ToFileMap.Where(kvp => kvp.Value.Count > 1))
                {
                    if (displayedMd5s.Contains(kvp.Key)) continue;

                    var item = new ListViewItem(kvp.Value[0]);
                    item.SubItems.Add(kvp.Value.Count > 1 ? kvp.Value[1] : "无");
                    item.SubItems.Add(kvp.Key);
                    item.SubItems.Add(kvp.Value.Count > 2 ? "是" : "否");
                    listView1.Items.Add(item);

                    displayedMd5s.Add(kvp.Key);
                }
            }
        }
    }
}