# NewLife.Video - مكتبة ترميز الفيديو

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md)
<!-- lang:end -->

> قد تتأخر هذه الترجمة عن [النسخة الإنجليزية](README.md).

دعم واسع لمجموعة كبيرة من مرمّزات الفيديو وصيغ الحاويات ومعالجة الإطارات والوسائط المتدفقة وتكييف فيديو إنترنت الأشياء.

المصدر: https://github.com/NewLifeX/NewLife.Video  
NuGet: `NewLife.Video`  

## الميزات

### محرّك الترميز
- فك وترميز H.264/H.265 (غلاف أنبوب FFmpeg، وتمرير مباشر للمرمّز العتادي)
- فك وترميز MJPEG بإدارة كاملة للشيفرة
- فك وترميز VP8/VP9/AV1 (غلاف أنبوب FFmpeg)
- تحويل فضاء الألوان: YUV420P/NV12 ↔ RGB24/BGRA32 (BT.601، مع تسريع SIMD)
- تكييف ترويسات الشرائح: Hisilicon/Dahua/Uniview
- اكتشاف/تنزيل/تخزين مؤقت لبيئة تشغيل FFmpeg تلقائيًا
- واجهة برمجية للتحويل الشامل بأمر واحد: VideoTranscoder

### صيغ الحاويات
| الصيغة | القراءة | الكتابة | ملاحظات |
|------|:--:|:--:|------|
| MP4 (ISO BMFF) | ✅ | ✅ | بنية صناديق ftyp/moov/mdat كاملة |
| FLV | ✅ | ✅ | ترويسة FLV + VideoTag |
| AVI (RIFF) | ✅ | ✅ | بنية avih/strh/strf/movi/idx1 كاملة |
| TS (MPEG-TS) | ✅ | ✅ | PAT/PMT + حزم TS بحجم 188 بايت |
| MKV/WebM (Matroska) | ✅ | ✅ | EBML/Segment/Tracks/Cluster/SimpleBlock |
| إطارات فيديو خام | ✅ | ✅ | YUV420P/NV12/RGB24 |
| fMP4/CMAF (مقسّم) | ❌ | ✅ | Fmp4SegmentWriter للبث المباشر |

### معالجة الإطارات
تغيير الحجم، والقص، والتدوير والانعكاس، وضبط الألوان، والعلامة المائية النصية، وتراكب الطابع الزمني، وتوليد الصور المصغّرة، وكشف الحركة، وتحويل معدل الإطارات.

### صيغ الصور
- إخراج اللقطات: ترميز JPEG/BMP/TIFF/PNG/WebP/GIF بإدارة كاملة للشيفرة، وAVIF عبر أنبوب FFmpeg
- ترميز GIF متحرك بإدارة كاملة للشيفرة (GIF 89a، ضغط LZW داخلي)
- خط أنابيب ترميز دفعي للإطارات: إخراج دفعي متعدد الإطارات + صور مصغّرة شبكية

### الوسائط المتدفقة
- تحزيم/إزالة تحزيم RTP: H.264 (RFC 6184)، H.265 (RFC 7798)
- عميل RTSP: OPTIONS/DESCRIBE/SETUP/PLAY/TEARDOWN
- دفع RTMP: مصافحة + طبقة أوامر AMF0 (connect/createStream/publish)
- تقسيم HLS: توليد قائمة m3u8 + مقاطع TS + تنظيف تلقائي
- JitterBuffer، وقناة فيديو WebSocket

### تشغيل البث
- محرّك التشغيل FfmpegPlayer: RTSP/RTMP/HLS/الملفات المحلية، والإطارات الخام تُصدر عبر الحدث FrameDecoded
- fMP4 عبر WebSocket: Fmp4WebSocketStreamer للتشغيل الفوري في المتصفح بزمن تأخير 1-3 ثوانٍ
- جسر RTSP→WebSocket: تشغيل بثّ كاميرات RTSP في المتصفح
- إدارة كاميرات متعددة: تجمّع اتصالات CameraStreamManager/إعادة اتصال تلقائية/مراقبة صحّة (16+ قناة 1080p لكل عملية)
- عارض إطارات بإدارة كاملة للشيفرة: VideoFrameRenderer يُخرج مخازن ذاكرة BGRA32

### تكييف فيديو إنترنت الأشياء
- ONVIF: اكتشاف الأجهزة عبر WS-Discovery، واسترجاع عنوان بث Media، والتحكم PTZ، والتخزين الطرفي Profile G
- GB/T 28181: تسجيل/نبض SIP، والبث عند الطلب المباشر (INVITE+SDP+RTP)
- جدولة التسجيل: بالمؤقت + بالمشغّلات
- ترويسات فيديو الشرائح: Hisilicon/Dahua/Uniview

### التقاط الأجهزة
- ScreenCapture: التقاط سطح المكتب DXGI على Windows / نهج متوافق عبر المنصات
- دعم خلفيات متعددة: DirectShow / V4L2 / macOS AVFoundation

