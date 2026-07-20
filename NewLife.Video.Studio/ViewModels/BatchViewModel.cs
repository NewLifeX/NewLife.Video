using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewLife.Video.Processing;
using NewLife.Video.Studio.Models;
using NewLife.Video.Studio.Services;

namespace NewLife.Video.Studio.ViewModels;

/// <summary>批量处理视图模型（CONV-3）</summary>
public partial class BatchViewModel : ViewModelBase
{
    private readonly IConverterService _converterService;

    [ObservableProperty]
    private ObservableCollection<BatchTaskItem> _tasks = [];

    [ObservableProperty]
    private BatchTaskItem? _selectedTask;

    [ObservableProperty]
    private Int32 _totalProgress;

    [ObservableProperty]
    private Boolean _isRunning;

    [ObservableProperty]
    private String _outputFormat = ".mp4";

    [ObservableProperty]
    private String _statusText = "就绪";

    /// <summary>支持格式</summary>
    public ObservableCollection<String> OutputFormats { get; } = [".mp4", ".flv", ".avi", ".mkv", ".ts"];

    /// <summary>实例化批量处理视图模型</summary>
    public BatchViewModel() : this(new ConverterService()) { }

    public BatchViewModel(IConverterService converterService) => _converterService = converterService;

    /// <summary>添加文件到队列</summary>
    [RelayCommand]
    private void AddFiles()
    {
        // TODO: 多文件选择对话框
        StatusText = "请使用文件对话框添加文件";
    }

    /// <summary>移除选中任务</summary>
    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedTask != null)
            Tasks.Remove(SelectedTask);
    }

    /// <summary>清空列表</summary>
    [RelayCommand]
    private void ClearAll()
    {
        Tasks.Clear();
        StatusText = "已清空";
    }

    /// <summary>开始批量处理</summary>
    [RelayCommand]
    private async Task StartBatch()
    {
        if (Tasks.Count == 0) { StatusText = "请先添加文件"; return; }

        IsRunning = true;
        TotalProgress = 0;

        for (var i = 0; i < Tasks.Count; i++)
        {
            var task = Tasks[i];
            task.Status = "转换中...";
            StatusText = $"正在处理 ({i + 1}/{Tasks.Count}): {task.Name}";

            try
            {
                await _converterService.ConvertAsync(task.InputPath, task.OutputPath);
                task.Status = "完成";
                task.IsCompleted = true;
            }
            catch (Exception ex)
            {
                task.Status = $"失败: {ex.Message}";
            }

            TotalProgress = (Int32)((i + 1) * 100.0 / Tasks.Count);
        }

        IsRunning = false;
        StatusText = $"批量处理完成（成功: {Tasks.Count(t => t.IsCompleted)}/{Tasks.Count}）";
    }
}

/// <summary>批量任务项</summary>
public partial class BatchTaskItem : ObservableObject
{
    public String InputPath { get; set; } = "";
    public String OutputPath { get; set; } = "";
    public String Name => Path.GetFileName(InputPath);

    [ObservableProperty]
    private String _status = "等待";

    [ObservableProperty]
    private Boolean _isCompleted;
}
