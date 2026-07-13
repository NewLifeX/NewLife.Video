using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewLife.Video.Studio.Services;

namespace NewLife.Video.Studio.ViewModels;

/// <summary>播放器视图模型</summary>
public partial class PlayerViewModel : ViewModelBase
{
    private readonly IPlayerService _playerService;

    [ObservableProperty]
    private String _sourceUrl = "";

    [ObservableProperty]
    private Boolean _isPlaying;

    [ObservableProperty]
    private Boolean _isPaused;

    [ObservableProperty]
    private Boolean _hasVideo;

    [ObservableProperty]
    private WriteableBitmap? _videoBitmap;

    [ObservableProperty]
    private String _statusText = "就绪";

    [ObservableProperty]
    private String _frameRateText = "";

    /// <summary>播放/暂停按钮文字</summary>
    public String PlayPauseText => IsPlaying && !IsPaused ? "暂停" : "播放";

    /// <summary>最近打开的文件列表</summary>
    public ObservableCollection<String> RecentFiles { get; } = [];

    /// <summary>播放器服务实例</summary>
    public IPlayerService PlayerService => _playerService;

    /// <summary>实例化播放器视图模型</summary>
    public PlayerViewModel() : this(new PlayerService()) { }

    /// <summary>实例化播放器视图模型（含依赖注入）</summary>
    public PlayerViewModel(IPlayerService playerService)
    {
        _playerService = playerService;
        _playerService.FrameRendered += OnFrameRendered;
        _playerService.StateChanged += OnStateChanged;
    }

    /// <summary>打开文件命令</summary>
    [RelayCommand]
    private async Task OpenFile()
    {
        // TODO: 使用 Avalonia 的 OpenFileDialog
        await Task.CompletedTask;
    }

    /// <summary>打开 URL 命令</summary>
    [RelayCommand]
    private void OpenUrl()
    {
        if (String.IsNullOrWhiteSpace(SourceUrl)) return;

        _playerService.Open(SourceUrl);
        _playerService.Play();
        StatusText = $"正在打开: {SourceUrl}";

        // 添加到最近文件
        if (!RecentFiles.Contains(SourceUrl))
            RecentFiles.Insert(0, SourceUrl);
    }

    /// <summary>播放/暂停切换</summary>
    [RelayCommand]
    private void TogglePlayPause()
    {
        if (IsPlaying && !IsPaused)
        {
            _playerService.Pause();
        }
        else
        {
            _playerService.Play();
        }
    }

    /// <summary>停止</summary>
    [RelayCommand]
    private void Stop()
    {
        _playerService.Stop();
        VideoBitmap = null;
        HasVideo = false;
        StatusText = "已停止";
        FrameRateText = "";
    }

    private void OnFrameRendered(Object? sender, (Byte[] Data, Int32 Width, Int32 Height, Int32 Stride) e)
    {
        // 在 UI 线程更新 WriteableBitmap
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var bitmap = VideoBitmap;
                if (bitmap == null || bitmap.PixelSize.Width != e.Width || bitmap.PixelSize.Height != e.Height)
                {
                    bitmap = new WriteableBitmap(
                        new Avalonia.PixelSize(e.Width, e.Height),
                        new Avalonia.Vector(96, 96),
                        PixelFormat.Bgra8888,
                        AlphaFormat.Opaque);
                    VideoBitmap = bitmap;
                    HasVideo = true;
                }

                using var locked = bitmap.Lock();
                unsafe
                {
                    var src = e.Data;
                    var dst = (Byte*)locked.Address;
                    fixed (Byte* pSrc = src)
                    {
                        Buffer.MemoryCopy(pSrc, dst, e.Data.Length, e.Data.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText = $"渲染错误: {ex.Message}";
            }
        });
    }

    private void OnStateChanged(Object? sender, NewLife.Video.Streaming.PlayState state)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            IsPlaying = state == NewLife.Video.Streaming.PlayState.Playing;
            IsPaused = state == NewLife.Video.Streaming.PlayState.Paused;
            StatusText = state switch
            {
                NewLife.Video.Streaming.PlayState.Playing => "播放中",
                NewLife.Video.Streaming.PlayState.Paused => "已暂停",
                NewLife.Video.Streaming.PlayState.Stopped => "已停止",
                NewLife.Video.Streaming.PlayState.Ended => "播放结束",
                _ => state.ToString(),
            };
            FrameRateText = IsPlaying ? $"帧率: {_playerService.FrameRate:F1} fps" : "";
            OnPropertyChanged(nameof(PlayPauseText));
        });
    }
}
