# NewLife.Video - 影片編解碼庫

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> 本翻譯可能落後，以 [English README](README.md) 為準。

支援各式影片編解碼、封裝格式、影格處理、串流媒體與 IoT 影片整合。

原始碼：https://github.com/NewLifeX/NewLife.Video  
NuGet：`NewLife.Video`  

## 功能特色

### 編解碼引擎
- H.264/H.265 解碼與編碼（FFmpeg 管道封裝，支援硬體編碼器直通）
- MJPEG 純受控編解碼
- VP8/VP9/AV1 解碼與編碼（FFmpeg 管道封裝）
- 色彩空間轉換：YUV420P/NV12 ↔ RGB24/BGRA32（BT.601，SIMD 加速）
- 晶片標頭整合：Hisilicon/Dahua/Uniview
- FFmpeg 執行階段自動偵測／下載／快取
- 一鍵轉碼 API：VideoTranscoder

### 封裝格式
| 格式 | 讀取 | 寫入 | 備註 |
|------|:--:|:--:|------|
| MP4 (ISO BMFF) | ✅ | ✅ | 完整的 ftyp/moov/mdat box 結構 |
| FLV | ✅ | ✅ | FLV 標頭 + VideoTag |
| AVI (RIFF) | ✅ | ✅ | 完整的 avih/strh/strf/movi/idx1 結構 |
| TS (MPEG-TS) | ✅ | ✅ | PAT/PMT + 188 位元組 TS 封包 |
| MKV/WebM (Matroska) | ✅ | ✅ | EBML/Segment/Tracks/Cluster/SimpleBlock |
| 原始影片影格 | ✅ | ✅ | YUV420P/NV12/RGB24 |
| fMP4/CMAF（分段） | ❌ | ✅ | Fmp4SegmentWriter，適用於直播串流 |

### 影格處理
尺寸縮放、區域裁剪、旋轉與翻轉、色彩調整、文字浮水印、時間戳記疊加、縮圖產生、移動偵測、影格率轉換。

### 影像格式
- 快照輸出：JPEG/BMP/TIFF/PNG/WebP/GIF 純受控編碼，AVIF 透過 FFmpeg 管道編碼
- 動畫 GIF 純受控編碼（GIF 89a，自研 LZW 壓縮）
- 批次影格編碼管線：多影格批次輸出 + 網格縮圖

### 串流媒體
- RTP 封包化／解封包化：H.264（RFC 6184）、H.265（RFC 7798）
- RTSP 用戶端：OPTIONS/DESCRIBE/SETUP/PLAY/TEARDOWN
- RTMP 推送：握手 + AMF0 命令層（connect/createStream/publish）
- HLS 分段：m3u8 播放清單產生 + TS 分段 + 自動清理
- JitterBuffer、WebSocket 影片通道

### 串流播放
- FfmpegPlayer 播放引擎：支援 RTSP/RTMP/HLS/本機檔案，原始影格經由 FrameDecoded 事件輸出
- fMP4 over WebSocket：Fmp4WebSocketStreamer 實現瀏覽器即時播放，延遲 1-3 秒
- RTSP→WebSocket 橋接：將攝影機 RTSP 串流投放至瀏覽器播放
- 多攝影機管理：CameraStreamManager 連線集區／自動重連／健康監測（單一程序 16+ 路 1080p）
- 純受控影格轉譯器：VideoFrameRenderer 輸出 BGRA32 記憶體緩衝區

### IoT 影片整合
- ONVIF：WS-Discovery 裝置探索、Media 串流位址取得、PTZ 雲台控制、Profile G 邊緣儲存
- GB/T 28181：SIP 註冊／心跳、即時點播（INVITE+SDP+RTP）
- 錄影排程：計時器 + 事件觸發
- 晶片影片標頭：Hisilicon/Dahua/Uniview

### 裝置擷取
- ScreenCapture：Windows DXGI 桌面擷取／跨平台相容方案
- DirectShow / V4L2 / macOS AVFoundation 多後端支援

### 桌面用戶端（NewLife.Video.Studio）
- 以 Avalonia 打造的全功能桌面應用程式，涵蓋擷取、播放、處理、轉碼與串流完整流程
- 攝影機／螢幕／裝置擷取、媒體資訊檢視器、影格瀏覽器
- 批次處理、格式轉換、FFmpeg 主控台、HLS 直播、ONVIF 雲台控制

## 說明文件

| 文件 | 說明 |
|------|------|
| [需求文件](Doc/需求文档.md) | 專案願景、核心目標與模組劃分 |
| [功能清單](Doc/功能模块清单.md) | 82 項功能的實作／測試／註解三維狀態追蹤 |
| [架構設計](Doc/架构设计.md) | 分層架構、元件互動與關鍵設計決策 |
| [競品分析](Doc/竞品分析报告.md) | .NET 影片處理領域的競品比較與差異化定位 |
| [更新日誌](Doc/ChangeLog.md) | 版本發行歷史 |

## 快速開始

```csharp
// 1. 使用內建編解碼器
var factory = new VideoCodecFactory(); // 自動註冊 MJPEG/H.264/H.265/VP8/VP9

// 2. 封裝容器讀寫
using var writer = VideoContainerFactory.CreateWriter("output.mp4");
writer.WriteMetadata(1920, 1080, VideoTypes.H264, 30);
writer.WriteFrame(new VPacket { Data = new Packet(h264Data), CodecType = VideoTypes.H264 });
// Dispose 時自動寫入完整檔案

// 3. 影格處理管線
var pipeline = new VideoPipeline();
pipeline.Append(new FrameResizer(640, 480))
       .Append(new TimestampOverlay())
       .Append(new WatermarkOverlay("Camera-01"));

// 4. 串流推送
var client = new RtmpClient("rtmp://server/live/stream");
await client.ConnectAsync();
await client.ConnectStreamAsync();
await client.CreateStreamAsync();
await client.PublishAsync();
await client.PushVideoAsync(new VPacket { Data = new Packet(avccData), CodecType = VideoTypes.H264 });

// 5. ONVIF 裝置探索
var discovery = new OnvifDiscovery();
var devices = await discovery.DiscoverAsync();
```

