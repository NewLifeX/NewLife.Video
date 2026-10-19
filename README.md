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

## Project Matrix

Each project supports net9.0/netstandard2.1/netstandard2.0/net4.62/net4.5 by default; the 2024.0801 release line also supports net4.0/net2.0.

| Project | Since | Description |
| :---: | :---: | ----------- |
| Base Components | | Foundation for other middleware and product projects |
| [NewLife.Core](https://github.com/NewLifeX/X) | 2002 | Core library: logging, configuration, caching, networking, serialization, APM performance tracing |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 2005 | Big-data middleware: 10 billion rows per table, MySql/SQLite/SqlServer/Oracle/PostgreSql/DM, automatic sharding, read/write splitting |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 2005 | Networking library: 22.66 million tps per node, 4 million TCP long connections per node |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2011 | Protocol communication library: C/S application framework, Http/RPC support, high throughput, low overhead for IoT devices |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 2010 | Cube rapid development platform: user permissions, SSO login, OAuth server, validated on 10-billion-row projects |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 2008 | Service management component: installs applications as OS daemons, Windows services and Linux systemd |
| [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero) | 2020 | Zero-generation scaffold: project templates (NewLife.Templates) based on the NewLife ecosystem, Web/WebApi/Service |
| Middleware | | Integration with mainstream middleware platforms |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | 2017 | Redis client: microsecond latency, million-level throughput, rich message queues, validated on 10-billion-data projects |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018 | Pure managed RocketMQ client: Apache RocketMQ and Alibaba Cloud Message Queue, validated on billion-level projects |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | 2019 | IoT messaging protocol: MqttClient/MqttServer, client supports Alibaba Cloud IoT |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 2022 | IoT standard library: communication protocol specifications for the IoT domain |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | 2022 | ModbusTcp/ModbusRTU/ModbusASCII based on the IoT standard library, supports ZeroIoT platform and IoTEdge gateway |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 2022 | Siemens PLC protocol based on the IoT standard library, supports IoT platform and IoTEdge |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 2022 | Map component library: Baidu Maps, Amap, Tencent Maps, Tianditu |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 2023 | Audio codec library: PCM/ADPCMA/G711A/G722U/WAV/AAC |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | 2026 | Video codec library: H.264/H.265/MJPEG/YUV, RTSP/RTMP/HLS/GB28181/ONVIF |
| Product Platforms | | Product-level platforms, ready to compile and deploy with customization |
| [Stardust](https://github.com/NewLifeX/Stardust) | 2018 | Stardust distributed service platform: node management, APM monitoring center, configuration center, registry, release center |
| [AntJob](https://github.com/NewLifeX/AntJob) | 2019 | AntJob distributed big-data computing platform (real-time/offline), sharding-based, validated on trillion-level data projects |
| [NewLife.ERP](https://github.com/NewLifeX/NewLife.ERP) | 2021 | Enterprise ERP: product, customer, sales and supplier management |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | 2006 | CrazyCoder developer toolbox: networking, serial port, encryption, regular expressions, Modbus, MQTT |
| [EasyIO](https://github.com/NewLifeX/EasyIO) | 2023 | Simple file storage for centralized file storage in distributed systems |
| [XProxy](https://github.com/NewLifeX/XProxy) | 2005 | Product-grade reverse proxy: NAT proxy, Http proxy |
| [HttpMeter](https://github.com/NewLifeX/HttpMeter) | 2022 | Http stress testing tool |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | 2015 | Git source code management system |
| [SmartOS](https://github.com/NewLifeX/SmartOS) | 2014 | Embedded operating system, fully independent, supports ARM Cortex-M chip architecture |
| [SmartA2](https://github.com/NewLifeX/SmartA2) | 2019 | Embedded industrial computer and IoT edge gateway, high-performance .NET8 host for industry, agriculture, transportation and healthcare |
| FIoT IoT Platform | 2020 | End-to-end IoT solution for buildings, environmental protection and agriculture; hardware, software and big-data analytics integrated; validated on 100,000-point single-node projects |
| UWB High-Precision Indoor Positioning | 2020 | Centimeter-level (10~20cm) high-precision indoor positioning, integrated hardware and software, interworks with other systems, validated in large exhibition halls |

## License

MIT License

## About NewLife Team

![XCode](https://newlifex.com/logo.png)  

The NewLife Team was founded in 2002 and provides IoT industry solutions, offering hardware/software consulting, system architecture planning and development services.  
More than 80 open-source projects led by the team are widely used across industries, with cumulative NuGet downloads exceeding 4 million.  
The team's big-data middleware NewLife.XCode, AntJob scheduling platform, Stardust distributed platform, NewLife.Redis cache/queue component and FIoT IoT platform have been successfully applied in power, education, internet, telecom, transportation, logistics, industrial control, healthcare and cultural heritage industries, delivering advanced, reliable, secure, high-quality and extensible products and system integration services.  

We will keep improving our services to become a long-term trusted partner, and keep innovating to become an outstanding IoT service provider in China.  

`The NewLife Team started in 2002; some open-source projects have a history of over 20 years, and the source repository keeps all changes since 2010`  
Website: https://newlifex.com  
Open source: https://github.com/newlifex  
QQ Group: 1600800/1600838  
WeChat Official Account:  
![Smart Stone](https://newlifex.com/stone.jpg)  
