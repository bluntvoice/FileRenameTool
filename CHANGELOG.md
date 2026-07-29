# Changelog

本项目的所有重要变更均记录于此。格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。

## [Unreleased]

## [2.3] - 2026-07-29

### Added

- 新增右上角图钉按钮，可随时切换窗口置顶状态。
- 新增空白 DOCX 创建功能，可按当前命名选项生成规范文件名的 `.docx` 格式空白文档。
- 新增“仅保留文件名、日期和版本号”清理模式；缺少日期或版本号时会自动补充。

### Changed

- 公司简称与包含英文字母的版本类型组合时自动加入一个空格，中文版本类型继续保持紧凑连接。
- “仅保留文件名”“仅保留版本号”“仅保留文件名、日期和版本号”采用互斥选择。
- 创建空白 DOCX 时，如果目标文件名已存在，会自动递增版本号，避免覆盖已有文件。

### Fixed

- 修复新建空白 DOCX 对话框在高 DPI 缩放下标签与输入框重叠的问题。
- 调整新建提示文案，明确生成的是 `.docx` 格式的空白文档。

## [2.2] - 2026-07-22

### Added

- 新增系统托盘常驻，关闭主窗口后可继续在后台运行。
- 新增“仅保留版本号”模式，可输出“文件名-v版本号”。

### Changed

- “仅保留文件名”和“仅保留版本号”采用互斥选择。

## [2.1] - 2026-07-18

### Changed

- 优化启动加载流程，使窗口内容准备完成后一次性显示。
- 预加载并复用“关于”窗口，改善首次打开时的卡顿。

## [2.0] - 2026-07-18

### Changed

- 当前版本升级为 v2.0，集中交付文件命名、只读处理、单文件快速编辑和界面优化。
- 软件图标更新。
- 更新公开说明，明确适用场景、使用方式和通用重命名工具推荐。

[Unreleased]: https://github.com/bluntvoice/FileRenameTool/compare/v2.3...HEAD
[2.3]: https://github.com/bluntvoice/FileRenameTool/compare/v2.2...v2.3
[2.2]: https://github.com/bluntvoice/FileRenameTool/compare/v2.1...v2.2
[2.1]: https://github.com/bluntvoice/FileRenameTool/compare/v2.0...v2.1
[2.0]: https://github.com/bluntvoice/FileRenameTool/releases/tag/v2.0
