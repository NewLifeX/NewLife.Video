# NewLife.Video - Bibliothèque de codecs vidéo

![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.Video?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.Video?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.Video?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.Video?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.Video/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.Video/actions/workflows/test.yml)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-blue)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.Video?logo=github)](https://github.com/NewLifeX/NewLife.Video/graphs/contributors)

<!-- lang:start -->
🌐 [中文](Readme.MD) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> Cette traduction peut être en retard sur la version chinoise.

NewLife.Video fournit multiplexage, démultiplexage et transcodage pour H.264/H.265, MP4, FLV, RTMP et RTSP, pour le streaming et la vidéosurveillance.

## Installation

```bash
dotnet add package NewLife.Video
```

## Démarrage rapide

```csharp
var reader = new Mp4Reader(stream);
var frame = reader.ReadFrame();
```

## Fonctionnalités

- Capacités de streaming et de communication temps réel
- Large couverture des versions de protocoles et formats courants
- Multiplateforme : Windows, Linux, macOS et ARM64/LoongArch64

## Documentation

> 📖 Documentation complète (chinois) : [documents du dépôt](Doc)

## Licence

MIT License © 2002-2026 Équipe NewLife

[GitHub](https://github.com/NewLifeX/NewLife.Video) | [Website](https://newlifex.com)
