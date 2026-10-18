# Contributing / 参与贡献

## English

Issues and pull requests are welcome. Please describe the use case, expected behavior, and Windows version. Use synthetic folder names and test files; never attach a real scan snapshot, full CSV export, credential, or personal document. Keep the application local-first and preserve its no-delete/no-move behavior.

Before a pull request, run the integration test and build the WPF project on Windows with the .NET 10 SDK:

```powershell
dotnet run --project .\tests\DesktopCatalogApp.IntegrationTests\DesktopCatalogApp.IntegrationTests.csproj -c Release
dotnet build .\src\DesktopCatalogApp\DesktopCatalogApp.csproj -c Release
```

## 简体中文

欢迎提交 Issue 和 Pull Request。请写清使用场景、预期表现和 Windows 版本，并使用虚构目录与测试文件；不要上传真实扫描快照、完整 CSV、密钥或个人文档。修改时请保持本地优先，以及不删除、不移动用户文件的设计。

提交前请在 Windows 上使用 .NET 10 SDK 运行上述集成测试和构建命令。
