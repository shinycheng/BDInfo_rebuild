# BDInfo 项目架构文档

> 本文档记录 BDInfo 项目中每个文件的作用和职责。

---

## 项目结构

```
BDInfo.Core/
├── BDInfo.Core.sln              # 解决方案文件
├── BDCommon/                    # 共享库（数据模型、扫描状态、工具）
│   ├── BDCommon.csproj
│   ├── Cloner.cs                # 深拷贝工具（用于 CmdOptions 克隆）
│   ├── ScanBDROMState.cs        # 扫描进度状态（TotalBytes, FinishedBytes, StreamFile, PlaylistMap, Exception）
│   ├── ScanBDROMResult.cs       # 扫描结果（ScanException, FileExceptions）
│   ├── ToolBox.cs               # 通用工具函数（FormatFileSize 等）
│   └── rom/                     # 蓝光光盘数据模型
│       ├── BDInfoSettings.cs    # 设置抽象基类（定义所有配置属性接口）
│       ├── BDROM.cs             # 蓝光光盘主模型（Scan() 解析 BDMV 结构，持有 PlaylistFiles, StreamFiles 等）
│       ├── TSStreamFile.cs      # Stream 文件模型 — Scan(playlists, true) 执行实际的流分析
│       ├── TSPlaylistFile.cs    # 播放列表解析（ClearBitrates, StreamClips 管理）
│       ├── TSStreamClip.cs      # Stream clip 模型（PacketSize, BitRate, Length 等属性）
│       ├── TSStreamClipFile.cs  # Clip 文件模型
│       ├── TSStreamBuffer.cs    # 流解析缓冲区
│       ├── TSStream.cs          # 流基类（Video/Audio/Graphics/Text 子类型）
│       ├── TSInterleavedFile.cs # SSIF 交错文件
│       ├── TSCodec*.cs          # 各编码解析器（AVC, HEVC, MPEG2, VC1, MVC, AC3, TrueHD, DTS, DTSHD, AAC, LPCM, MPA, PGS）
│       ├── LanguageCodes.cs     # 语言代码映射表
│       └── IO/                  # I/O 工具（文件系统抽象）
├── BDInfo/                      # 主程序（命令行入口）
│   ├── BDInfo.csproj
│   ├── Program.cs               # 主入口：薄编排层（111 行），CLI 解析 → 初始化 → 扫描
│   ├── BDROMInitializer.cs      # [新] BDROM 初始化（创建实例、注册错误回调、打印光盘信息）
│   ├── BDROMScanner.cs          # [新] 扫描编排（准备、串行扫描、进度报告、完成处理）
│   ├── ReportGenerator.cs       # [新] 报告生成（.bdinfo 格式输出，含 Forums Paste 和 Stream Diagnostics）
│   ├── CmdOptions.cs            # 命令行参数定义（使用 CommandLine 库）
│   └── BDSettings.cs            # BDInfoSettings 的具体实现（从 CmdOptions 读取值）
├── BDExtractor/                 # ISO 提取工具（不在此次重构范围内）
└── BDInfoDataSubstractor/       # 数据提取器（不在此次重构范围内）
```

---

## 核心数据流（Phase 1 之后）

```
Main(args) → Exec(opts)
  局部变量: error, debug, bdinfoSettings（无静态可变字段）
  → BDROMInitializer.InitBDROM(path, settings, errorLogPath) → 返回 BDROM 实例
  → BDROMScanner.ScanBDROM(bdrom, settings, productVersion, errorLogPath, debugLogPath)
    → ScanBDROMWork(): 逐个扫描每个 TSStreamFile（当前串行）
      → streamFile.Scan(playlists, true): CPU 密集型流分析
    → ScanBDROMCompleted(): 输出扫描完成信息，触发报告生成
      → ReportGenerator.GenerateReport(): 基于扫描结果生成 .bdinfo 报告文件
```

---

## 关键类职责

| 类 | 文件 | 行数 | 职责 |
|---|---|---|---|
| `Program` | `BDInfo/Program.cs` | 111 | 薄编排层：CLI 解析 → 初始化 → 扫描，无静态可变字段 |
| `BDROMInitializer` | `BDInfo/BDROMInitializer.cs` | 113 | 创建 BDROM 实例、注册错误回调、打印光盘信息 |
| `BDROMScanner` | `BDInfo/BDROMScanner.cs` | 205 | 扫描编排、进度报告、完成处理、触发报告生成 |
| `ReportGenerator` | `BDInfo/ReportGenerator.cs` | 1088 | 报告格式化输出（从原 Generate() 原样搬迁） |
| `BDROM` | `BDCommon/rom/BDROM.cs` | — | 蓝光光盘模型，`Scan()` 解析目录结构 |
| `TSStreamFile` | `BDCommon/rom/TSStreamFile.cs` | — | 单个 .m2ts 流文件，`Scan()` 做 CPU 密集型解析 |
| `TSPlaylistFile` | `BDCommon/rom/TSPlaylistFile.cs` | — | 播放列表，持有 StreamClips 和 bitrate 数据 |
| `ScanBDROMState` | `BDCommon/ScanBDROMState.cs` | — | 扫描运行时状态（共享可变，需重构） |
| `ScanBDROMResult` | `BDCommon/ScanBDROMResult.cs` | — | 扫描结果收集（异常信息） |
| `BDInfoSettings` | `BDCommon/rom/BDInfoSettings.cs` | — | 配置抽象基类 |
| `BDSettings` | `BDInfo/BDSettings.cs` | — | 配置具体实现 |
| `CmdOptions` | `BDInfo/CmdOptions.cs` | — | CLI 参数定义 |

---

## Phase 1 关键设计决策

1. **无静态可变字段**: `Program.cs` 中所有状态（`BDROM`, `ScanResult`, `_bdinfoSettings`, `_error`, `_debug`）改为 `Exec()` 内的局部变量，通过参数传递给各模块。
2. **参数化依赖**: 每个模块通过方法参数接收依赖（`BDInfoSettings`, 日志路径, `BDROM` 实例），为后续并行化奠定无全局共享状态的基础。
3. **错误/调试日志分离**: `errorLogPath` 用于异常记录，`debugLogPath` 用于报告生成调试日志，两者在 `BDROMScanner.ScanBDROM` 签名中明确区分。
4. **ReportGenerator 保持原样**: 1088 行虽超规范（300 行限制），但 Phase 1 原则为"不改逻辑"，该方法为纯格式化代码，后续可拆分为子方法。