### عميل سطح المكتب (NewLife.Video.Studio)
- تطبيق Avalonia متكامل يغطي كامل سلسلة الالتقاط والتشغيل والمعالجة والتحويل والبث
- التقاط الكاميرا/الشاشة/الأجهزة، وعارض معلومات الوسائط، ومتصفّح الإطارات
- المعالجة الدفعية، وتحويل الصيغ، وطرفية FFmpeg، والبث المباشر HLS، والتحكم ONVIF PTZ

## التوثيق

| المستند | الوصف |
|------|------|
| [المتطلبات](Doc/需求文档.md) | رؤية المشروع والأهداف الأساسية وتقسيم الوحدات |
| [قائمة الميزات](Doc/功能模块清单.md) | 82 ميزة متتبَّعة عبر التنفيذ والاختبار والتعليقات |
| [التصميم المعماري](Doc/架构设计.md) | المعمارية الطبقية وتفاعل المكوّنات وقرارات التصميم الرئيسية |
| [تحليل المنافسين](Doc/竞品分析报告.md) | مقارنة تنافسية وأوجه التمايز في معالجة الفيديو على .NET |
| [سجل التغييرات](Doc/ChangeLog.md) | تاريخ الإصدارات |

## البدء السريع

```csharp
// 1. استخدام المرمّزات المدمجة
var factory = new VideoCodecFactory(); // يسجّل MJPEG/H.264/H.265/VP8/VP9 تلقائيًا

// 2. قراءة الحاويات وكتابتها
using var writer = VideoContainerFactory.CreateWriter("output.mp4");
writer.WriteMetadata(1920, 1080, VideoTypes.H264, 30);
writer.WriteFrame(new VPacket { Data = new Packet(h264Data), CodecType = VideoTypes.H264 });
// عند Dispose يُكتب الملف كاملًا تلقائيًا

// 3. خط أنابيب معالجة الإطارات
var pipeline = new VideoPipeline();
pipeline.Append(new FrameResizer(640, 480))
       .Append(new TimestampOverlay())
       .Append(new WatermarkOverlay("Camera-01"));

// 4. دفع البث
var client = new RtmpClient("rtmp://server/live/stream");
await client.ConnectAsync();
await client.ConnectStreamAsync();
await client.CreateStreamAsync();
await client.PublishAsync();
await client.PushVideoAsync(new VPacket { Data = new Packet(avccData), CodecType = VideoTypes.H264 });

// 5. اكتشاف أجهزة ONVIF
var discovery = new OnvifDiscovery();
var devices = await discovery.DiscoverAsync();
```

## فلسفة التصميم المعماري

**إدارة كاملة للشيفرة + هجين مع FFmpeg**: العمليات البسيطة (ترميز MJPEG، والتحويل بين YUV وRGB، وتحليل الحاويات، ومعالجة الإطارات) تنفيذات مُدارة بالكامل بلا اعتماديات خارجية. أما المرمّزات المعقّدة (H.264/H.265) فتُفوَّض إلى عمليات FFmpeg.

**واجهة برمجية على مستوى الإطار**: لا تقتصر على تحويل الملفات، بل توفّر إمكانات فك الترميز والترميز والمعالجة لكل إطار على حدة، وهو ما يناسب بثّ الفيديو في الزمن الحقيقي.

**إنترنت الأشياء أولًا**: تكامل عميق مع ONVIF وGB/T 28181 وترويسات شرائح إنترنت الأشياء، وهو ما صُمّم خصيصًا لمراقبة الفيديو وسيناريوهات إنترنت الأشياء.

## أطر العمل المستهدفة

`netstandard2.1` / `netstandard2.0` / `net461` / `net8.0` / `net10.0` — متوافقة مع .NET Framework 4.6.1+ و.NET Core 3.0+ و.NET 5+ وMono.

## خريطة المنظومة

NewLife منظومة متعدّدة الطبقات من مشاريع مفتوحة المصدر تُصدَر بإصدارات مستقلة. الخريطة أدناه مشتركة حرفيًا بين جميع مستودعات NewLife؛ وكل مشروع يرتبط بمستودعه وبحزمته الرئيسية على NuGet.

