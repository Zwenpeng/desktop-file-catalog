# Privacy / 隐私

## English

- All scanning and SHA-256 hashing happen locally. The app has no network client, account system, or telemetry. File symbolic links and child directory reparse points are skipped.
- Scan snapshots under `Data/snapshots` contain the chosen root path and relative file paths. They are generated on the user's computer and excluded from Git.
- Full CSV exports contain absolute paths. HTML share summaries contain first-level folder names but no absolute paths or per-file list. Users should review exports before sharing.
- The release package contains only program files and documentation, never scan results from the maintainer's computer.

## 简体中文

- 扫描和 SHA-256 计算都在本机完成，程序没有网络客户端、账号系统或遥测。文件符号链接及子目录重解析点会被跳过。
- `Data/snapshots` 中的历史快照包含所选根路径与相对文件路径，由用户自己的电脑生成，并被 Git 排除。
- 完整 CSV 包含绝对路径；HTML 分享摘要包含一级文件夹名，但不含绝对路径和逐文件列表。分享前请自行检查。
- 发布包只包含程序和说明文件，不包含维护者电脑的扫描结果。
