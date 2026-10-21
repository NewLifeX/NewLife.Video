# NewLife.Video - 비디오 코덱 라이브러리

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [العربية](README.ar.md)
<!-- lang:end -->

> 이 번역은 [영어 버전](README.md)보다 늦을 수 있습니다.

광범위한 비디오 코덱, 컨테이너 형식, 프레임 처리, 스트리밍 미디어 및 IoT 비디오 적응을 지원합니다.

소스: https://github.com/NewLifeX/NewLife.Video  
NuGet: `NewLife.Video`  

## 기능

### 코덱 엔진
- H.264/H.265 디코딩 및 인코딩 (FFmpeg 파이프 래퍼, 하드웨어 인코더 패스스루)
- MJPEG 순수 관리 코드 코덱
- VP8/VP9/AV1 디코딩 및 인코딩 (FFmpeg 파이프 래퍼)
- 색 공간 변환: YUV420P/NV12 ↔ RGB24/BGRA32 (BT.601, SIMD 가속)
- 칩 헤더 적응: Hisilicon/Dahua/Uniview
- FFmpeg 런타임 자동 감지/다운로드/캐싱
- 원클릭 트랜스코딩 API: VideoTranscoder

### 컨테이너 형식
| 형식 | 읽기 | 쓰기 | 비고 |
|------|:--:|:--:|------|
| MP4 (ISO BMFF) | ✅ | ✅ | 완전한 ftyp/moov/mdat 박스 구조 |
| FLV | ✅ | ✅ | FLV 헤더 + VideoTag |
| AVI (RIFF) | ✅ | ✅ | 완전한 avih/strh/strf/movi/idx1 구조 |
| TS (MPEG-TS) | ✅ | ✅ | PAT/PMT + 188바이트 TS 패킷 |
| MKV/WebM (Matroska) | ✅ | ✅ | EBML/Segment/Tracks/Cluster/SimpleBlock |
| 원시 비디오 프레임 | ✅ | ✅ | YUV420P/NV12/RGB24 |
| fMP4/CMAF (분할) | ❌ | ✅ | 라이브 스트리밍용 Fmp4SegmentWriter |

### 프레임 처리
크기 조정, 자르기, 회전 및 뒤집기, 색상 조정, 텍스트 워터마크, 타임스탬프 오버레이, 썸네일 생성, 동작 감지, 프레임 레이트 변환.

### 이미지 형식
- 스냅샷 출력: JPEG/BMP/TIFF/PNG/WebP/GIF 순수 관리 코드 인코딩, AVIF는 FFmpeg 파이프 사용
- 애니메이션 GIF 순수 관리 코드 인코딩 (GIF 89a, 자체 LZW 압축)
- 배치 프레임 인코딩 파이프라인: 다중 프레임 배치 출력 + 그리드 썸네일

### 스트리밍 미디어
- RTP 패킷화/디패킷화: H.264 (RFC 6184), H.265 (RFC 7798)
- RTSP 클라이언트: OPTIONS/DESCRIBE/SETUP/PLAY/TEARDOWN
- RTMP 푸시: 핸드셰이크 + AMF0 명령 계층 (connect/createStream/publish)
- HLS 분할: m3u8 플레이리스트 생성 + TS 세그먼트 + 자동 정리
- JitterBuffer, WebSocket 비디오 채널

### 스트리밍 재생
- FfmpegPlayer 재생 엔진: RTSP/RTMP/HLS/로컬 파일, FrameDecoded 이벤트로 원시 프레임 전달
- WebSocket 기반 fMP4: Fmp4WebSocketStreamer로 브라우저 실시간 재생, 지연 1~3초
- RTSP→WebSocket 브리지: 카메라 RTSP 스트림을 브라우저에서 재생
- 다중 카메라 관리: CameraStreamManager 연결 풀/자동 재연결/상태 모니터링 (프로세스당 1080p 16채널 이상)
- 순수 관리 코드 프레임 렌더러: VideoFrameRenderer가 BGRA32 메모리 버퍼 출력

### IoT 비디오 적응
- ONVIF: WS-Discovery 장치 검색, Media 스트림 URL 조회, PTZ 제어, Profile G 에지 스토리지
- GB/T 28181: SIP 등록/하트비트, 라이브 온디맨드 (INVITE+SDP+RTP)
- 녹화 스케줄: 타이머 + 이벤트 트리거
- 칩 비디오 헤더: Hisilicon/Dahua/Uniview

### 장치 캡처
- ScreenCapture: Windows DXGI 데스크톱 캡처 / 크로스 플랫폼 호환 방식
- DirectShow / V4L2 / macOS AVFoundation 다중 백엔드 지원

