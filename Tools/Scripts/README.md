# 构建与本地化脚本

本目录集中存放仓库级脚本，根目录只保留项目入口文件。

| 脚本 | 用途 | 常用命令 |
|------|------|----------|
| `check-locales.ps1` | 词表门禁：键集合、占位符、格式说明符、孤儿键、`L.T`/日志/程序集元数据缺词条；源文扫描含 `.cs` 与 `.csproj` | `pwsh -File Tools/Scripts/check-locales.ps1` |
| `show-locales.ps1` | 人工观察各语言译文，并列出与英文原文相同的未翻译条目 | `pwsh -File Tools/Scripts/show-locales.ps1 -All` |
| `sync-readme-languages.ps1` | 按 `readme-languages.json` 生成各 README 的语言切换行 | `pwsh -File Tools/Scripts/sync-readme-languages.ps1 -Check` |
| `sync-readme-package.ps1` | 从 `README.md` 生成 `README.nuget.md`，并校验 `PackageReadmeFile` | `pwsh -File Tools/Scripts/sync-readme-package.ps1 -Check` |
| `check-readmes.ps1` | 校验 11 份 README 的标题数、代码围栏、链接数、翻译说明与英文标题残留 | `pwsh -File Tools/Scripts/check-readmes.ps1` |

## 本地化校验

```powershell
# 全量词表门禁
pwsh -File Tools/Scripts/check-locales.ps1

# 只看前 10 条；或 -All 输出全部
pwsh -File Tools/Scripts/show-locales.ps1
pwsh -File Tools/Scripts/show-locales.ps1 -All

# 查看指定词条在全部语言中的译文
pwsh -File Tools/Scripts/show-locales.ps1 -Key "Invalid AVI.,NewLife Video Library"
```

单元测试中 `LocalizationTests` 会遍历 10 种语言验证译文、异常消息、带前缀日志与程序集元数据；本地化改动后建议同时运行：

```powershell
dotnet test XUnitTest/XUnitTest.csproj -c Release --filter "FullyQualifiedName~LocalizationTests"
```
