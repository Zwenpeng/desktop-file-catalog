# Desktop File Catalog / 桌面文件管家

[English](README.md) | [简体中文](README.zh-CN.md)

A local Windows file catalog for finding files, reviewing exact-content duplicates, and seeing what changed between scans. The application reads the folders you select and does not offer delete, move, or rename actions.

## Download and run

1. Download `DesktopFileCatalog-v1.0.0-win-x64.zip` from the [latest release](https://github.com/Zwenpeng/desktop-file-catalog/releases/latest).
2. Extract the ZIP into a folder where you can save files, such as your Downloads or Documents folder. Do not run the EXE inside the ZIP.
3. Double-click `桌面文件管家.exe`. Choose any folder and select **开始扫描** (Start scan).

The release is a self-contained Windows x64 build: no .NET SDK or runtime installation is required. It needs a 64-bit Windows desktop environment and permission to read the selected folder. It does not require an account, internet connection, or administrator privileges. Windows may show a warning for an unsigned community build; inspect the repository and release checksum before deciding whether to run it.

## Features

- Browse file names, relative paths, extensions, categories, sizes, and modification times in a searchable table.
- Search multiple words together; filter by category or duplicate status and hide common dependency/cache paths.
- Run exact-content duplicate detection on demand: group same-size candidates, then calculate SHA-256 hashes. The displayed redundant size is theoretical, not a deletion recommendation.
- Compare complete scans of the same folder to count added, modified, and removed files. If a scan has read errors, history is not updated and removal counts are not reported.
- Export a full CSV for personal analysis, or a smaller HTML summary for sharing.
- Skip child directory junctions and other reparse points so a linked directory is not repeatedly traversed.
- Cancel long-running scans and duplicate checks.

## Privacy and local data

The application does not upload files, use telemetry, or call an online service. Ordinary scans read file metadata. Duplicate detection reads the contents of same-size candidates locally to calculate hashes.

Complete scan snapshots are saved beside the EXE in `Data/snapshots` as compressed JSON; they contain the selected root path and relative file paths. Up to 10 snapshots are kept per scanned root. The release ZIP does not contain any user's snapshots. When sharing the app, share the original ZIP or EXE, not your local `Data` folder.

The **完整 CSV** (full CSV) contains absolute paths and file names. The **分享摘要** (share summary) omits absolute paths and individual file listings, but includes first-level folder names, category totals, and counts. Review any exported report before sharing it.

## Scope and limitations

- Searches names and paths, not the text inside documents.
- Reports logical file sizes, not physical space recovered by deleting a file. Hard links, sparse files, and cloud placeholders can make these differ.
- Detects exact-content candidates, not visually similar photos or near-duplicate documents.
- The change history compares relative path, size, and modification time; it is not a content-versioning system or a backup.
- A large scan stores its file list in memory. Some protected folders cannot be read without additional permissions; the app reports those boundaries.
- This is a portable app. If you place the EXE in a protected folder such as `Program Files`, it may be unable to save its local history.

## Build from source

Use a Windows x64 machine with the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0):

```powershell
dotnet run --project .\tests\DesktopCatalogApp.IntegrationTests\DesktopCatalogApp.IntegrationTests.csproj -c Release
dotnet publish .\src\DesktopCatalogApp\DesktopCatalogApp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:DebugSymbols=false -o .\artifacts\publish
```

The build command produces `artifacts/publish/桌面文件管家.exe`. It is not necessary to install Visual Studio if the .NET SDK is already installed.

## Project structure

```text
src/DesktopCatalogApp/                 WPF application and scan services
tests/DesktopCatalogApp.IntegrationTests/  Small filesystem integration test
.github/workflows/ci.yml               Windows build and test check
```

See [Privacy](PRIVACY.md), [Security](SECURITY.md), [Contributing](CONTRIBUTING.md), and [Changelog](CHANGELOG.md).

## License

[MIT](LICENSE).
