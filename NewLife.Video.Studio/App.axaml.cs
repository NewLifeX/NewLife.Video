using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Video.Studio.Services;
using NewLife.Video.Studio.ViewModels;
using NewLife.Video.Studio.Views;

namespace NewLife.Video.Studio;

public partial class App : Application
{
    /// <summary>DI 服务提供器</summary>
    public static ServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 构建 DI 容器
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // 注册服务
        services.AddSingleton<IPlayerService, PlayerService>();
        services.AddSingleton<IMediaInfoService, MediaInfoService>();
        services.AddSingleton<IFfmpegService, FfmpegService>();
        services.AddSingleton<IDeviceService, DeviceService>();
        services.AddSingleton<IConverterService, ConverterService>();
        services.AddSingleton<IProcessingService, ProcessingService>();
        services.AddSingleton<IOnvifService, OnvifService>();
        services.AddSingleton<IExportService, ExportService>();
    }
}