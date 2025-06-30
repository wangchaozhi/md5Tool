using System.Runtime.InteropServices;
namespace md5WinForms.utils;
public static class MD5Utils
{
    // 导入 C 库的批量 MD5 计算函数
    [DllImport("md5utils.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int calculate_md5_from_array(
        IntPtr filenames, int count, IntPtr resultBuffer);

    /// <summary>
    /// 批量计算文件的 MD5 哈希值，并返回结果。
    /// </summary>
    /// <param name="files">文件路径数组。</param>
    /// <returns>每个文件的 MD5 哈希数组。</returns>
    public static string[] CalculateMD5FromFiles(string[] files)
    {
        int fileCount = files.Length;
        IntPtr[] filePtrs = new IntPtr[fileCount];

        // 将 C# 字符串数组转换为非托管指针数组
        for (int i = 0; i < fileCount; i++)
        {
            filePtrs[i] = Marshal.StringToHGlobalAnsi(files[i]);
        }

        // 为 MD5 结果分配非托管内存（每个结果 32 字节）
        IntPtr resultBuffer = Marshal.AllocHGlobal(fileCount * 32);

        // 创建 GCHandle 来固定指针数组
        GCHandle handle = GCHandle.Alloc(filePtrs, GCHandleType.Pinned);

        try
        {
            IntPtr ptrArray = handle.AddrOfPinnedObject();

            // 调用 C 库计算 MD5
            int status = calculate_md5_from_array(ptrArray, fileCount, resultBuffer);

            if (status != 0)
                throw new InvalidOperationException("MD5 计算失败");

            // 读取并返回 MD5 结果
            string[] md5Results = new string[fileCount];
            for (int i = 0; i < fileCount; i++)
            {
                md5Results[i] = Marshal.PtrToStringAnsi(resultBuffer + (i * 32), 32);
            }

            return md5Results;
        }
        finally
        {
            // 释放所有非托管资源
            FreeMemory(handle, filePtrs, resultBuffer);
        }
    }

    /// <summary>
    /// 释放分配的非托管内存。
    /// </summary>
    private static void FreeMemory(GCHandle handle, IntPtr[] filePtrs, IntPtr resultBuffer)
    {
        handle.Free();
        foreach (var ptr in filePtrs)
        {
            Marshal.FreeHGlobal(ptr);
        }
        Marshal.FreeHGlobal(resultBuffer);
    }
}
