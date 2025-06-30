using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace md5WinForms.utils
{
    public static class MD5UtilsRust
    {
        // Rust 库的 DLL 名称（根据您的构建配置调整）
        private const string RUST_DLL ="md5utils_rust.dll";
        
        // MD5 字符串长度常量
        private const int MD5_STRING_LENGTH = 33; // 32 字符 + null 终止符

        #region P/Invoke 声明

        /// <summary>
        /// 批量计算文件 MD5 的 Rust 函数
        /// </summary>
        /// <param name="filePaths">文件路径指针数组</param>
        /// <param name="fileCount">文件数量</param>
        /// <param name="resultsBuffer">结果缓冲区</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <returns>成功计算的文件数量，负数表示错误</returns>
        [DllImport(RUST_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int calculate_md5_batch(
            IntPtr[] filePaths,
            int fileCount,
            IntPtr resultsBuffer,
            int bufferSize
        );

        /// <summary>
        /// 计算单个文件 MD5 的 Rust 函数
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="resultBuffer">结果缓冲区</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <returns>0表示成功，负数表示错误</returns>
        [DllImport(RUST_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int calculate_md5_single(
            IntPtr filePath,
            IntPtr resultBuffer,
            int bufferSize
        );

        /// <summary>
        /// 获取 Rust 库版本
        /// </summary>
        /// <returns>版本字符串指针</returns>
        [DllImport(RUST_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr get_version();

        /// <summary>
        /// 获取支持的最大文件数量
        /// </summary>
        /// <returns>最大文件数量</returns>
        [DllImport(RUST_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int get_max_file_count();

        /// <summary>
        /// 获取每个 MD5 结果的字节长度
        /// </summary>
        /// <returns>MD5 长度</returns>
        [DllImport(RUST_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int get_md5_length();

        #endregion

        #region 公共接口方法

        /// <summary>
        /// 批量计算文件的 MD5 哈希值（同步版本）
        /// </summary>
        /// <param name="files">文件路径数组</param>
        /// <returns>每个文件的 MD5 哈希数组</returns>
        public static string[] CalculateMD5FromFiles(string[] files)
        {
            if (files == null)
                throw new ArgumentNullException(nameof(files));

            if (files.Length == 0)
                return new string[0];

            // 检查最大文件数量限制
            int maxFileCount = get_max_file_count();
            if (files.Length > maxFileCount)
            {
                throw new ArgumentException($"文件数量超过限制，最大支持 {maxFileCount} 个文件");
            }

            return CalculateMD5BatchInternal(files);
        }

        /// <summary>
        /// 批量计算文件的 MD5 哈希值（异步版本）
        /// </summary>
        /// <param name="files">文件路径数组</param>
        /// <returns>每个文件的 MD5 哈希数组</returns>
        public static async Task<string[]> CalculateMD5FromFilesAsync(string[] files)
        {
            if (files == null)
                throw new ArgumentNullException(nameof(files));

            if (files.Length == 0)
                return new string[0];

            // 在后台线程中执行 Rust 调用
            return await Task.Run(() => CalculateMD5FromFiles(files));
        }

        /// <summary>
        /// 计算单个文件的 MD5 哈希值
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>MD5 哈希字符串</returns>
        public static string CalculateMD5(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("文件路径不能为空", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"文件不存在: {filePath}");

            return CalculateMD5SingleInternal(filePath);
        }

        /// <summary>
        /// 计算字节数组的 MD5 哈希值
        /// </summary>
        /// <param name="data">字节数组</param>
        /// <returns>MD5 哈希字符串</returns>
        public static string CalculateMD5(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            // 创建临时文件来计算MD5
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(tempFile, data);
                return CalculateMD5SingleInternal(tempFile);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        /// <summary>
        /// 计算字符串的 MD5 哈希值
        /// </summary>
        /// <param name="input">输入字符串</param>
        /// <param name="encoding">字符编码（默认UTF-8）</param>
        /// <returns>MD5 哈希字符串</returns>
        public static string CalculateMD5(string input, Encoding encoding = null)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            encoding = encoding ?? Encoding.UTF8;
            byte[] data = encoding.GetBytes(input);
            return CalculateMD5(data);
        }

        /// <summary>
        /// 验证文件的 MD5 哈希值
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="expectedHash">期望的MD5哈希值</param>
        /// <returns>是否匹配</returns>
        public static bool VerifyMD5(string filePath, string expectedHash)
        {
            if (string.IsNullOrEmpty(expectedHash))
                return false;

            try
            {
                string actualHash = CalculateMD5(filePath);
                return string.Equals(actualHash, expectedHash.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取 Rust 库版本信息
        /// </summary>
        /// <returns>版本字符串</returns>
        public static string GetRustLibVersion()
        {
            try
            {
                IntPtr versionPtr = get_version();
                return Marshal.PtrToStringAnsi(versionPtr) ?? "Unknown";
            }
            catch
            {
                return "Error getting version";
            }
        }

        /// <summary>
        /// 获取支持的最大文件数量
        /// </summary>
        /// <returns>最大文件数量</returns>
        public static int GetMaxFileCount()
        {
            try
            {
                return get_max_file_count();
            }
            catch
            {
                return 1000; // 默认值
            }
        }

        #endregion

        #region 内部实现方法

        /// <summary>
        /// 内部批量 MD5 计算实现
        /// </summary>
        /// <param name="files">文件路径数组</param>
        /// <returns>MD5 哈希数组</returns>
        private static string[] CalculateMD5BatchInternal(string[] files)
        {
            IntPtr[] filePathPtrs = new IntPtr[files.Length];
            IntPtr resultsBuffer = IntPtr.Zero;

            try
            {
                // 将字符串转换为 UTF-8 字节并分配内存
                for (int i = 0; i < files.Length; i++)
                {
                    byte[] pathBytes = Encoding.UTF8.GetBytes(files[i] + '\0'); // 添加 null 终止符
                    filePathPtrs[i] = Marshal.AllocHGlobal(pathBytes.Length);
                    Marshal.Copy(pathBytes, 0, filePathPtrs[i], pathBytes.Length);
                }

                // 分配结果缓冲区
                int bufferSize = files.Length * MD5_STRING_LENGTH;
                resultsBuffer = Marshal.AllocHGlobal(bufferSize);

                // 调用 Rust 函数
                int successCount = calculate_md5_batch(filePathPtrs, files.Length, resultsBuffer, bufferSize);

                if (successCount < 0)
                {
                    throw new InvalidOperationException($"Rust MD5 批量计算失败，错误代码: {successCount}");
                }

                // 读取结果
                string[] results = new string[files.Length];
                for (int i = 0; i < files.Length; i++)
                {
                    IntPtr resultPtr = IntPtr.Add(resultsBuffer, i * MD5_STRING_LENGTH);
                    string result = Marshal.PtrToStringAnsi(resultPtr) ?? "ERROR";
                    
                    if (result == "ERROR")
                    {
                        throw new InvalidOperationException($"计算文件 '{files[i]}' 的MD5失败");
                    }
                    
                    results[i] = result;
                }

                return results;
            }
            finally
            {
                // 清理内存
                for (int i = 0; i < filePathPtrs.Length; i++)
                {
                    if (filePathPtrs[i] != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(filePathPtrs[i]);
                    }
                }

                if (resultsBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(resultsBuffer);
                }
            }
        }

        /// <summary>
        /// 内部单个文件 MD5 计算实现
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>MD5 哈希字符串</returns>
        private static string CalculateMD5SingleInternal(string filePath)
        {
            IntPtr filePathPtr = IntPtr.Zero;
            IntPtr resultBuffer = IntPtr.Zero;

            try
            {
                // 将文件路径转换为 UTF-8 字节
                byte[] pathBytes = Encoding.UTF8.GetBytes(filePath + '\0');
                filePathPtr = Marshal.AllocHGlobal(pathBytes.Length);
                Marshal.Copy(pathBytes, 0, filePathPtr, pathBytes.Length);

                // 分配结果缓冲区
                resultBuffer = Marshal.AllocHGlobal(MD5_STRING_LENGTH);

                // 调用 Rust 函数
                int result = calculate_md5_single(filePathPtr, resultBuffer, MD5_STRING_LENGTH);

                if (result != 0)
                {
                    string errorMsg = result switch
                    {
                        -1 => "无效参数",
                        -2 => "字符串转换失败",
                        -3 => "MD5 计算失败",
                        _ => $"未知错误，代码: {result}"
                    };
                    throw new InvalidOperationException($"计算文件 '{filePath}' 的MD5失败: {errorMsg}");
                }

                // 读取结果
                string md5Hash = Marshal.PtrToStringAnsi(resultBuffer);
                if (string.IsNullOrEmpty(md5Hash))
                {
                    throw new InvalidOperationException($"读取文件 '{filePath}' 的MD5结果失败");
                }

                return md5Hash;
            }
            finally
            {
                // 清理内存
                if (filePathPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(filePathPtr);
                }

                if (resultBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(resultBuffer);
                }
            }
        }

        #endregion

        #region 静态构造函数和库检查

        /// <summary>
        /// 静态构造函数，检查 Rust 库是否可用
        /// </summary>
        static MD5UtilsRust()
        {
            try
            {
                // 尝试调用一个简单的函数来验证库是否可用
                get_md5_length();
            }
            catch (DllNotFoundException)
            {
                throw new DllNotFoundException($"找不到 Rust 库 '{RUST_DLL}'。请确保该库文件位于可执行文件目录或系统 PATH 中。");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"初始化 Rust 库失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 检查 Rust 库是否可用
        /// </summary>
        /// <returns>如果库可用返回 true</returns>
        public static bool IsRustLibraryAvailable()
        {
            try
            {
                get_md5_length();
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}