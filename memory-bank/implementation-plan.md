# BDInfo 多核并行重构 — 分步实施计划

> 本计划基于 `tech-stack.md` 和 `product-requirement-document.md` 制定。
> **严禁包含代码**——只提供清晰、具体的操作指令和验证方法。

---

## 前置条件

在执行任何步骤前，必须：
1. 完整阅读 `memory-bank/@architecture.md`
2. 完整阅读 `memory-bank/@product-requirement-document.md`
3. 确认项目可正常编译：在 `BDInfo.Core/` 目录下运行 `dotnet build`，确认无编译错误

---

## 阶段 0：基线建立（修改 0 行业务逻辑）

### 步骤 0.1 — 生成基线报告

**目的**：用当前串行版本生成一份参考报告，后续所有改动都以此报告作为正确性基准。

**指令**：
1. 准备一个蓝光 BDMV 文件夹或 ISO 文件作为测试盘
2. 使用当前版本执行扫描，并将报告输出到文件：`dotnet run --project BDInfo -p <测试盘路径> -o baseline_report.bdinfo`
3. 保存 `baseline_report.bdinfo` 到 `docs/` 目录

**验证**：
- 确认 `baseline_report.bdinfo` 文件存在且非空
- 确认文件包含 "PLAYLIST:" 和 "DISC INFO:" 等关键字段
- 记录执行耗时（控制台输出或手动计时）

---

## 阶段 1：拆分 Program.cs 单体文件（纯重构，不改行为）

> **原则**：这一阶段只做文件拆分和方法搬迁，不修改任何逻辑。每一步完成后必须保证编译通过且报告输出不变。

### 步骤 1.1 — 提取报告生成器到独立文件

**目的**：将 `Program.cs` 中 `Generate()` 方法（第 443–1501 行，约 1058 行）提取到新文件。

**指令**：
1. 在 `BDInfo/` 目录下新建文件 `ReportGenerator.cs`
2. 创建 `internal static class ReportGenerator`，命名空间为 `BDInfo`
3. 将 `Program.cs` 中 `Generate(BDROM, IEnumerable<TSPlaylistFile>, ScanBDROMResult)` 方法整体搬迁到 `ReportGenerator` 中
4. 该方法需要用到 `_bdinfoSettings` 和 `ProductVersion`，以参数方式传入（新增对应参数），不使用静态字段
5. 更新 `Program.cs` 中 `GenerateReport()` 方法，改为调用 `ReportGenerator.Generate(...)`
6. 清理 `Program.cs` 中不再需要的 `using` 语句

**验证**：
- `dotnet build` 编译通过，零错误零警告
- 用步骤 0.1 的同一测试盘重新扫描生成报告，`diff` 比较新旧报告，**内容完全一致**
- 确认 `Program.cs` 行数显著减少（应从 ~1501 行降至 ~450 行）
- 确认 `ReportGenerator.cs` 包含且仅包含 `ReportGenerator` 类

---

### 步骤 1.2 — 提取 BDROM 初始化逻辑到独立文件

**目的**：将 `InitBDROM`、`InitBDROMWork`、`InitBDROMCompleted` 及三个错误回调方法提取出来。

**指令**：
1. 在 `BDInfo/` 目录下新建文件 `BDROMInitializer.cs`
2. 创建 `internal static class BDROMInitializer`，命名空间为 `BDInfo`
3. 搬迁以下方法：`InitBDROMWork`、`InitBDROMCompleted`、`BDROM_StreamClipFileScanError`、`BDROM_StreamFileScanError`、`BDROM_PlaylistFileScanError`
4. 将这些方法中引用的静态字段（`_error`）改为参数传入
5. 让 `InitBDROMWork` 返回初始化好的 `BDROM` 实例（而非赋值给静态字段）
6. 更新 `Program.cs` 中 `InitBDROM()` 方法，改为调用 `BDROMInitializer` 的方法

