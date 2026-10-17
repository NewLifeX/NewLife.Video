namespace XUnitTest;

/// <summary>测试临时文件辅助。统一生成唯一路径并安全清理，避免 GetTempFileName 每次泄漏一个空文件</summary>
internal static class TestFile
{
    /// <summary>创建唯一临时文件路径（不创建文件）</summary>
    /// <param name="extension">扩展名，可带点</param>
    /// <returns>临时文件完整路径</returns>
    public static String GetTempPath(String extension = null)
    {
        var name = Guid.NewGuid().ToString("N");
        if (!String.IsNullOrEmpty(extension))
            name += extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension;

        return Path.Combine(Path.GetTempPath(), name);
    }
}
