# NewLife.Video - Video Codec Library for .NET

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
🌐 [中文](Readme.MD) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> English | **[简体中文](Readme.MD)**

A cross-platform .NET video processing library supporting video codecs, container formats, frame processing, streaming media, and IoT video adaptation for the NewLife ecosystem.

**Source**: https://github.com/NewLifeX/NewLife.Video  
**NuGet**: `NewLife.Video`  

---

## Features

### Codec Engine
- **H.264/H.265** Decode & Encode (FFmpeg pipe wrapper)
- **MJPEG** Pure managed codec (zero external dependencies)
- **VP8/VP9/AV1** Decode & Encode (FFmpeg pipe wrapper)
- **Color Space Conversion**: YUV420P/NV12 ↔ RGB24/BGRA32 (BT.601)
- **Chip Header Adaptation**: Hisilicon/Dahua/Uniview IoT chip headers

### Container Formats

| Format | Read | Write | Notes |
|--------|:----:|:-----:|-------|
| MP4 (ISO BMFF) | ✅ | ✅ | Complete ftyp/moov/mdat box structure |
| FLV | ✅ | ✅ | FLV header + VideoTag |
| AVI (RIFF) | ✅ | ✅ | avih/strh/strf/movi/idx1 full structure |
| TS (MPEG-TS) | ✅ | ✅ | PAT/PMT + 188-byte TS packets |
| MKV/WebM (Matroska) | ✅ | ✅ | EBML/Segment/Tracks/Cluster/SimpleBlock |
| Raw Video Frames | ✅ | ✅ | YUV420P/NV12/RGB24 |

### Frame Processing
Resize, crop, rotate & flip, color adjustment, text watermark, timestamp overlay, thumbnail generation, motion detection, frame rate conversion, FFmpeg filter chain integration.

### Streaming Media
- **RTP** Packetization/Depacketization: H.264 (RFC 6184), H.265 (RFC 7798)
- **RTSP Client**: OPTIONS/DESCRIBE/SETUP/PLAY/TEARDOWN
- **RTMP Push**: Handshake + AMF0 command layer (connect/createStream/publish)
- **HLS Segmentation**: m3u8 playlist + TS segments + auto cleanup
- **JitterBuffer**, **WebSocket** video channel

### IoT Video Adaptation
- **ONVIF**: WS-Discovery device discovery, Media (get stream URL), PTZ control
- **GB/T 28181**: SIP registration/heartbeat, live on-demand (INVITE+SDP+RTP)
- **Recording Schedule**: Timer + event-triggered recording

### Device Capture
- **ScreenCapture**: Windows GDI screen capture (net461)
- **DirectShow / V4L2** backends (scaffold, platform-specific)

### Desktop Client (NewLife.Video.Studio)
- Full-featured video processing desktop app built on Avalonia, covering capture, playback, processing, transcoding and streaming
- Camera/screen/device capture, media info viewer, frame browser
- Batch processing, format conversion, FFmpeg console, HLS live streaming, ONVIF PTZ control

---

## Quick Start

```csharp
// 1. Use built-in codecs
var factory = new VideoCodecFactory(); // Auto-registers MJPEG/H.264/H.265/VP8/VP9

// 2. Container read/write
using var writer = VideoContainerFactory.CreateWriter("output.mp4");
writer.WriteMetadata(1920, 1080, VideoTypes.H264, 30);
writer.WriteFrame(new VPacket { Data = new Packet(h264Data), CodecType = VideoTypes.H264 });
// Dispose automatically writes complete file

// 3. Frame processing pipeline
var pipeline = new VideoPipeline();
pipeline.Append(new FrameResizer(640, 480))
       .Append(new TimestampOverlay())
       .Append(new WatermarkOverlay("Camera-01"));

// 4. RTMP push streaming
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

---

## Architecture Philosophy

**Pure Managed + FFmpeg Hybrid**: Simple operations (MJPEG codec, YUV↔RGB conversion, container parsing, frame processing) are zero-dependency pure managed implementations. Complex codecs (H.264/H.265) delegate to FFmpeg processes.

**Frame-Level API**: Not limited to file transcoding — provides per-frame decode/encode/process capabilities, ideal for real-time video streams.

**IoT-First**: Deep integration with ONVIF, GB/T 28181, and IoT chip headers — purpose-built for video surveillance and IoT scenarios.

---

## Project Matrix

The NewLife ecosystem includes the following projects (default targets: net9.0/netstandard2.1/netstandard2.0/net4.62/net4.5):

| Project | Since | Description |
|:-------:|:-----:|-------------|
| [NewLife.Core](https://github.com/NewLifeX/X) | 2002 | Core library: logging, config, caching, networking, serialization, APM |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 2005 | Big-data ORM: 10B rows per table, MySQL/SQLite/SqlServer/Oracle/PostgreSql/DM |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 2005 | Networking: 22.66M tps, 4M TCP connections |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 2010 | Rapid dev platform: user/auth/SSO/OAuth |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | 2017 | Redis client: microsecond latency, million-level throughput |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018 | Pure managed RocketMQ client |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | 2019 | IoT messaging protocol |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 2022 | IoT standard library |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | 2026 | Video codec library |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 2023 | Audio codec library |
| [Stardust](https://github.com/NewLifeX/Stardust) | 2018 | Distributed service platform |
| [AntJob](https://github.com/NewLifeX/AntJob) | 2019 | Distributed big-data computing |

---

## Target Frameworks

`netstandard2.1` / `netstandard2.0` / `net461` — compatible with .NET Framework 4.6.1+, .NET Core 3.0+, .NET 5+, and Mono.

---

## License

MIT License

---

## About NewLife Team

**NewLife Team** was founded in 2002, dedicated to IoT industry solutions. We provide hardware/software consulting, system architecture design, and development services.

Our 80+ open-source projects are widely adopted across power, education, internet, telecom, transportation, logistics, industrial control, healthcare, and cultural heritage industries. NuGet downloads exceed 4 million.

Website: https://newlifex.com  
GitHub: https://github.com/newlifex  
QQ Group: 1600800 / 1600838