**验证**：
- `dotnet build` 编译通过
- 用同一测试盘扫描，`diff` 比较报告输出，**内容完全一致**
- 确认 `Program.cs` 行数进一步减少（应降至 ~350 行以下）

---

### 步骤 1.3 — 提取扫描逻辑到独立文件

**目的**：将 `ScanBDROMWork`、`ScanBDROMThread`、`ScanBDROMProgress`、`ScanBDROMEvent`、`ScanBDROMCompleted` 提取出来。

**指令**：
1. 在 `BDInfo/` 目录下新建文件 `BDROMScanner.cs`
2. 创建 `internal static class BDROMScanner`，命名空间为 `BDInfo`
3. 搬迁以下方法：`ScanBDROMWork`、`ScanBDROMThread`、`ScanBDROMProgress`、`ScanBDROMEvent`、`ScanBDROMCompleted`
4. 将 `ScanBDROM()` 的编排逻辑也搬迁过来，让它接收 `BDROM` 实例和 `BDSettings` 作为参数
5. 将静态字段（`_error`、`_bdinfoSettings`）的引用全部改为参数传入
6. `ScanBDROMWork` 返回 `ScanBDROMResult` 实例（而非赋值给静态字段）
7. 更新 `Program.cs`，调用 `BDROMScanner` 的方法

**验证**：
- `dotnet build` 编译通过
- 用同一测试盘扫描，`diff` 比较报告输出，**内容完全一致**
- 确认 `Program.cs` 现在只包含 `Main`、`Exec` 和必要的编排调用，行数 **不超过 150 行**
- 确认没有单个新文件超过 300 行

---

### 步骤 1.4 — 消除 Program.cs 中的静态可变字段

**目的**：将 `BDROM`、`ScanResult`、`_bdinfoSettings`、`_error`、`_debug` 这些静态字段改为通过方法参数传递。

**指令**：
1. 在 `Program.cs` 中删除所有静态可变字段声明（`BDROM`、`ScanResult`、`_bdinfoSettings`、`_error`、`_debug`）
2. 在 `Exec` 方法中以局部变量声明它们
3. 通过参数将它们传递给 `BDROMInitializer`、`BDROMScanner`、`ReportGenerator`
4. `ProductVersion` 可保留为 `const` 或 `static readonly`（只读不变）

**验证**：
- `dotnet build` 编译通过
- 用同一测试盘扫描，`diff` 比较报告输出，**内容完全一致**
- 使用 `grep -rn "private static" BDInfo/Program.cs` 确认不再有非 readonly 的静态字段
- 确认 `Program.cs` 行数不超过 150 行

---

## 阶段 2：添加 `-t` / `--threads` 参数（无并行逻辑）

### 步骤 2.1 — 在 BDInfoSettings 接口中新增 MaxThreads 属性

**指令**：
1. 打开 `BDCommon/rom/BDInfoSettings.cs`
2. 添加 `public abstract int MaxThreads { get; }` 抽象属性

**验证**：
- `dotnet build` 预期会报错（`BDSettings` 未实现新属性）——这是正确的
- 确认只改动了 `BDInfoSettings.cs` 一个文件

---

### 步骤 2.2 — 在 CmdOptions 中新增 -t 参数

**指令**：
1. 打开 `BDInfo/CmdOptions.cs`
2. 添加新属性：短参数 `'t'`，长参数 `"threads"`，非必填，默认值为 `Environment.ProcessorCount`
3. HelpText 设为 `"Maximum number of parallel scan threads"`

**验证**：
- 确认 `CmdOptions.cs` 文件改动仅增加了一个属性
- 编译暂时仍会因 `BDSettings` 缺少实现而报错——这是预期的

---

### 步骤 2.3 — 在 BDSettings 中实现 MaxThreads

**指令**：
1. 打开 `BDInfo/BDSettings.cs`
2. 添加 `MaxThreads` 属性实现，从 `_opts.Threads` 读取值
3. 如果值小于 1，则默认使用 `Environment.ProcessorCount`