| المشروع | الوظيفة | التنزيلات |
| --- | --- | --- |
| **الأساس** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | التسجيل والتهيئة والتخزين المؤقت والشبكات والتسلسل والجدولة وتتبّع أداء APM. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | وسيط بيانات: جداول واحدة بمستوى عشرات المليارات من الصفوف، تقسيم تلقائي للجداول، فصل القراءة عن الكتابة، وأكثر من 10 قواعد بيانات. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | مكتبة شبكات عالية الأداء: 1.77 مليون pps / 14.5 Gbps على جهاز واحد (TCP 1KB، 2026-10)، و4 ملايين اتصال TCP طويل الأمد. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | إطار اتصال بروتوكولي وRPC لتطبيقات العميل/الخادم وأجهزة إنترنت الأشياء. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | يثبّت تطبيقات .NET كخدمات حارسة لنظام التشغيل: خدمات Windows وsystemd في Linux. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | قوالب مشاريع `dotnet new` للطرفية وweb وAPI والخدمات والبيانات وإنترنت الأشياء. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **البيانات والمراسلة** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | عميل Redis بزمن استجابة بالميكروثانية، وملايين العمليات في الثانية، وقوائم رسائل مدمجة. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | عميل RocketMQ مُدار بالكامل لـ Apache RocketMQ وخدمة رسائل Alibaba Cloud. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | عميل وخادم MQTT، بما في ذلك الاتصال بـ Alibaba Cloud IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | قاعدة بيانات هجينة بلغة C#: مدمجة أو شبكية، وعلائقية، وسلاسل زمنية، وقائمة رسائل، وKV في واحد. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | برنامج تشغيل MySql وطبقة مساعدة مبنية على مكدّس بيانات NewLife. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web والذكاء الاصطناعي والأنظمة الموزّعة** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | إطار منصة إدارة سريعة: الصلاحيات، والدخول الموحّد SSO، وخادم OAuth، وتوليد واجهات CRUD. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | منصة خدمات موزّعة: إدارة العقد، وAPM، ومركز الإعدادات، ومركز التسجيل، ومركز النشر. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | منصة موزّعة للمهام وجدولة البيانات الفورية، مع التقسيم وإعادة المحاولة وإعادة التعبئة. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | عميل موحّد لـ 46 مزوّد LLM بالإضافة إلى ChatAI، تطبيق ويب جاهز للمحادثة. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **الصناعة وإنترنت الأشياء** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | مكتبة معايير إنترنت الأشياء التي تعرّف عقود البروتوكولات وتجريدات الأجهزة. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | تنفيذ Modbus TCP/RTU/ASCII فوق NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | بروتوكول Siemens PLC فوق NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | بروتوكول Omron PLC فوق NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | بروتوكول Mitsubishi Melsec PLC فوق NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | عميل OPC للوصول إلى البيانات الصناعية. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | مكدّس بروتوكول BACnet لأتمتة المباني. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | منصة إنترنت أشياء مفتوحة المصدر وخفيفة، مع إدارة البوابات والأجهزة. | — |
| **تليماتيكا المركبات** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | بروتوكول تحديد مواقع المركبات JT/T 808: أكثر من 70 جسم رسالة، 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | بروتوكول JT/T 809 بين المنصات لجهات إدارة النقل. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | فيديو داخل المركبة JT/T 1078: تحليل RTP وإعادة التجميع إلى FLV/fMP4 والبث المباشر. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **مكوّنات المجال** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | حزمة واحدة لـ Excel وWord وPPT وPDF وأكثر من 15 صيغة مستندات. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | صوت عبر المنصات: مرمّزات، وإدخال/إخراج الأجهزة، وDSP، وحاويات، وبث. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | مرمّزات الفيديو وصيغ الحاويات ومعالجة الإطارات والبث. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | واجهة موحّدة فوق Baidu وAMap وTencent وTianditu مع الترميز الجغرافي وتحويل الإحداثيات. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | تحديد الموقع الجغرافي لعناوين IPv4 بقاعدة بيانات غير متصلة مُعيّنة في الذاكرة، وواجهة API عبر الإنترنت كبديل. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **الأدوات والمنتجات** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | صندوق أدوات المطوّر: الشبكات والمنفذ التسلسلي والتشفير والتعبيرات النمطية وModbus وMQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | نظام استضافة ذاتية لإدارة الشيفرة المصدرية Git. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | يثبّت بيئة تشغيل .NET المطابقة تلقائيًا عند التشغيل من مضيف أقدم. | — |

> تعرض الشارات الحزمة الرئيسية لكل مشروع؛ أما حزم السمات والإضافات والحزم التابعة فمذكورة في صفحاتها على NuGet. أما الحزم الإضافية التي يتجاوز عددها 30 حزمة وغير المذكورة هنا (FTP وLoRa وإضافات Office وبرامج تشغيل البروتوكولات وغيرها) فمتاحة في [منظمة NewLifeX](https://github.com/NewLifeX) وعلى [newlifex.com](https://newlifex.com).

## الترخيص

MIT License

## الفريق

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

تأسّست NewLife في عام 2002، وأمضت أكثر من عقدين في منظومة .NET وإنترنت الأشياء، وتدير ما يقارب 100 مشروع مفتوح المصدر (34 منها مشاريع رئيسية)، وتقدّم حلولًا متكاملة للبرمجيات والأجهزة لقطاعات الكهرباء والخدمات اللوجستية والرعاية الصحية والتحكّم الصناعي والإنترنت. ونلتزم بمبدأ "لا أسبقية في التعلّم — الإتقان يكسب الاحترام".

نُزّلت المكوّنات مفتوحة المصدر <!--nuget-total:start-->6.48M<!--nuget-total:end--> مرة من NuGet (على مستوى المنظومة بأكملها، حتى 2026-10).

الموقع: https://newlifex.com · المنظمة مفتوحة المصدر: https://github.com/NewLifeX

<details>
<summary>حساب WeChat الرسمي: 智能大石头 (امسح للاشتراك)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>
