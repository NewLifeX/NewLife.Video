# NewLife.Video - Video Codec Library

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
🌐 [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> English | **[简体中文](README.zh-CN.md)**

Support for a wide range of video codecs, container formats, frame processing, streaming media and IoT video adaptation.

Source: https://github.com/NewLifeX/NewLife.Video  
NuGet: `NewLife.Video`  

## Features

### Codec Engine
- H.264/H.265 decode & encode (FFmpeg pipe wrapper, hardware encoder passthrough)
- MJPEG pure managed codec
- VP8/VP9/AV1 decode & encode (FFmpeg pipe wrapper)
- Color space conversion: YUV420P/NV12 ↔ RGB24/BGRA32 (BT.601, SIMD accelerated)
- Chip header adaptation: Hisilicon/Dahua/Uniview
- FFmpeg runtime auto-detection/download/caching
- One-click transcoding API: VideoTranscoder

### Container Formats
| Format | Read | Write | Notes |
|------|:--:|:--:|------|
| MP4 (ISO BMFF) | ✅ | ✅ | Complete ftyp/moov/mdat box structure |
| FLV | ✅ | ✅ | FLV header + VideoTag |
| AVI (RIFF) | ✅ | ✅ | Complete avih/strh/strf/movi/idx1 structure |
| TS (MPEG-TS) | ✅ | ✅ | PAT/PMT + 188-byte TS packets |
| MKV/WebM (Matroska) | ✅ | ✅ | EBML/Segment/Tracks/Cluster/SimpleBlock |
| Raw video frames | ✅ | ✅ | YUV420P/NV12/RGB24 |
| fMP4/CMAF (segmented) | ❌ | ✅ | Fmp4SegmentWriter for live streaming |

### Frame Processing
Resize, crop, rotate & flip, color adjustment, text watermark, timestamp overlay, thumbnail generation, motion detection, frame rate conversion.

### Image Formats
- Snapshot output: JPEG/BMP/TIFF/PNG/WebP/GIF pure managed encoding, AVIF via FFmpeg pipe
- Animated GIF pure managed encoding (GIF 89a, in-house LZW compression)
- Batch frame encoding pipeline: multi-frame batch output + grid thumbnails

### Streaming Media
- RTP packetization/depacketization: H.264 (RFC 6184), H.265 (RFC 7798)
- RTSP client: OPTIONS/DESCRIBE/SETUP/PLAY/TEARDOWN
- RTMP push: handshake + AMF0 command layer (connect/createStream/publish)
- HLS segmentation: m3u8 playlist generation + TS segments + automatic cleanup
- JitterBuffer, WebSocket video channel

### Streaming Playback
- FfmpegPlayer playback engine: RTSP/RTMP/HLS/local files, raw frames emitted via the FrameDecoded event
- fMP4 over WebSocket: Fmp4WebSocketStreamer for real-time browser playback with 1-3 second latency
- RTSP→WebSocket bridge: camera RTSP streams played in the browser
- Multi-camera management: CameraStreamManager connection pool/auto reconnect/health monitoring (16+ 1080p channels per process)
- Pure managed frame renderer: VideoFrameRenderer outputs BGRA32 memory buffers

### IoT Video Adaptation
- ONVIF: WS-Discovery device discovery, Media stream URL retrieval, PTZ control, Profile G edge storage
- GB/T 28181: SIP registration/heartbeat, live on-demand (INVITE+SDP+RTP)
- Recording schedule: timer + event-triggered
- Chip video headers: Hisilicon/Dahua/Uniview

### Device Capture
- ScreenCapture: Windows DXGI desktop capture / cross-platform compatible approach
- DirectShow / V4L2 / macOS AVFoundation multi-backend support

### Desktop Client (NewLife.Video.Studio)
- Full-featured Avalonia desktop app covering the entire capture, playback, processing, transcoding and streaming workflow
- Camera/screen/device capture, media info viewer, frame browser
- Batch processing, format conversion, FFmpeg console, HLS live streaming, ONVIF PTZ control

## Documentation

| Document | Description |
|------|------|
| [Requirements](Doc/需求文档.md) | Project vision, core goals and module breakdown |
| [Feature List](Doc/功能模块清单.md) | 82 features tracked across implementation/testing/comments |
| [Architecture](Doc/架构设计.md) | Layered architecture, component interaction and key design decisions |
| [Competitive Analysis](Doc/竞品分析报告.md) | Competitive comparison and differentiation in .NET video processing |
| [Change Log](Doc/ChangeLog.md) | Release history |

## Quick Start

```csharp
// 1. Use built-in codecs
var factory = new VideoCodecFactory(); // Auto-registers MJPEG/H.264/H.265/VP8/VP9

// 2. Container read/write
using var writer = VideoContainerFactory.CreateWriter("output.mp4");
writer.WriteMetadata(1920, 1080, VideoTypes.H264, 30);
writer.WriteFrame(new VPacket { Data = new Packet(h264Data), CodecType = VideoTypes.H264 });
// Dispose automatically writes the complete file

// 3. Frame processing pipeline
var pipeline = new VideoPipeline();
pipeline.Append(new FrameResizer(640, 480))
       .Append(new TimestampOverlay())
       .Append(new WatermarkOverlay("Camera-01"));

// 4. Streaming push
var client = new RtmpClient("rtmp://server/live/stream");
await client.ConnectAsync();
await client.ConnectStreamAsync();
await client.CreateStreamAsync();
await client.PublishAsync();
await client.PushVideoAsync(new VPacket { Data = new Packet(avccData), CodecType = VideoTypes.H264 });

// 5. ONVIF device discovery
var discovery = new OnvifDiscovery();
var devices = await discovery.DiscoverAsync();
```

## Architecture Philosophy

**Pure Managed + FFmpeg Hybrid**: Simple operations (MJPEG codec, YUV↔RGB conversion, container parsing, frame processing) are zero-dependency pure managed implementations. Complex codecs (H.264/H.265) delegate to FFmpeg processes.

**Frame-Level API**: Not limited to file transcoding — provides per-frame decode/encode/process capabilities, ideal for real-time video streams.

**IoT-First**: Deep integration with ONVIF, GB/T 28181, and IoT chip headers — purpose-built for video surveillance and IoT scenarios.

## Target Frameworks

`netstandard2.1` / `netstandard2.0` / `net461` / `net8.0` / `net10.0` — compatible with .NET Framework 4.6.1+, .NET Core 3.0+, .NET 5+, and Mono.

## Ecosystem Map

NewLife is a layered ecosystem of independently versioned open-source projects. The map below is shared verbatim by every NewLife repository; each project links to its repository and its primary NuGet package.

| Project | What it does | Downloads |
| --- | --- | --- |
| **Foundation** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | Logging, configuration, caching, networking, serialization, scheduling and APM tracing. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | Data middleware: single tables at the tens-of-billions scale, automatic sharding, read-write splitting, 10+ databases. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | High-performance network library: 1.77M pps / 14.5 Gbps on a single machine (TCP 1KB, 2026-10), 4M long-lived TCP connections. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | Protocol communication and RPC framework for client/server applications and IoT devices. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | Installs .NET applications as OS daemons: Windows services and Linux systemd. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | `dotnet new` project templates for console, web, API, service, data and IoT projects. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **Data & Messaging** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Redis client with microsecond latency, millions of ops/s and built-in message queues. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Pure managed RocketMQ client for Apache RocketMQ and Alibaba Cloud Message Queue. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT client and server, including Alibaba Cloud IoT connectivity. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | Hybrid database in C#: embedded or networked, relational, time-series, message queue and KV in one. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | MySql driver and helper layer built on the NewLife data stack. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, AI & Distributed** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | Rapid admin-platform framework: permissions, SSO, OAuth server and CRUD scaffolding. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | Distributed service platform: node management, APM, configuration center, registry and release center. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | Distributed task and real-time data scheduling platform with sharding, retries and backfill. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | Unified client for 46 LLM providers plus ChatAI, a ready-to-run web chat application. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **Industrial & IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | IoT standard library defining protocol contracts and device abstractions. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Modbus TCP/RTU/ASCII implementation on top of NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | Siemens PLC protocol on top of NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | Omron PLC protocol on top of NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | Mitsubishi Melsec PLC protocol on top of NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | OPC client for industrial data access. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet building-automation protocol stack. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | Lightweight open-source IoT platform with gateway and device management. | — |
| **Vehicle Telematics** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 vehicle positioning protocol: 70+ message bodies, 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | JT/T 809 platform-to-platform protocol for transport authorities. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 in-vehicle video: RTP parsing, FLV/fMP4 remuxing and live streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **Domain Components** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | One package for Excel, Word, PPT, PDF and 15+ document formats. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | Cross-platform audio: codecs, device I/O, DSP, containers and streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | Video codecs, container formats, frame processing and streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | Unified interface over Baidu, AMap, Tencent and Tianditu with geocoding and coordinate conversion. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 geolocation with an offline memory-mapped database and an online API fallback. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **Tools & Products** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | Developer toolbox: network, serial port, crypto, regex, Modbus and MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | Self-hosted Git source code management system. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | Installs the matching .NET runtime automatically when launched from an older host. | — |

> Badges show the primary package of each project; theme, extension and satellite packages are listed on their NuGet pages.The 30+ packages not listed here (FTP, LoRa, Office extensions, protocol drivers and more) are available under the [NewLifeX organization](https://github.com/NewLifeX) and on [newlifex.com](https://newlifex.com).

## License

MIT License

## Team

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

Founded in 2002, the NewLife team has spent more than two decades on the .NET and IoT ecosystem, maintains nearly 100 open-source projects (34 of them core to this map), and delivers integrated software and hardware solutions for power, logistics, healthcare, industrial control and internet industries. We live by the motto "learning has no seniority — mastery earns respect".

Open-source components have been downloaded <!--nuget-total:start-->6.48M<!--nuget-total:end--> times from NuGet (ecosystem-wide, as of 2026-10).

Website: https://newlifex.com · Open source organization: https://github.com/NewLifeX

<details>
<summary>WeChat Official Account: 智能大石头 (scan to follow)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>
