using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewLife.Video.IoT;
using NewLife.Video.Studio.Services;

namespace NewLife.Video.Studio.ViewModels;

/// <summary>ONVIF 设备管理视图模型（M5-2）</summary>
public partial class OnvifViewModel : ViewModelBase
{
    private readonly IOnvifService _onvifService;

    [ObservableProperty]
    private ObservableCollection<OnvifDeviceInfo> _devices = [];

    [ObservableProperty]
    private Boolean _isSearching;

    [ObservableProperty]
    private String _statusText = "就绪";

    /// <summary>实例化 ONVIF 视图模型</summary>
    public OnvifViewModel() : this(new OnvifService()) { }

    /// <summary>实例化 ONVIF 视图模型（含依赖注入）</summary>
    public OnvifViewModel(IOnvifService onvifService)
    {
        _onvifService = onvifService;
    }

    /// <summary>发现设备</summary>
    [RelayCommand]
    private async Task Discover()
    {
        IsSearching = true;
        StatusText = "正在搜索 ONVIF 设备...";
        Devices.Clear();

        try
        {
            var found = await _onvifService.DiscoverDevicesAsync();
            foreach (var device in found)
            {
                Devices.Add(device);
            }
            StatusText = $"搜索完成，找到 {Devices.Count} 个设备";
        }
        catch (Exception ex)
        {
            StatusText = $"搜索失败: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }
}
