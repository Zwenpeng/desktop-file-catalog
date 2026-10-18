# Changelog / 更新记录

## v1.0.1 — 2026-10-18

### English

- Skip file-level reparse points during scans and reject files changed into links before duplicate checking.
- Make the full CSV include every scanned record even when the table is filtered; show incomplete duplicate-check results in the app and HTML summary.
- Replace a project-specific search example with neutral wording and clarify scan/share boundaries.

### 简体中文

- 扫描时跳过文件级重解析点；查重前再次检查文件是否被替换成链接。
- 完整 CSV 不受表格筛选影响；应用与 HTML 摘要都会提示查重读取不完整。
- 把带个人项目主题的搜索示例换成中性文字，并明确扫描与分享边界。

## v1.0.0 — 2026-10-18

### English

- Initial public Windows x64 release with local inventory, search, exact-content duplicate review, scan history, and CSV/HTML exports.
- Preserve drive-root paths and avoid updating history from incomplete scans.
- Add bilingual documentation, privacy notes, and a Windows build check.

### 简体中文

- 首次公开发布 Windows x64 版本：本地文件清单、搜索、内容查重、扫描历史、CSV/HTML 导出。
- 修正磁盘根目录路径处理；读取不完整时不覆盖历史基线。
- 增加中英文说明、隐私说明和 Windows 构建检查。
