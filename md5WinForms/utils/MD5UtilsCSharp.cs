
using System.Security.Cryptography;

namespace md5WinForms.utils
{
    public static class MD5UtilsCSharp
    {
        /// <summary>
        /// 批量计算文件的 MD5 哈希值（同步版本）
        /// </summary>
        /// <param name="files">文件路径数组</param>
        /// <returns>每个文件的 MD5 哈希数组</returns>
        public static string[] CalculateMD5FromFiles(string[] files)
        {
            if (files == null)
                throw new ArgumentNullException(nameof(files));

            string[] results = new string[files.Length];

            using (var md5 = MD5.Create())
            {
                for (int i = 0; i < files.Length; i++)
                {
                    results[i] = CalculateMD5(files[i], md5);
                }
            }

            return results;
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

            string[] results = new string[files.Length];
            
            // 并行处理多个文件
            var tasks = new Task<string>[files.Length];
            
            for (int i = 0; i < files.Length; i++)
            {
                int index = i; // 捕获循环变量
                tasks[i] = Task.Run(() => CalculateMD5(files[index]));
            }

            var md5Results = await Task.WhenAll(tasks);
            return md5Results;
        }

        /// <summary>
        /// 计算单个文件的 MD5 哈希值
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>MD5 哈希字符串</returns>
        public static string CalculateMD5(string filePath)
        {
            using (var md5 = MD5.Create())
            {
                return CalculateMD5(filePath, md5);
            }
        }

        /// <summary>
        /// 使用指定的 MD5 实例计算文件哈希值
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="md5">MD5 实例</param>
        /// <returns>MD5 哈希字符串</returns>
        private static string CalculateMD5(string filePath, MD5 md5)
        {
            try
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"文件不存在: {filePath}");

                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"计算文件 '{filePath}' 的MD5失败: {ex.Message}", ex);
            }
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

            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(data);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// 计算字符串的 MD5 哈希值
        /// </summary>
        /// <param name="input">输入字符串</param>
        /// <param name="encoding">字符编码（默认UTF-8）</param>
        /// <returns>MD5 哈希字符串</returns>
        public static string CalculateMD5(string input, System.Text.Encoding encoding = null)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            encoding = encoding ?? System.Text.Encoding.UTF8;
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

            string actualHash = CalculateMD5(filePath);
            return string.Equals(actualHash, expectedHash.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
    