# NewLife.Video 版本更新记录

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