## 架構理念

**純受控 + FFmpeg 混合**：簡單操作（MJPEG 編解碼、YUV↔RGB 轉換、封裝格式解析、影格處理）皆為零相依的純受控實作；複雜編解碼（H.264/H.265）則交由 FFmpeg 程序處理。

**影格級 API**：不只侷限於檔案轉碼——提供逐影格解碼／編碼／處理能力，適合即時影片串流。

**IoT 優先**：深度整合 ONVIF、GB/T 28181 與晶片影片標頭，專為影片監控與物聯網場景打造。

## 目標框架

`netstandard2.1` / `netstandard2.0` / `net461` / `net8.0` / `net10.0` —— 相容於 .NET Framework 4.6.1+、.NET Core 3.0+、.NET 5+ 與 Mono。

## 生態版圖

NewLife 生態系按相依分層，各專案獨立版本、獨立發行。下面的版圖在所有 NewLife 儲存庫中逐字一致；每個專案連結到程式碼儲存庫與主 NuGet 套件。

| 專案 | 一句話定位 | 下載量 |
| --- | --- | --- |
| **基礎底座** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | 日誌、設定、快取、網路、序列化、排程與 APM 追蹤。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 資料中介層：單表百億級、自動分表、讀寫分離，支援 10 多種資料庫。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 高效能網路庫：單機 177 萬 pps、14.5 Gbps（TCP 1KB，2026-10 實測），400 萬 Tcp 長連線。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 協定通訊與 RPC 框架，面向 CS 應用程式與物聯網裝置。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 將 .NET 應用程式安裝為作業系統常駐服務：Windows 服務與 Linux systemd。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | `dotnet new` 專案範本：主控台、Web、API、服務、資料與物聯網。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **資料儲存與訊息** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Redis 用戶端：微秒級延遲、百萬級吞吐，內建訊息佇列。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 純受控 RocketMQ 用戶端，支援 Apache RocketMQ 與阿里雲訊息佇列。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT 用戶端與服務端，含阿里雲物聯網接入。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | C# 混合式資料庫：內嵌或網路雙模，關係型、時序、訊息佇列與 KV 於一體。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | 建構於 NewLife 資料堆疊之上的 MySql 驅動與輔助層。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web、AI 與分散式** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 快速後台開發框架：權限、SSO、OAuth 服務端與 CRUD 樣板。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | 分散式服務平台：節點管理、APM、設定中心、註冊中心與發佈中心。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | 分散式任務與即時資料排程平台，支援分片、重試與回溯補算。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | 統一接入 46 家大模型服務商，附帶開箱即用的 ChatAI Web 對話應用。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **工業與物聯網** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 物聯網標準庫，定義通訊協定契約與裝置抽象。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | 建構於 NewLife.IoT 之上的 Modbus TCP/RTU/ASCII 實作。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 建構於 NewLife.IoT 之上的西門子 PLC 協定。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | 建構於 NewLife.IoT 之上的歐姆龍 PLC 協定。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | 建構於 NewLife.IoT 之上的三菱 Melsec PLC 協定。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | 工業資料存取的 OPC 用戶端。 | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet 樓宇自動化協定堆疊。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | 開源輕量級物聯網平台，含閘道與裝置管理。 | — |
| **車聯網** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 車輛定位協定：70+ 訊息體，涵蓋 2011/2013/2019 三個版本。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | JT/T 809 部標平台互聯協定，面向運輸管理部門。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 車載視訊：RTP 解析、FLV/fMP4 轉封裝與直播推送。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **領域元件** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | 一個套件涵蓋 Excel、Word、PPT、PDF 等 15+ 文件格式。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 跨平台音訊：編解碼、裝置 IO、DSP、容器與串流處理。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | 視訊編解碼、封裝格式、影格處理與串流。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 百度／高德／騰訊／天地圖統一介面，支援地理編碼與座標轉換。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 歸屬地：離線記憶體映射資料庫 + 線上 API 雙來源。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **工具與產品** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | 開發者工具箱：網路、序列埠、加解密、正規表示式、Modbus 與 MQTT。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | 自架 Git 原始碼管理系統。 | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | 從較舊的宿主啟動時，自動安裝相符的 .NET 執行階段。 | — |

> 徽章取各專案主套件；主題套件、擴充套件與衛星套件見對應 NuGet 頁面。未在表中列出的 30 多個套件（FTP、LoRa、Office 擴充、各類協定驅動等）見 [NewLifeX 組織](https://github.com/NewLifeX) 與 [newlifex.com](https://newlifex.com)。

## 授權

MIT 授權

## 新生命開發團隊

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

新生命團隊成立於 2002 年，二十餘年專注 .NET 與物聯網技術生態，主導開源專案近百個（生態版圖收錄 34 個主幹專案），為電力、物流、醫療、工控、網際網路等行業提供軟硬體一體化解決方案；以「學無先後達者為師」為信條。

開源元件在 NuGet 累計下載 <!--nuget-total:start-->6.48M<!--nuget-total:end--> 次（全生態口徑，截至 2026-10）。

網站：https://newlifex.com · 開源組織：https://github.com/NewLifeX

<details>
<summary>微信公眾號：智能大石头（掃碼關注）</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>