**验证**：
- `dotnet build` 编译通过
- 运行 `dotnet run --project BDInfo -- --help`，确认输出中包含 `-t, --threads` 参数说明
- 运行 `dotnet run --project BDInfo -p <测试盘路径> -t 1 -o threads_test.bdinfo`，`diff` 比较与基线报告一致

---

## 阶段 3：Stream 文件级并行化（核心改动）

### 步骤 3.1 — 重构 ScanBDROMState 为线程安全版本

**目的**：消除共享可变字段，拆分为只读准备数据与线程安全的运行时状态。

**指令**：
1. 打开 `BDCommon/ScanBDROMState.cs`
2. 移除 `StreamFile` 字段（并行时每个线程有自己的 streamFile，不需共享）
3. 移除 `Exception` 字段（改用外部的 `ConcurrentDictionary` 收集）
4. 将 `FinishedBytes` 的 setter 改为不触发事件（进度报告将用独立机制）
5. 保留 `TotalBytes`（初始化后只读）和 `PlaylistMap`（初始化后只读）
6. 保留 `TimeStarted`
7. 添加 `using System.Collections.Concurrent;`

**验证**：
- `dotnet build` 可能报错（依赖 `StreamFile` 和 `Exception` 字段的代码需要后续步骤修复）——先记录错误列表
- 确认 `ScanBDROMState.cs` 不再包含任何非线程安全的可变字段

---

### 步骤 3.2 — 重构 ScanBDROMResult 使用 ConcurrentDictionary

**指令**：
1. 打开 `BDCommon/ScanBDROMResult.cs`
2. 将 `FileExceptions` 的类型从 `Dictionary<string, Exception>` 改为 `ConcurrentDictionary<string, Exception>`
3. 添加 `using System.Collections.Concurrent;`

**验证**：
- `dotnet build`（可能仍有其他未修复的报错，但此文件本身应无错误）
- 确认 `ScanBDROMResult.cs` 中 `FileExceptions` 的类型已变更

---

### 步骤 3.3 — 新建 ThreadSafeProgressReporter 类

**目的**：创建一个独立的线程安全进度报告器，替代原来依赖 `ScanBDROMState.StreamFile` 的进度报告。

**指令**：
1. 在 `BDCommon/` 目录下新建文件 `ThreadSafeProgressReporter.cs`
2. 创建 `public class ThreadSafeProgressReporter`，命名空间为 `BDCommon`
3. 包含以下字段和方法：
   - `_totalBytes`（`long`，构造时设置）
   - `_finishedBytes`（`long`，使用 `Interlocked` 累加）
   - `_timeStarted`（`DateTime`）
   - `_lastReportTicks`（`long`，用于输出节流）
   - `ReportFileCompleted(long fileBytes)` — 原子累加 `_finishedBytes`
   - `RenderProgress()` — 使用 `Interlocked.CompareExchange` 实现 500ms 节流，输出格式与原版 `ScanBDROMProgress` 一致（百分比、已用时间、剩余时间）
4. 使用 `\r` 覆盖当前行输出

**验证**：
- `dotnet build` 编译通过（此为新文件，不影响已有代码）
- 确认文件不超过 80 行
- 确认所有可变字段都使用了 `Interlocked` 或 `volatile`

---

### 步骤 3.4 — 将 BDROMScanner 扫描循环改为 Parallel.ForEach

**目的**：这是核心改动，将串行扫描变为并行扫描。

**指令**：
1. 打开 `BDInfo/BDROMScanner.cs`
2. 在 `ScanBDROMWork` 方法中：
   - 保留准备阶段（计算 `TotalBytes`、构建 `PlaylistMap`）不变——这部分是串行的
   - 确保 `playlist.ClearBitrates()` 在准备阶段完成，不在并行阶段调用
   - 删除 `Timer` 的创建（进度改用 `ThreadSafeProgressReporter`）
   - 将第二个 `foreach` 循环（扫描循环）替换为 `Parallel.ForEach`
   - 创建 `ParallelOptions`，设置 `MaxDegreeOfParallelism` 从 settings 的 `MaxThreads` 读取
   - 在每个并行迭代体中：
     - 使用局部变量获取 `streamFile`（由 `Parallel.ForEach` 自动提供）
     - 从 `PlaylistMap` 中查找关联的 playlists（只读操作，线程安全）
     - 调用 `streamFile.Scan(playlists, true)`
     - 如果抛出异常，用 `ConcurrentDictionary` 的线程安全方法收集
     - 在 `finally` 中使用 `Interlocked.Add` 累加已完成字节数
     - 调用 `ThreadSafeProgressReporter.ReportFileCompleted(...)`
