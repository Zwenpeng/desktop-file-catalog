# 桌面文件管家 / Desktop File Catalog

[English](README.md) | [简体中文](README.zh-CN.md)

一个在 Windows 本机运行的文件目录工具，帮你查找文件、查看内容相同的文件，以及比较两次扫描之间的变化。它读取你选择的文件夹，不提供删除、移动或重命名操作。

## 下载并使用

1. 到[最新版本页面](https://github.com/Zwenpeng/desktop-file-catalog/releases/latest)下载 `DesktopFileCatalog-v1.0.1-win-x64.zip`。
2. 将 ZIP 解压到有写入权限的文件夹，例如“下载”或“文档”。不要直接在压缩包内运行 EXE。
3. 双击 `桌面文件管家.exe`，选择文件夹，再点击“开始扫描”。

发布包是 Windows x64 自包含版本，接收者不必安装 .NET SDK 或运行时。需要 64 位 Windows 桌面环境和目标文件夹的读取权限；不需要账号、联网或管理员权限。个人开源软件通常没有代码签名，Windows 可能显示未知发布者提示；可先查看本仓库源码和发布校验值，再决定是否运行。

## 能做什么

- 在表格中查看文件名、相对路径、扩展名、分类、大小和修改时间。
- 多关键词同时搜索，按分类或重复状态筛选，隐藏常见依赖和缓存路径。
- 手动执行精确查重：先筛选同大小文件，再计算 SHA-256。显示的“理论冗余”不是删除建议；如有候选文件无法核对，会提示结果不完整。
- 比较同一文件夹两次完整扫描，统计新增、修改和移除数量。若本次存在读取错误，不更新历史，也不报告移除数量。
- 导出包含全部扫描记录的 CSV（不受当前搜索和筛选影响）供本人分析，或导出较简洁的 HTML 摘要用于分享。
- 跳过文件符号链接，以及子目录中的 Junction 等重解析点，避免沿联接读取其他位置或重复扫描。
- 取消耗时较长的扫描和查重任务。

## 隐私和本地数据

程序不会上传文件、不包含遥测，也不调用在线服务。普通扫描读取文件元数据；只有执行精确查重时，才会在本机读取同大小候选文件的内容以计算哈希。

完整扫描快照以压缩 JSON 保存在 EXE 旁的 `Data/snapshots`，包含所选根目录和文件的相对路径；每个根目录最多保留 10 份。发布 ZIP 不包含任何用户的扫描快照。分享软件时，请发送原始 ZIP 或 EXE，不要发送自己生成的 `Data` 文件夹。

“完整 CSV”含绝对路径和文件名。“分享摘要”不列绝对路径及逐个文件，但会显示一级目录名称、分类容量和数量。发送任何导出报告前都建议自己先看一遍。

## 功能边界

- 搜索文件名和路径，不搜索 Word、PDF 等文件的正文。
- 显示的是文件逻辑大小；硬链接、稀疏文件、云占位文件等情况下，它不等于实际可释放的磁盘空间。
- 查找内容相同的候选文件，不识别相似照片或内容近似的文档。
- 历史比较依据相对路径、大小和修改时间；“移除”表示本次完整扫描中不再出现，不必然代表文件已被物理删除。它不是内容版本管理或备份。
- 大目录的文件列表会占用内存。无权读取的目录会显示在扫描边界中。
- 程序在 EXE 旁保存历史；若放入 `Program Files` 等受保护目录，可能无法写入历史。

## 从源码构建

在 Windows x64 电脑上安装 [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)：

```powershell
dotnet run --project .\tests\DesktopCatalogApp.IntegrationTests\DesktopCatalogApp.IntegrationTests.csproj -c Release
dotnet publish .\src\DesktopCatalogApp\DesktopCatalogApp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:DebugSymbols=false -o .\artifacts\publish
```

生成的程序为 `artifacts/publish/桌面文件管家.exe`。安装 .NET SDK 后，不需要额外安装 Visual Studio。

## 项目结构

```text
src/DesktopCatalogApp/                 WPF 界面和扫描服务
tests/DesktopCatalogApp.IntegrationTests/  小型文件系统集成测试
.github/workflows/ci.yml               Windows 构建和测试检查
```

其他说明：[隐私](PRIVACY.md)、[安全](SECURITY.md)、[参与贡献](CONTRIBUTING.md)、[更新记录](CHANGELOG.md)。

## 开源许可

[MIT](LICENSE)。
