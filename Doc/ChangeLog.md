# NewLife.Video 版本更新记录

## v1.3.2026.1024 (2026-10-24)

### 多语言本地化
- **运行时词表 11 种语言**：接入 NewLife.Core 12 编译期词表机制，异常消息与运行期消息全量本地化，缺失译文自动回退英文
- **带前缀日志**：新增统一带前缀日志入口，日志前缀不参与查表，模板可正常翻译
- **程序集元数据本地化**：AssemblyTitle/Description 纳入词表，反射读取后可按当前语言展示
- **README 全语种覆盖**：11 种语言 README 完整翻译，新增结构、徽章与译文完整性门禁

### NativeAOT 与跨平台
- **AOT 兼容**：库侧启用 AOT/裁剪分析器，net8.0/net10.0 目标要求 0 告警
- **AotSmoke 冒烟工程**：22 项纯托管场景冒烟，覆盖编解码、容器、处理管线、流媒体与 IoT
- **移动端冒烟**：新增 Android/iOS 冒烟工程，验证主库在移动端可加载并执行核心 API
- **指令集冒烟**：新增 riscv64/s390x 冒烟工程与 CI 校验
- **多平台测试矩阵**：win-x64/linux-x64/linux-arm64/loongarch64 单元测试，net8.0/net10.0 双目标

### 容器与流媒体修复
- **Mp4Writer 修复**：修正样本描述盒保留区与样本偏移、帧格式，产出的 MP4 可被标准工具与帧级读取解析
- **[fix]** 修复 AMF0 对象结束标记未写入流的问题

### 桌面客户端
- **文件选择统一**：新增跨平台 FileDialogHelper，Studio 各视图补全文件对话框

### 工程与文档
- **解决方案格式**：迁移到 .slnx 新格式
- **依赖升级**：NewLife.Core 12、NewLife.Audio 最新版
- **CI 扩展**：本地化校验、多平台与多指令集兼容矩阵、README 语言一致性校验

---

## v1.2.2026.0802 (2026-08-02)

### 客户端应用
- **NewLife.Video.Studio 桌面客户端**：基于 Avalonia 的全套视频处理应用，覆盖采集、处理、转码、流媒体等可视化操作

### 编解码增强
- **纯托管动画 GIF 编码器**：零外部依赖实现 GIF 动画编码
- **纯托管编码器大规模替换外部依赖**：移除 ImageSharp/SkiaSharp/LibVLCSharp 依赖，以纯托管 + FFmpeg 实现替代，显著降低部署复杂度
- **硬件编码器参数透传**：支持向硬件编码器透传自定义参数
- **AAC 音频支持**：引入 NewLife.Audio 统一 AAC 音频编解码能力

### 容器格式增强
- **fMP4 分段写入器 Fmp4SegmentWriter**：fMP4 分段写入，优化 HLS 直播场景
- **fMP4 盒子结构**：完整 fMP4 盒子序列化与读写支持
- **FLV 读写修复**：修复 FlvReader PreScanFrames skip 逻辑、FlvWriter ScriptTag 缺失 PreviousTagSize
- **H264 解析辅助**：重构容器目录结构，新增 H264 解析辅助与零拷贝支持

### 快照与转码
- **快照格式扩展**：新增 TIFF/GIF/AVIF 快照输出格式
- **VideoTranscoder 转码高级 API**：解码器/编码器分离架构，跨平台兼容

### 流媒体播放
- **播放器增强**：FfmpegPlayer 自动分辨率探测、RTSP TCP 传输、暂停控制与帧率统计
- **摄像头流管理异步化**：CameraStreamManager 异步化重构

### IoT 视频适配
- **ONVIF RecordingClient Profile G**：ONVIF 边缘存储录像能力

### 性能优化
- **PixelFormatConverter SIMD 加速**：NV12/NV21 原生支持 + BT.709 色彩空间转换
- **FrameBatchEncoder 网格缩略图**：批量编码输出网格缩略图

### 工具与部署
- **FFmpegSetup 自动下载**：支持从插件服务器下载 FFmpeg 运行时，新增预下载脚本

### 代码质量
- **[fix]** 修复 VideoTranscoder 解码器/编码器分离与跨平台兼容问题
- **[docs]** 模块编码方案统一重构，需求文档更新至 v1.3

---

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
