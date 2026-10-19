# NewLife.Video - 视频编解码库

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/NewLife.Video?logo=github)


![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.Video?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.Video?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.Video?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.Video?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.Video/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.Video/actions/workflows/test.yml)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20arm64%20%7C%20loongarch64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.Video?logo=github)](https://github.com/NewLifeX/NewLife.Video/graphs/contributors)

<!-- lang:start -->
🌐 [繁體中文](README.zh-TW.md) | [English](README.en.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> **[English](README.en.md)** | 简体中文

支持各种视频编解码、容器格式、帧处理、流媒体传输和IoT视频适配

源码： https://github.com/NewLifeX/NewLife.Video  
Nuget：NewLife.Video  

## 功能特性

### 编解码引擎
- H.264/H.265 解码与编码（FFmpeg 管道封装，支持硬件编码器透传）
- MJPEG 纯托管编解码
- VP8/VP9/AV1 解码与编码（FFmpeg 管道封装）
- 颜色空间转换：YUV420P/NV12 ↔ RGB24/BGRA32 (BT.601，SIMD 加速)
- 芯片头适配：海思/大华/宇视
- FFmpeg 运行时自动检测/下载/缓存
- 一键转码 API：VideoTranscoder

### 容器格式
| 格式 | 读取 | 写入 | 备注 |
|------|:--:|:--:|------|
| MP4 (ISO BMFF) | ✅ | ✅ | ftyp/moov/mdat 完整盒结构 |
| FLV | ✅ | ✅ | FLV 头 + VideoTag |
| AVI (RIFF) | ✅ | ✅ | avih/strh/strf/movi/idx1 完整结构 |
| TS (MPEG-TS) | ✅ | ✅ | PAT/PMT + 188字节 TS 包 |
| MKV/WebM (Matroska) | ✅ | ✅ | EBML/Segment/Tracks/Cluster/SimpleBlock |
| 原始视频帧 | ✅ | ✅ | YUV420P/NV12/RGB24 |
| fMP4/CMAF (分段) | ❌ | ✅ | Fmp4SegmentWriter 直播场景 |

### 帧处理
尺寸缩放、区域裁剪、旋转翻转、色彩调整、文字水印、时间戳叠加、缩略图生成、移动侦测、帧率转换

### 图像格式
- 快照输出：JPEG/BMP/TIFF/PNG/WebP/GIF 纯托管编码，AVIF FFmpeg 管道编码
- 动画 GIF 纯托管编码（GIF 89a，自研 LZW 压缩）
- 批量帧编码管线：多帧批量输出 + 网格缩略图

### 流媒体
- RTP 封包/解包：H.264 (RFC 6184)、H.265 (RFC 7798)
- RTSP 客户端：OPTIONS/DESCRIBE/SETUP/PLAY/TEARDOWN
- RTMP 推流：握手 + AMF0 命令层（connect/createStream/publish）
- HLS 分段：m3u8 生成 + TS 分段 + 自动清理
- JitterBuffer、WebSocket 视频通道

### 流式播放
- FfmpegPlayer 播放引擎：支持 RTSP/RTMP/HLS/本地文件，FrameDecoded 事件输出原始帧
- fMP4 over WebSocket：Fmp4WebSocketStreamer 浏览器实时播放，延迟 1-3 秒
- RTSP→WebSocket 桥接：摄像头 RTSP 流转浏览器播放
- 多路摄像头管理：CameraStreamManager 连接池/自动重连/健康监测（单进程 16+ 路 1080p）
- 纯托管帧渲染器：VideoFrameRenderer 输出 BGRA32 内存缓冲区

### IoT 视频适配
- ONVIF：WS-Discovery 设备发现、Media 获取流地址、PTZ 云台控制、Profile G 边缘存储
- GB/T 28181：SIP 注册/心跳、实时点播（INVITE+SDP+RTP）
- 录像调度：定时 + 事件触发
- 芯片视频头：海思/大华/宇视

### 设备采集
- ScreenCapture：Windows DXGI 桌面捕获 / 跨平台兼容方案
- DirectShow / V4L2 / macOS AVFoundation 多后端支持

### 桌面客户端（NewLife.Video.Studio）
- 基于 Avalonia 的全功能视频处理桌面应用，覆盖采集、播放、处理、转码、流媒体全流程
- 摄像头/屏幕/设备采集、媒体信息查看、帧浏览器
- 批量处理、格式转换、FFmpeg 控制台、HLS 直播、ONVIF 云台控制

## 文档

| 文档 | 说明 |
|------|------|
| [需求文档](Doc/需求文档.md) | 项目愿景、核心目标与模块划分 |
| [功能清单](Doc/功能模块清单.md) | 82 项功能的实现/测试/注释三维状态追踪 |
| [架构设计](Doc/架构设计.md) | 分层架构、组件交互与关键设计决策 |
| [竞品分析](Doc/竞品分析报告.md) | .NET 视频处理领域竞品对比与差异化定位 |
| [更新日志](Doc/ChangeLog.md) | 版本发布历史 |

## 快速开始

```csharp
// 1. 使用内置编解码器
var factory = new VideoCodecFactory(); // 自动注册 MJPEG/H.264/H.265/VP8/VP9

// 2. 容器读写
using var writer = VideoContainerFactory.CreateWriter("output.mp4");
writer.WriteMetadata(1920, 1080, VideoTypes.H264, 30);
writer.WriteFrame(new VPacket { Data = new Packet(h264Data), CodecType = VideoTypes.H264 });
// Dispose 时自动写入完整文件

// 3. 帧处理管道
var pipeline = new VideoPipeline();
pipeline.Append(new FrameResizer(640, 480))
       .Append(new TimestampOverlay())
       .Append(new WatermarkOverlay("Camera-01"));

// 4. 流媒体推流
var client = new RtmpClient("rtmp://server/live/stream");
await client.ConnectAsync();
await client.ConnectStreamAsync();
await client.CreateStreamAsync();
await client.PublishAsync();
await client.PushVideoAsync(new VPacket { Data = new Packet(avccData), CodecType = VideoTypes.H264 });

// 5. ONVIF 设备发现
var discovery = new OnvifDiscovery();
var devices = await discovery.DiscoverAsync();
```

## 新生命项目矩阵
各项目默认支持net9.0/netstandard2.1/netstandard2.0/net4.62/net4.5，旧版（2024.0801）支持net4.0/net2.0  

|                               项目                               | 年份  | 说明                                                                                        |
| :--------------------------------------------------------------: | :---: | ------------------------------------------------------------------------------------------- |
|                             基础组件                             |       | 支撑其它中间件以及产品项目                                                                  |
|          [NewLife.Core](https://github.com/NewLifeX/X)           | 2002  | 核心库，日志、配置、缓存、网络、序列化、APM性能追踪                                         |
|    [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode)    | 2005  | 大数据中间件，单表百亿级，MySql/SQLite/SqlServer/Oracle/PostgreSql/达梦，自动分表，读写分离 |
|      [NewLife.Net](https://github.com/NewLifeX/NewLife.Net)      | 2005  | 网络库，单机千万级吞吐率（2266万tps），单机百万级连接（400万Tcp长连接）                     |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011  | 协议通信库，提供CS应用通信框架，支持Http/RPC通信框架，高吞吐，物联网设备低开销易接入        |
|     [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube)     | 2010  | 魔方快速开发平台，集成了用户权限、SSO登录、OAuth服务端等，单表100亿级项目验证               |
|    [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent)    | 2008  | 服务管理组件，把应用安装成为操作系统守护进程，Windows服务、Linux的Systemd                   |
|     [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero)     | 2020  | Zero零代脚手架，基于NewLife组件生态的项目模板NewLife.Templates，Web、WebApi、Service        |
|                              中间件                              |       | 对接知名中间件平台                                                                          |
|    [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis)    | 2017  | Redis客户端，微秒级延迟，百万级吞吐，丰富的消息队列，百亿级数据量项目验证                   |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018  | RocketMQ纯托管客户端，支持Apache RocketMQ和阿里云消息队列，十亿级项目验                     |
|     [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT)     | 2019  | 物联网消息协议，MqttClient/MqttServer，客户端支持阿里云物联网                               |
|      [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT)      | 2022  | IoT标准库，定义物联网领域的各种通信协议标准规范                                             |
|   [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus)   | 2022  | ModbusTcp/ModbusRTU/ModbusASCII，基于IoT标准库实现，支持ZeroIoT平台和IoTEdge网关            |
|  [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens)  | 2022  | 西门子PLC协议，基于IoT标准库实现，支持IoT平台和IoTEdge                                      |
|      [NewLife.Map](https://github.com/NewLifeX/NewLife.Map)      | 2022  | 地图组件库，封装百度地图、高德地图、腾讯地图、天地图                                        |
|    [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio)    | 2023  | 音频编解码库，PCM/ADPCMA/G711A/G722U/WAV/AAC                                                |
|    [NewLife.Video](https://github.com/NewLifeX/NewLife.Video)    | 2026  | 视频编解码库，H.264/H.265/MJPEG/YUV，RTSP/RTMP/HLS/GB28181/ONVIF                           |
|                             产品平台                             |       | 产品平台级，编译部署即用，个性化自定义                                                      |
|         [Stardust](https://github.com/NewLifeX/Stardust)         | 2018  | 星尘，分布式服务平台，节点管理、APM监控中心、配置中心、注册中心、发布中心                   |
|           [AntJob](https://github.com/NewLifeX/AntJob)           | 2019  | 蚂蚁调度，分布式大数据计算平台（实时/离线），蚂蚁搬家分片思想，万亿级数据量项目验证         |
|      [NewLife.ERP](https://github.com/NewLifeX/NewLife.ERP)      | 2021  | 企业ERP，产品管理、客户管理、销售管理、供应商管理                                           |
|         [CrazyCoder](https://github.com/NewLifeX/XCoder)         | 2006  | 码神工具，众多开发者工具，网络、串口、加解密、正则表达式、Modbus、MQTT                      |
|           [EasyIO](https://github.com/NewLifeX/EasyIO)           | 2023  | 简易文件存储，支持分布式系统中文件集中存储。                                                |
|           [XProxy](https://github.com/NewLifeX/XProxy)           | 2005  | 产品级反向代理，NAT代理、Http代理                                                           |
|        [HttpMeter](https://github.com/NewLifeX/HttpMeter)        | 2022  | Http压力测试工具                                                                            |
|         [GitCandy](https://github.com/NewLifeX/GitCandy)         | 2015  | Git源代码管理系统                                                                           |
|          [SmartOS](https://github.com/NewLifeX/SmartOS)          | 2014  | 嵌入式操作系统，完全独立自主，支持ARM Cortex-M芯片架构                                      |
|          [SmartA2](https://github.com/NewLifeX/SmartA2)          | 2019  | 嵌入式工业计算机，物联网边缘网关，高性能.NET8主机，应用于工业、农业、交通、医疗             |
|                          FIoT物联网平台                          | 2020  | 物联网整体解决方案，建筑、环保、农业，软硬件及大数据分析一体化，单机十万级点位项目验证      |
|                        UWB高精度室内定位                         | 2020  | 厘米级（10~20cm）高精度室内定位，软硬件一体化，与其它系统联动，大型展厅项目验证             |

## 新生命开发团队
![XCode](https://newlifex.com/logo.png)  

新生命团队（NewLife）成立于2002年，是新时代物联网行业解决方案提供者，致力于提供软硬件应用方案咨询、系统架构规划与开发服务。  
团队主导的80多个开源项目已被广泛应用于各行业，Nuget累计下载量高达400余万次。  
团队开发的大数据中间件NewLife.XCode、蚂蚁调度计算平台AntJob、星尘分布式平台Stardust、缓存队列组件NewLife.Redis以及物联网平台FIoT，均成功应用于电力、高校、互联网、电信、交通、物流、工控、医疗、文博等行业，为客户提供了大量先进、可靠、安全、高质量、易扩展的产品和系统集成服务。  

我们将不断通过服务的持续改进，成为客户长期信赖的合作伙伴，通过不断的创新和发展，成为国内优秀的IoT服务供应商。  

`新生命团队始于2002年，部分开源项目具有20年以上漫长历史，源码库保留有2010年以来所有修改记录`  
网站：https://newlifex.com  
开源：https://github.com/newlifex  
QQ群：1600800/1600838  
微信公众号：  
![智能大石头](https://newlifex.com/stone.jpg)  