3. 删除 `ScanBDROMThread` 方法（不再需要）
4. 删除 `ScanBDROMEvent` 方法（不再需要）
5. 保留并更新 `ScanBDROMCompleted` 方法，从 `ThreadSafeProgressReporter` 获取最终状态

**验证**：
- `dotnet build` 编译通过
- 用 `-t 1` 运行：`dotnet run --project BDInfo -p <测试盘路径> -t 1 -o serial_test.bdinfo`
- `diff serial_test.bdinfo baseline_report.bdinfo` — **内容必须完全一致**
- 用默认线程数运行：`dotnet run --project BDInfo -p <测试盘路径> -o parallel_test.bdinfo`
- `diff parallel_test.bdinfo baseline_report.bdinfo` — **内容必须完全一致**
- 确认控制台进度输出正常，不闪烁

---

### 步骤 3.5 — 审计并添加 Playlist 写回锁

**目的**：防止多个 stream 扫描同时写入同一个 playlist 导致数据竞争。

**指令**：
1. 打开 `BDCommon/rom/TSStreamFile.cs`
2. 搜索所有对 `TSPlaylistFile` 对象的写入操作
3. 找到 `UpdateBitrate` 或类似方法中累加 bitrate 的代码
4. 在每个写入 playlist 属性的位置，添加 `lock (playlist) { ... }` 保护
5. 如果写入逻辑分散在多处，考虑在 `TSPlaylistFile` 中添加一个线程安全的 `UpdateBitrateThreadSafe` 方法，内部加锁

**验证**：
- `dotnet build` 编译通过
- 用高并发度运行 3 次：`dotnet run --project BDInfo -p <测试盘路径> -t 16 -o stress_test_N.bdinfo`（将 N 替换为 1、2、3）
- `diff` 比较三次输出，**三份报告内容完全一致**
- `diff` 比较与 `baseline_report.bdinfo`，**内容完全一致**

---

## 阶段 4：多光盘级并行（P1，可选）

### 步骤 4.1 — 重构 Exec() 消除全局状态依赖

**目的**：使每个光盘的 init + scan + report 流程完全独立，不依赖任何全局变量。

**指令**：
1. 打开 `BDInfo/Program.cs` 的 `Exec` 方法
2. 将多光盘循环中的每次迭代封装为一个返回值包含报告路径的独立方法
3. 该方法内部创建独立的 `BDROM` 实例、`ScanBDROMResult` 实例
4. 确保每个光盘的扫描 **不读写** 任何共享状态

**验证**：
- `dotnet build` 编译通过
- 准备一个包含多个 BDMV 子目录的路径
- 用 `-t 1` 扫描，`diff` 每个光盘的独立报告与基线报告一致
- 确认合并后的大报告与基线一致

---

### 步骤 4.2 — 将多光盘循环改为 Parallel.ForEach

**指令**：
1. 在 `Exec` 方法中，将 `foreach (var subDir in subItems...)` 替换为 `Parallel.ForEach`
2. 使用与 stream 级相同的 `MaxDegreeOfParallelism` 设置
3. 使用线程安全的列表收集报告路径
4. 报告合并部分保持串行（在 `Parallel.ForEach` 之后执行）

**验证**：
- `dotnet build` 编译通过
- 用多光盘路径 + `-t 1` 运行，`diff` 与基线一致
- 用多光盘路径 + 默认线程数运行，`diff` 与基线一致
- 手动检查合并后的报告格式正确

