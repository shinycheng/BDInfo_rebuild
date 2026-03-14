# BDInfo 多核重构 — AI 编码规则

> **目标项目**: BDInfo 蓝光扫盘 CLI 工具多核并行重构
> **Runtime**: .NET (C#)  
> **核心原则**: 零新依赖、模块化多文件、线程安全

---

## 🔴 前置必读（最高优先级 — 写任何代码前必须执行）

> [!CAUTION]
> 以下三条规则优先级高于本文件中的所有其他规则。**违反任意一条即视为无效输出。**

1. **写任何代码前，必须完整阅读 `memory-bank/@architecture.md`**（包含完整项目架构与数据结构）。未阅读即编码属于盲目修改，极易引入不兼容的设计。

2. **写任何代码前，必须完整阅读 `memory-bank/@product-requirement-document.md`**（包含完整需求定义、功能优先级和验证计划）。未阅读即编码可能偏离需求方向。

3. **每完成一个重大功能或里程碑后，必须更新 `memory-bank/@architecture.md`**，确保架构文档始终反映代码的最新状态。过时的架构文档等于没有文档。

---

## 🚫 禁止事项（违规即失败）

### 1. 禁止单体巨文件（Monolith）

- **严禁** 将所有并行逻辑、进度报告、状态管理、CLI 参数等代码全部塞入 `Program.cs`。
- **严禁** 任何单个 `.cs` 文件超过 **300 行**。如果接近此阈值，必须拆分。
- **严禁** 在一个类中混合多个不相关职责（如同时处理 CLI 解析 + 扫描逻辑 + 报告生成）。
- **严禁** 使用 `#region` 代替文件拆分。`#region` 不是模块化方案。

### 2. 禁止引入新外部依赖

- **严禁** 引入任何新的 NuGet 包。所有并行化原语必须来自 .NET BCL。
- 仅允许使用现有依赖：`CommandLine`（CLI 解析）、`DiscUtils`（ISO 读取）。

### 3. 禁止不安全并发模式

- **严禁** 使用 `Thread` + `while (thread.IsAlive)` 阻塞等待模式（这正是当前要重构掉的反模式）。
- **严禁** 对共享可变字段使用非原子的 `+=` 操作（如 `FinishedBytes += xxx`）。
- **严禁** 使用 `async/await` 改造扫描流程（CPU-bound 场景，async 化收益为零）。
- **严禁** 使用 `Task.WhenAll`、`Channels`、`TPL Dataflow`、`Akka.NET` 等过度设计方案。

---

## ✅ 强制规则

### 4. 模块化文件结构（必须遵守）

每个独立功能必须拆分到独立的文件中。以下是强制的模块边界：

| 模块 | 文件 | 职责 |
|------|------|------|
| **CLI 入口** | `Program.cs` | 仅负责 `Main()` → 参数解析 → 调用流程编排，不包含任何业务逻辑 |
| **CLI 参数** | `CmdOptions.cs` | 命令行参数定义（包括 `-t / --threads`） |
| **设置** | `BDSettings.cs` | 设置实现（包括 `MaxThreads` 属性） |
| **扫描编排** | 独立类文件 | 扫描流程编排（`ScanBDROMWork` 的并行化逻辑） |
| **进度报告** | 独立类文件 | `ThreadSafeProgressReporter`（线程安全的进度输出） |
| **扫描状态** | `ScanBDROMState.cs` | 拆分为不可变全局状态 + 线程局部状态 |
| **扫描结果** | `ScanBDROMResult.cs` | 使用 `ConcurrentDictionary` 收集异常 |

**规则**：如果你要新增一个类，它 **必须** 放在独立的 `.cs` 文件中，文件名与类名保持一致。

### 5. 并行化技术选型（强制）

| 场景 | 必须使用 | 禁止使用 |
|------|----------|----------|
| Stream 文件并行扫描 | `Parallel.ForEach` | `Task.WhenAll`, `Channels`, 手动 `Thread` |
| 并发度控制 | `ParallelOptions.MaxDegreeOfParallelism` | 手动信号量 |
| 原子计数器 | `Interlocked.Add` / `Interlocked.Increment` | 裸 `+=` 或 `lock` 包裹的简单加法 |
| 异常收集 | `ConcurrentDictionary<string, Exception>` | 共享 `Exception` 字段覆盖 |
| Playlist 写回保护 | `lock (playlist)` | 无保护直接写入 |
| 进度输出节流 | `Interlocked.CompareExchange` + 时间间隔 | 无节流的 `Console.Write` |

### 6. 代码组织规范

- **一个文件一个类**：每个 `.cs` 文件只包含一个主类（内部嵌套类除外）。
- **命名空间一致**：所有 `BDCommon/` 下的类使用 `BDInfoLib.BDROM` 命名空间；所有 `BDInfo/` 下的类使用 `BDInfo` 命名空间。
- **新增代码放对位置**：
  - 线程安全工具类 → `BDCommon/`
  - CLI 相关逻辑 → `BDInfo/`
  - ROM 数据模型 → `BDCommon/rom/`
- **接口改动同步**：修改 `BDInfoSettings` 接口后，所有实现类（`BDSettings`）必须同步更新。

### 7. 线程安全审计规则

在修改任何代码前，必须回答以下问题：

1. 这个对象是否被多个线程同时访问？
2. 访问是只读还是读写？
3. 如果是读写，是否有正确的同步机制？

**黄金法则**：
- `TSStreamFile.Scan()` — 每个实例独立调用 → ✅ 安全，无需加锁
- `TSPlaylistFile` bitrate 累加 — 多 stream 写同一 playlist → ⚠️ 必须 `lock`
- `BDROM` 实例 — 扫描阶段只读 → ✅ 安全
- `FinishedBytes` 累加 — 多线程写 → ⚠️ 必须 `Interlocked.Add`
- `PlaylistMap` — 初始化后只读 → ✅ 安全

### 8. `-t` 参数规范

- 参数名：`-t` / `--threads`
- 类型：`int`
- 默认值：`Environment.ProcessorCount`
- 约束：`-t 1` 必须退化为完全串行行为，输出结果与原版一致
- 在 `CmdOptions.cs` 中使用 `[Option('t', "threads", ...)]` 声明

---

## 📐 设计模式指引

### 9. 改动最小化原则

- 优先修改现有方法的实现，而非重写整个类。
- 保留所有现有的公开 API 签名，除非线程安全改造明确要求变更。
- 不改动 `TSStreamFile`、`TSStream`、`TSCodec*.cs` 等编解码器文件。
- 不改动 `BDExtractor`、`BDInfoDataSubstractor`。

### 10. 重构分阶段进行

必须按以下顺序推进，**不允许跳阶段**：

1. **Phase 1 (P0)**：Stream 文件级并行 — `ScanBDROMWork()` 改为 `Parallel.ForEach`
2. **Phase 2 (P1)**：多光盘级并行 — `Exec()` 中的多光盘循环并行化
3. **Phase 3**：线程安全进度报告器

每个 Phase 完成后必须验证：
- `-t 1` 与原版输出 `diff` 无差异
- `-t N` 不崩溃、不死锁、报告内容正确

### 11. 错误隔离

- 单个 Stream 文件扫描失败 **不得** 影响其他文件的扫描。
- 使用 `ConcurrentDictionary<string, Exception>` 收集所有失败，扫描完成后统一报告。
- 严禁使用 `AggregateException` 让整个 `Parallel.ForEach` 中断。

---

## 📁 项目结构参考

```
BDInfo.Core/
├── BDCommon/                         # 共享库
│   ├── rom/                          # 蓝光数据模型（尽量不改动）
│   │   ├── BDROM.cs
│   │   ├── TSStreamFile.cs
│   │   ├── TSPlaylistFile.cs
│   │   ├── TSStreamClip.cs
│   │   ├── TSStream.cs
│   │   ├── TSCodec*.cs
│   │   └── BDInfoSettings.cs        # 新增 MaxThreads 接口属性
│   ├── ScanBDROMState.cs             # 重构：拆分线程安全状态
│   ├── ScanBDROMResult.cs            # 改用 ConcurrentDictionary
│   ├── ThreadSafeProgressReporter.cs # [NEW] 线程安全进度报告器
│   ├── ToolBox.cs
│   └── Cloner.cs
├── BDInfo/                           # CLI 入口
│   ├── Program.cs                    # 重构：并行化编排，保持精简
│   ├── BDSettings.cs                 # 新增 MaxThreads 属性
│   └── CmdOptions.cs                 # 新增 -t 参数
├── BDExtractor/                      # 不改动
└── BDInfoDataSubstractor/            # 不改动
```

---

## ⚡ 快速检查清单

在提交任何改动前，用此清单自检：

- [ ] 没有单个文件超过 300 行
- [ ] 没有引入新的 NuGet 依赖
- [ ] 每个新类在独立 `.cs` 文件中
- [ ] `Program.cs` 只做流程编排，不含业务逻辑
- [ ] 所有共享可变状态使用了正确的同步机制
- [ ] `-t 1` 行为等价于串行原版
- [ ] 进度报告在并行场景下不闪烁
- [ ] 单文件扫描失败不影响其他文件
