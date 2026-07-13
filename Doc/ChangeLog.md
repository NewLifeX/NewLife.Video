# NewLife.Video 版本更新记录

## v1.1.2026.0713 (2026-07-13)

### 硬件编码器参数透传
- **M1-16 硬件编码器参数透传**：H.264/H.265 编码器新增 `encoderName` 和 `extraArgs` 参数，支持指定 h264_nvenc/hevc_nvenc/h264_qsv 等硬件编码器及自定义编码参数
- **FFmpegSetup**：FFmpeg 运行时自动检测/下载/缓存工具类，降低入门门槛
- **VideoTranscoder**：一键转码高级 API，支持格式转换/分辨率缩放/编码器切换/码率控制/进度回调

### 图像格式扩展
- **M4-6 快照格式扩展**：新增 TIFF/GIF/AVIF 三格式快照输出，基于 FFmpeg 管道编码
- **M4-7 ImageSharp 互操作**：VideoFrame ↔ Image&lt;Rgba32&gt; 双向转换
- **M4-8 SkiaSharp 互操作**：VideoFrame ↔ SKBitmap 双向转换，支持 AVIF/HEIF/JPEG XL 编码
- **M4-9 批量帧编码管线**：FrameBatchEncoder 帧序列批量编码 + 网格缩略图生成

### fMP4 分段写入器
- **M2-15 Fmp4SegmentWriter**：基于 ISO BMFF Box 模型的 fMP4/CMAF 分段写入器，支持直播录制和 DASH 兼容流
- 修复 FlvReader PreScanFrames skip 逻辑 Bug
- 修复 FlvWriter ScriptTag 缺失 PreviousTagSize

### 流式播放模块
- **M9-1 VlcPlayer**：LibVLCSharp 集成封装，支持 RTSP/RTMP/HLS/DASH 一键播放及 WPF/WinForms/MAUI 控件
- **M9-2 Fmp4WebSocketStreamer**：fMP4 over WebSocket 实时播放服务端，低延迟 1-3 秒
- **M9-4 CameraStreamManager**：多路摄像头 RTSP 连接池管理，支持自动重连和健康监测

### IoT 增强
- **M7-2 ONVIF Profile G**：OnvifRecordingClient 边缘存储客户端，支持 GetRecordings/CreateRecording/DeleteRecording
- 修复跨目标平台兼容性（net461/netstandard2.0/netstandard2.1）

## v1.1.2026.0702 (2026-07-02)

### AV1 编解码
- **AV1 解码器 FFmpegAV1Decoder**：通过管道接收 AV1 裸流，输出 YUV420P 原始帧，IVF封装自动检测
- **AV1 编码器 FFmpegAV1Encoder**：libsvtav1 优先回退 libaom-av1，支持 CRF 质量与编码速度预设
- **VP8/VP9 编码器**：补全 VP8/VP9 编码器实现，与解码器形成完整编解码闭环

### 容器格式增强
- **MKV/WebM 写入器 MkvWriter**：EBML/Segment/Tracks/Cluster/SimpleBlock 完整写入，支持 H.264/H.265
- **TS 写入器 TsWriter**：PAT/PMT 生成 + PES 打包，支持 H.264/H.265 双编码
- **MP4 写入器完善**：avcC/hvcC 配置盒写入，仅关键帧 stss 优化
- **FLV 写入器增强**：AVCDecoderConfigurationRecord 序列头自动写入
- **MKV 读取器增强**：CodecPrivate 回退宽高 + 关键帧标志修正

### 帧处理增强
- **FFmpeg 滤镜链集成 FFmpegFilterProcessor**：支持所有 -vf 滤镜链，与纯托管 VideoPipeline 可互换
- **快照输出 SnapshotEncoder**：帧快照支持 JPEG 纯托管/PNG/WebP/BMP 四格式输出
- **帧序列编码增强**：支持 H.264/H.265/MJPEG 三种编码格式输出

### 流媒体增强
- **RTMP 推流增强**：完整 Amf0Encoder 实现、onMetaData 自动发送、序列头写入、Ping 保活
- **FLV 元数据**：支持 ScriptTag (onMetaData) 写入

