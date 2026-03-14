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
│   ├── ScanBDROMState.cs        # 扫描准备数据（TotalBytes, PlaylistMap — 初始化后只读）
│   ├── ScanBDROMResult.cs       # 扫描结果（ScanException, FileExceptions 使用 ConcurrentDictionary）
│   ├── ThreadSafeProgressReporter.cs  # [NEW] 线程安全进度报告器（Interlocked 累加 + 500ms 节流）
│   ├── ToolBox.cs               # 通用工具函数（FormatFileSize 等）
│   └── rom/                     # 蓝光数据模型
│       ├── BDInfoSettings.cs    # 设置抽象基类（含 MaxThreads 属性）
│       ├── BDROM.cs             # 蓝光光盘主模型（Scan() 解析 BDMV 结构）
│       ├── TSStreamFile.cs      # Stream 文件模型 — Scan() 含 lock(playlist) 保护
│       ├── TSPlaylistFile.cs    # 播放列表解析（ClearBitrates, StreamClips 管理）
│       ├── TSStreamClip.cs      # Stream clip 模型
│       ├── TSStreamClipFile.cs  # Clip 文件模型
│       ├── TSStreamBuffer.cs    # 流解析缓冲区
│       ├── TSStream.cs          # 流基类（Video/Audio/Graphics/Text 子类型）
│       ├── TSInterleavedFile.cs # SSIF 交错文件
│       ├── TSCodec*.cs          # 各编码解析器
│       ├── LanguageCodes.cs     # 语言代码映射表
│       └── IO/                  # I/O 工具（文件系统抽象）
├── BDInfo/                      # 主程序（命令行入口）
│   ├── BDInfo.csproj
│   ├── Program.cs               # 主入口：薄编排层（152 行，含 ProcessSingleDisc）
│   ├── BDROMInitializer.cs      # BDROM 初始化
│   ├── BDROMScanner.cs          # 扫描编排（Parallel.ForEach 并行扫描，152 行）
│   ├── ReportGenerator.cs       # 报告生成（.bdinfo 格式输出）
│   ├── CmdOptions.cs            # 命令行参数定义（含 -t/--threads）
│   └── BDSettings.cs            # BDInfoSettings 具体实现（含 MaxThreads）
├── BDExtractor/                 # ISO 提取工具（不在此次重构范围内）
└── BDInfoDataSubstractor/       # 数据提取器（不在此次重构范围内）
```

---

## 核心数据流（Phase 4 之后）

```
Main(args) → Exec(opts)
  局部变量: error, debug, bdinfoSettings（无静态可变字段）
  → 判断是否多光盘
  → 单盘: BDROMInitializer.InitBDROM() → BDROMScanner.ScanBDROM()
  → 多盘: Parallel.ForEach(sortedItems, MaxDegreeOfParallelism) →
      ProcessSingleDisc(subDir, originalOpts, isIsoLevel):
        1. Clone opts → 独立 CmdOptions + BDSettings + error/debug log
        2. BDROMInitializer.InitBDROM() → BDROM 实例
        3. BDROMScanner.ScanBDROM() → 内部 Parallel.ForEach(streamFiles)
        4. 返回 reportPath
      → ConcurrentDictionary 收集 reportPath
    → 串行合并报告（按原始排序）

  BDROMScanner.ScanBDROM():
    → ScanBDROMWork():
      1. 准备阶段（串行）: 计算 TotalBytes, 构建 PlaylistMap, ClearBitrates
      2. 创建 ThreadSafeProgressReporter(totalBytes)
      3. Parallel.ForEach(streamFiles, MaxDegreeOfParallelism = settings.MaxThreads):
         → streamFile.Scan(playlists, true)  ← CPU 密集型，lock(playlist) 保护写回
         → catch → scanResult.FileExceptions[name] = ex  ← ConcurrentDictionary
         → finally → reporter.ReportFileCompleted(fileBytes)  ← Interlocked.Add
      4. reporter.RenderFinal()
    → ScanBDROMCompleted(): 输出完成信息 → ReportGenerator.GenerateReport()
