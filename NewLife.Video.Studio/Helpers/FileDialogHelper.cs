using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace NewLife.Video.Studio.Helpers;

/// <summary>文件对话框辅助类，基于 Avalonia StorageProvider 实现跨平台文件选择</summary>
/// <remarks>依赖应用主窗口作为对话框父级，须在 UI 线程调用</remarks>
public static class FileDialogHelper
{
    /// <summary>视频文件类型过滤器</summary>
    public static FilePickerFileType VideoFileType { get; } = new("视频文件")
    {
        Patterns = ["*.mp4", "*.flv", "*.avi", "*.mkv", "*.ts", "*.webm", "*.mov", "*.m4v", "*.mjpeg", "*.mjpg"],
    };

    /// <summary>图像文件类型过滤器</summary>
    public static FilePickerFileType ImageFileType { get; } = new("图像文件")
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp"],
    };

    /// <summary>获取当前应用的主窗口</summary>
    /// <returns>主窗口</returns>
    /// <exception cref="InvalidOperationException">应用未完成初始化时抛出</exception>
    public static Window GetMainWindow()
    {
        if (App.Current is App app && app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is { } window)
            return window;

        throw new InvalidOperationException("主窗口尚未初始化，无法打开文件对话框");
    }

    /// <summary>选择一个文件</summary>
    /// <param name="title">对话框标题</param>
    /// <param name="types">文件类型过滤器，为空时允许所有文件</param>
    /// <returns>所选文件路径，用户取消时返回空字符串</returns>
    public static async Task<String> PickFileAsync(String title, params FilePickerFileType[] types)
    {
        var files = await GetMainWindow().StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = types.Length == 0 ? null : types,
        });

        return files.Count == 0 ? "" : files[0].TryGetLocalPath() ?? "";
    }

    /// <summary>选择多个文件</summary>
    /// <param name="title">对话框标题</param>
    /// <param name="types">文件类型过滤器，为空时允许所有文件</param>
    /// <returns>所选文件路径数组，用户取消时返回空数组</returns>
    public static async Task<String[]> PickFilesAsync(String title, params FilePickerFileType[] types)
    {
        var files = await GetMainWindow().StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = true,
            FileTypeFilter = types.Length == 0 ? null : types,
        });

        return files.Select(e => e.TryGetLocalPath() ?? "").ToArray();
    }

    /// <summary>选择文件夹</summary>
    /// <param name="title">对话框标题</param>
    /// <returns>所选文件夹路径，用户取消时返回空字符串</returns>
    public static async Task<String> PickFolderAsync(String title)
    {
        var folders = await GetMainWindow().StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });

        return folders.Count == 0 ? "" : folders[0].TryGetLocalPath() ?? "";
    }

    /// <summary>保存文件对话框，返回目标路径</summary>
    /// <param name="title">对话框标题</param>
    /// <param name="defaultName">默认文件名</param>
    /// <param name="types">文件类型过滤器，为空时允许所有文件</param>
    /// <returns>保存路径，用户取消时返回空字符串</returns>
    public static async Task<String> PickSaveFileAsync(String title, String defaultName, params FilePickerFileType[] types)
    {
        var file = await GetMainWindow().StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultName,
            FileTypeChoices = types.Length == 0 ? null : types,
        });

        return file == null ? "" : file.TryGetLocalPath() ?? "";
    }
}