---

## 阶段 5：最终验证与收尾

### 步骤 5.1 — 全面正确性验证

**指令**：
1. 用至少 2 个不同的蓝光盘（一个 Full HD，一个 UHD）分别以 `-t 1` 和默认线程数扫描
2. 对每个盘，`diff` 比较串行和并行报告
3. 确认所有报告 **字节级一致**

**验证**：
- 4 份 `diff` 结果为空（`-t 1` FHD vs 并行 FHD，`-t 1` UHD vs 并行 UHD）

---

### 步骤 5.2 — 性能验证

**指令**：
1. 选一个 UHD 蓝光盘，CPU 核心数 ≥ 4
2. 用 `-t 1` 运行并记录耗时
3. 用 `-t 4` 运行并记录耗时
4. 用默认线程数运行并记录耗时
5. 计算加速比

**验证**：
- 在 CPU-bound 场景下，加速比应 ≥ 2x
- `-t 1` 耗时应与原版基线耗时基本一致（证明无性能退化）

---

### 步骤 5.3 — 错误隔离验证

**指令**：
1. 准备一个测试盘，在其中放入一个已损坏的 `.m2ts` 文件（可以手动截断一个正常文件）
2. 用并行模式扫描
3. 检查报告输出

**验证**：
- 扫描不崩溃，不死锁
- 报告中包含损坏文件的异常信息
- 其他正常文件的报告内容正确完整

---

### 步骤 5.4 — 向后兼容性验证

**指令**：
1. 不带 `-t` 参数运行扫描
2. 检查现有的所有命令行参数是否仍然正常工作：`-g`、`-e`、`-b`、`-l`、`-y`、`-v`、`-k`、`-m`、`-o`、`-q`、`-j`

**验证**：
- 所有现有参数照常工作
- 不带 `-t` 时默认使用 `Environment.ProcessorCount`
- `--help` 输出包含所有参数

---

### 步骤 5.5 — 代码质量检查

**指令**：
1. 检查每个 `.cs` 文件的行数：`find BDInfo.Core -name "*.cs" -exec wc -l {} + | sort -rn`
2. 检查 `Program.cs` 中是否还有静态可变字段：`grep -n "private static" BDInfo/Program.cs`
3. 检查所有新类是否在独立文件中
4. 确认无新 NuGet 依赖：`dotnet list BDInfo.Core/BDInfo/BDInfo.csproj package`

**验证**：
- 没有单个文件超过 300 行
- `Program.cs` 不超过 150 行，只做编排
- 无新增 NuGet 依赖
- 每个新类（`ReportGenerator`、`BDROMInitializer`、`BDROMScanner`、`ThreadSafeProgressReporter`）在独立文件中

---

### 步骤 5.6 — 更新架构文档

**指令**：
1. 更新 `memory-bank/@architecture.md`，记录：
   - 新增的文件及其职责
   - 并行化架构设计
   - 线程安全策略
   - `-t` 参数用法
2. 确保文档反映代码的最新状态

**验证**：
- `memory-bank/@architecture.md` 包含所有新增文件的描述
- 文档中的架构图与实际代码结构一致

---

## 步骤概览

| 阶段 | 步骤数 | 核心目标 | 风险等级 |
|------|--------|----------|----------|
| 阶段 0 — 基线建立 | 1 | 生成参考报告 | 🟢 无 |
| 阶段 1 — 拆分单体文件 | 4 | 模块化 `Program.cs` | 🟢 低（纯重构） |
| 阶段 2 — 添加 `-t` 参数 | 3 | 参数解析基础设施 | 🟢 低 |
| 阶段 3 — Stream 并行化 | 5 | 核心并行改造 | 🟡 中（线程安全） |
| 阶段 4 — 多光盘并行 | 2 | 多光盘并行（可选） | 🟡 中 |
| 阶段 5 — 验证与收尾 | 6 | 全面验证 | 🟢 低 |

**总计 21 步**，每步都有独立的验证标准。
