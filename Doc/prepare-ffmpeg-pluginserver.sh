#!/bin/bash
# ============================================================
# FFmpeg 插件服务器预下载脚本
# 用途: 从上游下载 FFmpeg 二进制，打包为统一 .zip 格式
#       供 NewLife.Video FFmpegSetup 通过 PluginServer 下载
# 用法: 在插件服务器（Linux）上直接运行
# 输出: 三个 .zip → DEST_DIR
# 依赖: curl, zip, unzip, tar (xz-utils)
# ============================================================
set -euo pipefail

# ---- 配置 ----
# 版本标签，用于生成文件名（如 ffmpeg-win64_v7.0.zip）
# 上游 BtbN 使用 master-latest 持续构建，这里只作标识用，不参与下载
FFMPEG_VERSION="7.0"

# 插件服务器上存放 FFmpeg 的目录（对应 HTML 页面里的 ffmpeg/ 子路径）
DEST_DIR="/var/www/x.newlifex.com/ffmpeg"

# 临时工作目录
WORK_DIR=$(mktemp -d)
trap "rm -rf $WORK_DIR" EXIT

# ---- 前置检查 ----
for cmd in curl unzip zip tar; do
    if ! command -v "$cmd" &>/dev/null; then
        echo "错误: 未找到 $cmd，请先安装。"
        echo "  Ubuntu/Debian: apt install curl unzip zip xz-utils"
        echo "  CentOS/RHEL:   yum install curl unzip zip xz"
        exit 1
    fi
done

echo "============================================"
echo " FFmpeg 插件服务器预下载"
echo " 版本标识: $FFMPEG_VERSION"
echo " 目标目录: $DEST_DIR"
echo " 临时目录: $WORK_DIR"
echo "============================================"
echo ""
cd "$WORK_DIR"

# ============================================================
# 1. Windows (win64) — 来自 BtbN/FFmpeg-Builds
# ============================================================
echo "=== [1/3] Windows win64 ==="
WIN_SRC="ffmpeg-master-latest-win64-gpl.zip"
curl -L -o "$WIN_SRC" \
  "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/$WIN_SRC"

# 解压并提取 ffmpeg.exe 到 zip 根目录（-j 丢弃目录结构）
unzip -q "$WIN_SRC"
zip -j "ffmpeg-win64_v${FFMPEG_VERSION}.zip" \
  "ffmpeg-master-latest-win64-gpl/bin/ffmpeg.exe"
rm -rf ffmpeg-master-latest-win64-gpl
echo "  → ffmpeg-win64_v${FFMPEG_VERSION}.zip 已生成"
echo ""

# ============================================================
# 2. Linux (linux64) — 来自 BtbN/FFmpeg-Builds
# ============================================================
echo "=== [2/3] Linux linux64 ==="
LNX_SRC="ffmpeg-master-latest-linux64-gpl.tar.xz"
curl -L -o "$LNX_SRC" \
  "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/$LNX_SRC"

# 解压 tar.xz，提取 ffmpeg 二进制到 zip 根目录
tar -xJf "$LNX_SRC"
zip -j "ffmpeg-linux64_v${FFMPEG_VERSION}.zip" \
  "ffmpeg-master-latest-linux64-gpl/bin/ffmpeg"
rm -rf ffmpeg-master-latest-linux64-gpl
echo "  → ffmpeg-linux64_v${FFMPEG_VERSION}.zip 已生成"
echo ""

# ============================================================
# 3. macOS (darwin64) — BtbN 不再提供 macOS 构建
#    改用 evermeet.cx 作为上游（或替换为其他来源）
# ============================================================
echo "=== [3/3] macOS darwin64 ==="
echo "  BtbN 不再提供 macOS 构建，尝试从网络获取..."
echo "  如需替换来源，修改 MAC_URL 变量即可。"
echo ""

# macOS 备选 FFmpeg 下载源（按可靠性排序）
# 1. https://evermeet.cx/ffmpeg/           — 社区维护，持续更新
# 2. https://www.osxexperts.net/            — 备用源
# 3. brew install ffmpeg                    — macOS 本地构建
# 4. 自行从 https://ffmpeg.org/download.html 编译
MAC_URLS=(
  "https://evermeet.cx/ffmpeg/ffmpeg-7.0.zip"
  "https://evermeet.cx/ffmpeg/ffmpeg-7.1.zip"
  "https://evermeet.cx/ffmpeg.zip"
)
MAC_FILE="ffmpeg-macos-source.zip"
MAC_DOWNLOADED=false

for url in "${MAC_URLS[@]}"; do
    echo "  尝试: $url"
    if curl -fL -o "$MAC_FILE" "$url" 2>/dev/null; then
        echo "  下载成功"
        MAC_DOWNLOADED=true
        break
    fi
done

if $MAC_DOWNLOADED; then
    # evermeet.cx 的 zip 里 ffmpeg 就在根目录
    unzip -q -o "$MAC_FILE"
    if [ -f "ffmpeg" ]; then
        zip -j "ffmpeg-darwin64_v${FFMPEG_VERSION}.zip" ffmpeg
        rm -f ffmpeg
        echo "  → ffmpeg-darwin64_v${FFMPEG_VERSION}.zip 已生成"
    else
        echo "  ⚠ 解压后未找到 ffmpeg 二进制，跳过 macOS"
    fi
else
    echo "  ⚠ 所有 macOS 下载源均失败。"
    echo "    请手动下载 FFmpeg for macOS 后执行:"
    echo "      zip -j ffmpeg-darwin64_v${FFMPEG_VERSION}.zip /path/to/ffmpeg"
fi
echo ""

# ============================================================
# 4. 计算 MD5 + 部署
# ============================================================
echo "=== MD5 哈希 ==="
for f in ffmpeg-win64_v${FFMPEG_VERSION}.zip \
         ffmpeg-linux64_v${FFMPEG_VERSION}.zip \
         ffmpeg-darwin64_v${FFMPEG_VERSION}.zip; do
    if [ -f "$f" ]; then
        md5sum "$f"
    fi
done
echo ""

echo "=== 部署到 $DEST_DIR ==="
mkdir -p "$DEST_DIR"
cp ffmpeg-*_v${FFMPEG_VERSION}.zip "$DEST_DIR/" 2>/dev/null || true
ls -lh "$DEST_DIR/"
echo ""

echo "============================================"
echo " 完成！"
echo ""
echo "下一步：在插件服务器 HTML 页面中添加以下行："
echo "============================================"
echo ""

for f in ffmpeg-win64_v${FFMPEG_VERSION}.zip \
         ffmpeg-linux64_v${FFMPEG_VERSION}.zip \
         ffmpeg-darwin64_v${FFMPEG_VERSION}.zip; do
    if [ -f "$f" ]; then
        SIZE=$(ls -lh "$f" | awk '{print $5}')
        MD5=$(md5sum "$f" | cut -d' ' -f1)
        echo "<tr>"
        echo "  <td>$(date '+%Y-%m-%d %H:%M:%S')</td><td>${SIZE}</td>"
        echo "  <td><a href=\"ffmpeg/${f}\">${f}</a></td>"
        echo "  <td>${MD5}</td>"
        echo "</tr>"
    fi
done