### IoT 视频适配
- **GB/T 28181 完整实现**：SIP REGISTER 注册 + 心跳 Keep-Alive + INVITE 实时点播
- **ONVIF PTZ 控制**：绝对/连续移动 + 预置位 + 停止
- **录像计划管理**：定时录像 + 事件触发录像

### 代码质量
- **[fix]** 统一视频数据包为 IPacket，全面适配 ArrayPacket
- **[fix]** 修复 TimestampOverlay 死代码，补全时间默认值
- **[fix]** 简化 MKV 写入流程，增强 FFmpeg 调用健壮性
- **[fix]** 清理空 BasicTest、废弃 Test 项目、空 Speech/DSP 目录
- **[perf]** 重构测试用例结构，分拆为独立模块文件，335 个测试全量通过

## v1.1.2026.0629 (2026-06-29)

### 编解码引擎
- **H.264/H.265 编解码**：基于 FFmpeg 管道封装，支持解码与编码双向
- **MJPEG 纯托管编解码**：零外部依赖的 MJPEG 编解码实现
- **VP8/VP9 编解码**：FFmpeg 管道封装的 VPx 系列编解码支持
- **颜色空间转换**：YUV420P/NV12 ↔ RGB24/BGRA32 (BT.601)
- **芯片头适配**：海思、大华、宇视等主流 IoT 芯片私有头解析

### 容器格式
- **MP4 (ISO BMFF)**：完整的 ftyp/moov/mdat 盒结构读写
- **FLV**：FLV 头 + VideoTag 格式完整读写
- **AVI (RIFF)**：avih/strh/strf/movi/idx1 完整 RIFF 结构读写
- **TS (MPEG-TS)**：PAT/PMT + 188 字节 TS 包读写
- **MKV/WebM (Matroska)**：EBML/Segment/Tracks/Cluster/SimpleBlock 完整解析与写入
- **原始视频帧**：YUV420P/NV12/RGB24 裸帧读写
- **格式检测**：VideoFormatDetector 自动识别容器与编码格式
- **工厂模式**：CreateWriter/CreateReader 统一创建读写器

### 帧处理
- **尺寸缩放**：FrameResizer 任意分辨率缩放
- **区域裁剪**：FrameCropper 精确区域裁剪
- **旋转翻转**：FrameRotator 90°/180°/270° 旋转及镜像翻转
- **色彩调整**：ColorAdjuster 亮度/对比度/饱和度调整
- **文字水印**：WatermarkOverlay 自定义文字叠加
- **时间戳叠加**：TimestampOverlay OSD 时间显示
- **缩略图生成**：关键帧提取与缩略图输出
- **移动侦测**：MotionDetector 帧差法移动检测
- **帧率转换**：FrameRateConverter 任意帧率互转

### 流媒体
- **RTP 封包/解包**：H.264 (RFC 6184)、H.265 (RFC 7798) 标准实现
- **RTSP 客户端**：OPTIONS/DESCRIBE/SETUP/PLAY/TEARDOWN 完整协议支持
- **RTMP 推流**：握手 + AMF0 命令层，支持 connect/createStream/publish
- **HLS 分段**：m3u8 生成 + TS 分段 + 自动清理过期分片
- **JitterBuffer**：网络抖动缓冲，平滑视频播放
- **WebSocket 视频通道**：基于 WebSocket 的实时视频传输

### IoT 视频适配
- **ONVIF 设备发现**：WS-Discovery 协议自动搜索局域网内 ONVIF 设备
- **ONVIF Media/PTZ**：获取流地址、云台控制
- **GB/T 28181**：SIP 注册/心跳、实时点播（INVITE+SDP+RTP）
- **录像调度**：定时录像 + 事件触发录像计划

### 设备采集
- **ScreenCapture**：Windows 下 Graphics.CopyFromScreen 屏幕捕获（net461）
- **DirectShow/V4L2**：设备采集后端骨架

### 代码质量
- **[fix]** 修复容器魔术字节检测逻辑
- **[fix]** Dispose 守卫完善，补充 IDisposable 实现防止资源泄漏
- 集成测试覆盖编解码、容器读写、流媒体等核心路径

---
