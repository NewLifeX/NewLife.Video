# M7-IoT视频适配

> 版本：v1.0 | 日期：2026-06-29
> 需求对应：[需求文档](需求文档.md) 第 8 章 | 功能清单：[功能模块清单](功能模块清单.md)

---

## 1. 模块职责

| 职责 | 说明 |
|---|---|
| ONVIF 设备发现 | 通过 WS-Discovery 协议在局域网内发现 ONVIF 摄像头 |
| ONVIF 媒体服务 | 获取摄像头媒体配置（Profile/Stream URI/Snapshot URI） |
| ONVIF PTZ 控制 | 云台控制（旋转、变焦、预置位） |
| GB/T 28181 接入 | 国标设备注册、心跳、实时点播 |
| 录像计划 | 定时录像、事件触发录像、录像文件管理 |
| IoT 芯片视频头 | 海思/大华/宇视的视频帧专有头适配 |

---

## 2. 核心组件

| 组件 | 说明 |
|---|---|
| `OnvifDiscovery` | ONVIF WS-Discovery：UDP 组播发现 + XAddrs 解析 |
| `OnvifMediaClient` | ONVIF 媒体服务：GetProfiles/GetStreamUri/GetSnapshotUri |
| `OnvifPtzClient` | ONVIF PTZ 服务：AbsoluteMove/RelativeMove/ContinuousMove/GotoPreset |
| `Gb28181Registrar` | 国标注册：SIP REGISTER + 心跳 Keep-Alive |
| `Gb28181Player` | 国标实时点播：SIP INVITE 拉流 |
| `RecordingScheduler` | 录像计划管理：定时/事件触发/分段管理 |
| `HisiliconVideoHeader` | 海思视频帧头 |
| `DahuaVideoHeader` | 大华视频帧头 |
| `UniviewVideoHeader` | 宇视视频帧头 |

---

## 3. 关键流程

### 3.1 ONVIF 设备发现

```mermaid
sequenceDiagram
    participant App as 调用方
    participant Disc as OnvifDiscovery
    participant Net as 局域网

    App->>Disc: DiscoverAsync(timeout: 5000ms)
    Disc->>Net: UDP 组播 WS-Discovery Probe (239.255.255.250:3702)
    Net-->>Disc: ProbeMatch (设备1: XAddrs=192.168.1.64/onvif/device_service)
    Net-->>Disc: ProbeMatch (设备2: XAddrs=192.168.1.65/onvif/device_service)
    Disc->>Disc: 解析 XAddrs，提取设备地址
    Disc-->>App: OnvifDeviceInfo[] (IP, 服务地址, 类型, Scope)
```

### 3.2 GB/T 28181 实时点播

```mermaid
sequenceDiagram
    participant App as 调用方
    participant Gb as Gb28181Player
    participant Device as 国标设备

    App->>Gb: PlayAsync(deviceId, streamType)
    Gb->>Device: SIP INVITE (sdp: 媒体参数)
    Device-->>Gb: 200 OK (sdp: 设备媒体参数)
    Gb->>Device: ACK
    loop RTP 流
        Device-->>Gb: RTP 视频包
        Gb->>Gb: RTP 解包 → VPacket
        App->>Gb: ReadFrame()
        Gb-->>App: VPacket
    end
    App->>Gb: StopAsync()
    Gb->>Device: SIP BYE
```

### 3.3 芯片视频头适配

```mermaid
flowchart TD
    A[收到视频数据] --> B{遍历注册的 IVideoChipHeader}
    B -->|HisiliconHeader| C{4字节头 0x48495349?}
    C -->|是| D[去除4字节头]
    C -->|否| B
    B -->|DahuaHeader| E{6字节头?}
    E -->|是| F[去除6字节头]
    E -->|否| B
    B -->|UniviewHeader| G{4字节头?}
    G -->|是| H[去除4字节头]
    G -->|否| B
    B -->|所有尝试失败| I[保持原始数据]
    D --> J[输出纯编码数据]
    F --> J
    H --> J
    I --> J
```

---

## 4. 设计决策

| 决策 | 理由 |
|---|---|
| ONVIF 基于 WS-Discovery + SOAP | 行业标准协议，无需反向工程 |
| GB/T 28181 基于 SIP | 国标强制要求，SIP 信令 + RTP 媒体 |
| 芯片头与 M1 共享接口 | `IVideoChipHeader` 统一定义，M7 提供具体实现，M1 工厂全局注册 |
| PTZ 操作为独立组件 | 云台控制与视频流无关，独立使用更灵活 |
| 录像计划不绑定特定存储 | 预留存储接口，支持本地文件/NAS/云存储 |

---

（完）
