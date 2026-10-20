# NewLife.Video - 视频编解码库

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/NewLife.Video?logo=github)


![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.Video?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.Video?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.Video?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.Video?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.Video/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.Video/actions/workflows/test.yml)
[![locales](https://img.shields.io/badge/locales-11-2ea44f?logo=dotnet)](Doc/多语言本地化.md)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20arm64%20%7C%20loongarch64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.Video?logo=github)](https://github.com/NewLifeX/NewLife.Video/graphs/contributors)

<!-- lang:start -->
🌐 [English](README.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> **[English](README.md)** | 简体中文

支持各种视频编解码、容器格式、帧处理、流媒体传输和IoT视频适配

源码： https://github.com/NewLifeX/NewLife.Video  
Nuget：NewLife.Video  

> ℹ️ 本中文版与 [English README](README.md) 同批维护；若两者内容不一致，以英文版为对外口径。

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

## 架构理念

**纯托管 + FFmpeg 混合**：简单操作（MJPEG 编解码、YUV↔RGB 转换、容器解析、帧处理）为零依赖纯托管实现；复杂编码（H.264/H.265）交给 FFmpeg 进程处理。

**帧级 API**：不止于文件转码——提供逐帧解码/编码/处理能力，适合实时视频流。

**IoT 优先**：深度集成 ONVIF、GB/T 28181 与芯片视频头，面向视频监控与物联网场景。

## 目标框架

`netstandard2.1` / `netstandard2.0` / `net461` / `net8.0` / `net10.0` —— 兼容 .NET Framework 4.6.1+、.NET Core 3.0+、.NET 5+ 与 Mono。

## 生态版图

NewLife 生态按依赖分层，各项目独立版本、独立发版。下面的版图在所有 NewLife 仓库中逐字一致；每个项目链接到代码仓库与主 NuGet 包。

| 项目 | 一句话定位 | 下载量 |
| --- | --- | --- |
| **基础底座** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | 日志、配置、缓存、网络、序列化、定时调度与 APM 追踪。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 数据中间件：单表百亿级、自动分表、读写分离，支持 10 多种数据库。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 高性能网络库：单机 177 万 pps、14.5 Gbps（TCP 1KB，2026-10 实测），400 万 Tcp 长连接。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 协议通信与 RPC 框架，面向 CS 应用与物联网设备。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 把 .NET 应用安装为操作系统守护进程：Windows 服务与 Linux systemd。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | `dotnet new` 项目模板：控制台、Web、WebApi、服务、数据与物联网。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **数据存储与消息** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Redis 客户端：微秒级延迟、百万级吞吐，内置消息队列。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 纯托管 RocketMQ 客户端，支持 Apache RocketMQ 与阿里云消息队列。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT 客户端与服务端，客户端支持阿里云物联网。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | C# 混合数据库：嵌入/网络双模，关系型、时序、消息队列与 KV 于一体。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | MySql 驱动与数据访问辅助层，基于 NewLife 数据栈实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web、AI 与分布式** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 魔方快速开发平台：用户权限、SSO、OAuth 服务端与 CRUD 脚手架。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | 星尘分布式服务平台：节点管理、APM、配置中心、注册中心与发布中心。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | 蚂蚁调度：分布式任务与实时数据平台，支持分片、重试与回溯补算。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | 统一接入 46 家大模型服务商，附带开箱即用的 ChatAI Web 对话应用。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **工业与物联网** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | IoT 标准库，定义物联网通信协议契约与设备抽象。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Modbus TCP/RTU/ASCII，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 西门子 PLC 协议，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | 欧姆龙 PLC 协议，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | 三菱 Melsec PLC 协议，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | OPC 客户端，用于工业数据采集。 | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet 楼宇自控协议栈。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | 开源轻量级 IoT 平台，含网关与设备管理。 | — |
| **车联网** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 车辆定位协议：70+ 消息体，覆盖 2011/2013/2019 三个版本。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | JT/T 809 部标平台互联协议，面向运输管理部门。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 车载视频：RTP 解析、FLV/fMP4 转封装与直播推送。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **领域组件** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | 一个包覆盖 Excel、Word、PPT、PDF 等 15+ 文档格式。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 跨平台音频：编解码、设备 IO、DSP、容器与流式处理。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | 视频编解码、封装格式、帧处理与流媒体。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 百度/高德/腾讯/天地图统一接口，支持地理编码与坐标转换。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 归属地：离线内存映射库 + 在线 API 双源。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **工具与产品** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | 码神工具：网络、串口、加解密、正则、Modbus 与 MQTT。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | 自托管 Git 源代码管理系统。 | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | 从低版本宿主启动时，自动安装匹配的 .NET 运行时。 | — |

> 徽章取各项目主包；主题包、扩展包与卫星包见对应 NuGet 页面。未在表中列出的 30 多个包（FTP、LoRa、Office 扩展、各类协议驱动等）见 [NewLifeX 组织](https://github.com/NewLifeX) 与 [newlifex.com](https://newlifex.com)。

## 许可证

MIT 许可证

## 新生命开发团队

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

新生命团队成立于 2002 年，二十余年专注 .NET 与物联网技术生态，主导开源项目近百个（生态版图收录 34 个主干项目），为电力、物流、医疗、工控、互联网等行业提供软硬件一体化解决方案；以“学无先后达者为师”为信条。

开源组件在 NuGet 累计下载 <!--nuget-total:start-->6.48M<!--nuget-total:end--> 次（全生态口径，截至 2026-10）。

网站：https://newlifex.com · 开源组织：https://github.com/NewLifeX

<details>
<summary>微信公众号：智能大石头（扫码关注）</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>