```

---

## 关键类职责

| 类 | 文件 | 行数 | 职责 |
|---|---|---|---|
| `Program` | `BDInfo/Program.cs` | 152 | 薄编排层：CLI 解析 → 初始化 → 扫描 + 多盘并行 |
| `BDROMInitializer` | `BDInfo/BDROMInitializer.cs` | 113 | 创建 BDROM 实例、注册错误回调 |
| `BDROMScanner` | `BDInfo/BDROMScanner.cs` | 152 | 并行扫描编排（Parallel.ForEach） |
| `ReportGenerator` | `BDInfo/ReportGenerator.cs` | 1088 | 报告格式化输出 |
| `ThreadSafeProgressReporter` | `BDCommon/ThreadSafeProgressReporter.cs` | 78 | 线程安全进度报告（Interlocked + 500ms 节流） |
| `ScanBDROMState` | `BDCommon/ScanBDROMState.cs` | 9 | 扫描准备数据（TotalBytes, PlaylistMap — 只读） |
| `ScanBDROMResult` | `BDCommon/ScanBDROMResult.cs` | 10 | 扫描结果（ConcurrentDictionary 收集异常） |
| `BDROM` | `BDCommon/rom/BDROM.cs` | — | 蓝光光盘模型 |
| `TSStreamFile` | `BDCommon/rom/TSStreamFile.cs` | — | .m2ts 流文件，Scan() + lock(playlist) 保护 |
| `TSPlaylistFile` | `BDCommon/rom/TSPlaylistFile.cs` | — | 播放列表，持有 StreamClips 和 bitrate 数据 |

---

## Phase 3 关键设计决策

1. **Parallel.ForEach + MaxDegreeOfParallelism**: 直接替换原有的 `Thread` + `while(IsAlive)` 阻塞模式，`-t 1` 时退化为串行。
2. **ThreadSafeProgressReporter**: 独立的进度报告类，使用 `Interlocked.Add` 原子累加字节数，`Interlocked.CompareExchange` 实现 500ms 节流防闪烁。
3. **lock(playlist)**: 在 `TSStreamFile.UpdateStreamBitrate` 和 `UpdateStreamBitrates` 中以 playlist 对象为锁粒度保护并发写入。
4. **ConcurrentDictionary\<string, Exception\>**: 替代 `Dictionary` 收集扫描异常，无需外部加锁。
5. **ClearBitrates 提前**: 从原来在准备阶段每个 streamFile 循环中重复调用改为单独遍历一次所有 playlist。
6. **ScanBDROMState 精简**: 移除 `StreamFile`、`Exception`、`OnReportChange` 等并行不安全字段，仅保留初始化后只读的 `TotalBytes` 和 `PlaylistMap`。

---

## Phase 4 关键设计决策

1. **ProcessSingleDisc 独立方法**: 封装单盘 init → scan → report 全流程，每次调用创建独立的 `CmdOptions` 克隆、`BDSettings` 实例和 error/debug 日志路径。
2. **CmdOptions 克隆**: 使用 `Cloner.Clone()` 深拷贝，因 `BDSettings` 通过引用实时读取 `CmdOptions` 属性，各线程不能共享同一 opts。
3. **ConcurrentDictionary\<string, string\>**: 收集 discPath → reportPath 映射，报告合并按原始排序顺序串行执行。
4. **两级并行**: 多盘级 `Parallel.ForEach`（外层）+ Stream 文件级 `Parallel.ForEach`（内层），共享同一 `MaxDegreeOfParallelism` 设置。

---

## Phase 5 验证结果

| 验证项 | 结果 |
|--------|------|
| `dotnet build` | ✅ 0 错误（23 个 NU1510 警告，均为系统包） |
| `-t 1` 串行 vs 并行 `diff` | ✅ 报告字节级一致 |
| `-t 16` 压力测试（3 次） | ✅ 两次成功完成，报告与基线完全一致，无崩溃无死锁 |
| `--help` 参数完整性 | ✅ 包含所有参数（`-g`, `-e`, `-b`, `-l`, `-y`, `-v`, `-k`, `-m`, `-o`, `-q`, `-j`, `-t`） |
| `Program.cs` 行数 | ✅ 152 行，仅 `static readonly` 字段 |
| 新 NuGet 依赖 | ✅ 无新增（仅 `CommandLine` + 系统包） |
| 新类独立文件 | ✅ `ReportGenerator`、`BDROMInitializer`、`BDROMScanner`、`ThreadSafeProgressReporter` 均在独立 `.cs` 文件中 |
| `lock(playlist)` 保护 | ✅ `TSStreamFile.cs` 第 350 行和第 375 行 |