### 데스크톱 클라이언트 (NewLife.Video.Studio)
- 캡처, 재생, 처리, 트랜스코딩, 스트리밍 전 과정을 아우르는 풀기능 Avalonia 데스크톱 애플리케이션
- 카메라/화면/장치 캡처, 미디어 정보 뷰어, 프레임 브라우저
- 배치 처리, 형식 변환, FFmpeg 콘솔, HLS 라이브 스트리밍, ONVIF PTZ 제어

## 문서

| 문서 | 설명 |
|------|------|
| [요구사항](Doc/需求文档.md) | 프로젝트 비전, 핵심 목표 및 모듈 구성 |
| [기능 목록](Doc/功能模块清单.md) | 구현/테스트/주석 3개 축으로 추적하는 82개 기능 |
| [아키텍처](Doc/架构设计.md) | 계층 아키텍처, 컴포넌트 상호작용 및 핵심 설계 결정 |
| [경쟁 분석](Doc/竞品分析报告.md) | .NET 비디오 처리 분야의 경쟁 비교와 차별화 |
| [변경 로그](Doc/ChangeLog.md) | 릴리스 이력 |

## 빠른 시작

```csharp
// 1. 내장 코덱 사용
var factory = new VideoCodecFactory(); // MJPEG/H.264/H.265/VP8/VP9 자동 등록

// 2. 컨테이너 읽기/쓰기
using var writer = VideoContainerFactory.CreateWriter("output.mp4");
writer.WriteMetadata(1920, 1080, VideoTypes.H264, 30);
writer.WriteFrame(new VPacket { Data = new Packet(h264Data), CodecType = VideoTypes.H264 });
// Dispose 시 완전한 파일이 자동으로 기록됩니다

// 3. 프레임 처리 파이프라인
var pipeline = new VideoPipeline();
pipeline.Append(new FrameResizer(640, 480))
       .Append(new TimestampOverlay())
       .Append(new WatermarkOverlay("Camera-01"));

// 4. 스트리밍 푸시
var client = new RtmpClient("rtmp://server/live/stream");
await client.ConnectAsync();
await client.ConnectStreamAsync();
await client.CreateStreamAsync();
await client.PublishAsync();
await client.PushVideoAsync(new VPacket { Data = new Packet(avccData), CodecType = VideoTypes.H264 });

// 5. ONVIF 장치 검색
var discovery = new OnvifDiscovery();
var devices = await discovery.DiscoverAsync();
```

## 아키텍처 철학

**순수 관리 코드 + FFmpeg 하이브리드**: 단순한 작업(MJPEG 코덱, YUV↔RGB 변환, 컨테이너 파싱, 프레임 처리)은 의존성이 없는 순수 관리 코드로 구현합니다. 복잡한 코덱(H.264/H.265)은 FFmpeg 프로세스에 위임합니다.

**프레임 단위 API**: 파일 트랜스코딩에 그치지 않고 프레임 단위 디코딩/인코딩/처리 기능을 제공하므로 실시간 비디오 스트림에 이상적입니다.

**IoT 우선**: ONVIF, GB/T 28181, IoT 칩 헤더와 깊이 통합되어 영상 보안과 IoT 시나리오에 맞춰 설계되었습니다.

## 대상 프레임워크

`netstandard2.1` / `netstandard2.0` / `net461` / `net8.0` / `net10.0` — .NET Framework 4.6.1+, .NET Core 3.0+, .NET 5+ 및 Mono와 호환됩니다.

## 생태계 지도

NewLife는 독립적으로 버전이 관리되는 오픈소스 프로젝트의 계층형 생태계입니다. 아래 지도는 모든 NewLife 리포지토리에서 그대로 동일하게 공유되며, 각 프로젝트는 코드 리포지토리와 기본 NuGet 패키지로 연결됩니다.

| 프로젝트 | 한 줄 소개 | 다운로드 |
| --- | --- | --- |
| **기반** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | 로깅, 구성, 캐시, 네트워크, 직렬화, 스케줄링, APM 추적. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 데이터 미들웨어: 단일 테이블 수백억 건, 자동 분할 테이블, 읽기/쓰기 분리, 10여 종 데이터베이스 지원. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 고성능 네트워크 라이브러리: 단일 노드 177만 pps, 14.5 Gbps(TCP 1KB, 2026-10 실측), 400만 Tcp 장기 연결. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | CS 애플리케이션과 IoT 장치를 위한 프로토콜 통신 및 RPC 프레임워크. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | .NET 애플리케이션을 운영체제 데몬으로 설치: Windows 서비스와 Linux systemd. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | 콘솔, Web, API, 서비스, 데이터, IoT용 `dotnet new` 프로젝트 템플릿. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **데이터 및 메시징** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Redis 클라이언트: 마이크로초 수준 지연, 백만 수준 처리량, 메시지 큐 내장. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Apache RocketMQ와 알리바바 클라우드 메시지 큐를 지원하는 순수 관리형 RocketMQ 클라이언트. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT 클라이언트와 서버, 알리바바 클라우드 IoT 연결 포함. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | C# 하이브리드 데이터베이스: 임베디드/네트워크 두 모드, 관계형, 시계열, 메시지 큐, KV를 하나로. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | NewLife 데이터 스택 위에 구축한 MySql 드라이버와 도우미 계층. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, AI 및 분산** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 빠른 관리 플랫폼 프레임워크: 권한, SSO, OAuth 서버, CRUD 스캐폴딩. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | 분산 서비스 플랫폼: 노드 관리, APM, 구성 센터, 등록 센터, 배포 센터. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | 샤딩, 재시도, 백필을 지원하는 분산 작업 및 실시간 데이터 스케줄링 플랫폼. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | 46개 LLM 공급자를 통합 연결하는 클라이언트와 바로 사용할 수 있는 ChatAI 웹 채팅 애플리케이션. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **산업 및 IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 프로토콜 계약과 장치 추상을 정의하는 IoT 표준 라이브러리. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | NewLife.IoT 기반으로 구현한 Modbus TCP/RTU/ASCII. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | NewLife.IoT 기반으로 구현한 지멘스 PLC 프로토콜. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | NewLife.IoT 기반으로 구현한 오므론 PLC 프로토콜. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | NewLife.IoT 기반으로 구현한 미쓰비시 Melsec PLC 프로토콜. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | 산업 데이터 수집용 OPC 클라이언트. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet 빌딩 자동화 프로토콜 스택. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | 게이트웨이와 장치 관리를 갖춘 경량 오픈소스 IoT 플랫폼. | — |
| **차량 텔레매틱스** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 차량 위치 프로토콜: 70개 이상 메시지 본문, 2011/2013/2019 지원. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | 운송 관리 부서를 위한 JT/T 809 플랫폼 간 프로토콜. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 차량용 비디오: RTP 파싱, FLV/fMP4 리먹싱, 라이브 스트리밍. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **도메인 컴포넌트** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Excel, Word, PPT, PDF 등 15개 이상 문서 형식을 하나의 패키지로. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 크로스 플랫폼 오디오: 코덱, 장치 IO, DSP, 컨테이너, 스트리밍. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | 비디오 코덱, 컨테이너 형식, 프레임 처리, 스트리밍. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 바이두/고덕/텐센트/톈디투 지도 통합 인터페이스, 지오코딩과 좌표 변환 지원. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 위치 정보: 오프라인 메모리 매핑 데이터베이스와 온라인 API 이중 소스. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **도구 및 제품** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | 개발자 도구 모음: 네트워크, 시리얼 포트, 암호화, 정규식, Modbus, MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | 셀프 호스팅 Git 소스 코드 관리 시스템. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | 구버전 호스트에서 시작할 때 일치하는 .NET 런타임을 자동으로 설치합니다. | — |

> 배지는 각 프로젝트의 기본 패키지를 나타냅니다. 테마, 확장, 위성 패키지는 해당 NuGet 페이지에서 확인하세요.여기에 나열하지 않은 30여 개 패키지(FTP, LoRa, Office 확장, 각종 프로토콜 드라이버 등)는 [NewLifeX 조직](https://github.com/NewLifeX)과 [newlifex.com](https://newlifex.com)에서 확인할 수 있습니다.

## 라이선스

MIT License

## 팀

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

뉴라이프 팀(NewLife)은 2002년에 설립되어 20여 년간 .NET과 IoT 기술 생태계에 집중하며 100여 개의 오픈소스 프로젝트를 주도하고(이 중 34개가 핵심 프로젝트), 전력, 물류, 의료, 산업 제어, 인터넷 등 산업에 소프트웨어와 하드웨어를 아우르는 통합 솔루션을 제공해 왔습니다. "학문에는 선후가 없으며, 이룬 사람이 스승이다"라는 신조를 따릅니다.

오픈소스 컴포넌트는 NuGet에서 누적 <!--nuget-total:start-->6.48M<!--nuget-total:end-->회 다운로드되었습니다(생태계 전체 기준, 2026-10).

웹사이트: https://newlifex.com · 오픈소스 조직: https://github.com/NewLifeX

<details>
<summary>위챗 공식 계정: 智能大石头(스캔하여 팔로우)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>
