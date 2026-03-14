# BDInfo 多核重构 — 技术栈推荐

> 原则：**不引入新框架，只用 .NET 标准库**。最少改动，最高可靠性。

---

## 推荐方案：纯 .NET BCL（零外部依赖）

### 为什么选这个方案

BDInfo 当前唯一的外部依赖是 `CommandLine`（CLI 解析）和 `DiscUtils`（ISO 读取）。并行化所需的全部原语都已内置于 .NET BCL 中，**不需要引入任何新依赖**。这是最简单、最健壮的选择。

---

## 技术选型一览

| 需求 | 选型 | 来源 | 理由 |
|------|------|------|------|
| **并行执行** | `Parallel.ForEach` | `System.Threading.Tasks` | 一行代码替代 `foreach`，内置工作窃取调度，自动负载均衡 |
| **并发度控制** | `ParallelOptions.MaxDegreeOfParallelism` | `System.Threading.Tasks` | 直接对应 `-t` 参数，`= 1` 时退化为串行 |
| **原子计数器** | `Interlocked.Add` | `System.Threading` | 无锁累加 `FinishedBytes`，比 `lock` 更轻量 |
| **线程安全集合** | `ConcurrentDictionary<K,V>` | `System.Collections.Concurrent` | 替代 `Dictionary` 收集异常，无需手动加锁 |
| **共享资源保护** | `lock` 语句 | C# 语法 | 仅用于 playlist 写回（`TSPlaylistFile` bitrate 累加）的少量临界区 |
| **进度输出节流** | `System.Threading.Timer` | `System.Threading` | 沿用现有模式，加 500ms 最小间隔防闪烁 |
| **CLI 参数** | `CommandLine` (现有) | NuGet | 已在用，新增 `-t` 参数仅需加一个 `[Option]` 属性 |
| **ISO 读取** | `DiscUtils` (现有) | NuGet | 已在用，不做变更 |

---

## 对比过的备选方案

| 备选 | 评估 | 结论 |
|------|------|------|
| `Task.WhenAll` + `SemaphoreSlim` | 更灵活，支持 async，但 BDInfo 的 `Scan()` 是同步 CPU 密集型操作，async 化收益为零，徒增复杂度 | ❌ 过度设计 |
| `System.Threading.Channels` | 适合生产者-消费者模型，但扫描是简单的分治并行，不需要队列 | ❌ 不匹配 |
| TPL Dataflow (`ActionBlock`) | 功能强大但 API 较重，学习成本高，BDInfo 的场景用不上管道/排序 | ❌ 杀鸡用牛刀 |
| `Parallel LINQ (PLINQ)` | `streamFiles.AsParallel().ForAll(...)` —可行但不如 `Parallel.ForEach` 控制精细（并发度、异常聚合） | ⚠️ 可选但不推荐 |
| Akka.NET / Orleans | Actor 模型，适合分布式场景，本项目是单机 CLI 工具 | ❌ 完全不适合 |

---

## 核心代码模式

### Stream 级并行（替换整个 `ScanBDROMWork` 循环）

```csharp
// 只需要这些 using
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

var exceptions = new ConcurrentDictionary<string, Exception>();
long finishedBytes = 0;

Parallel.ForEach(streamFiles,
    new ParallelOptions { MaxDegreeOfParallelism = maxThreads },
    streamFile =>
    {
        try
        {
            var playlists = playlistMap[streamFile.Name]; // 只读，线程安全
            streamFile.Scan(playlists, true);
        }
        catch (Exception ex)
        {
            exceptions[streamFile.Name] = ex;
        }
        finally
        {
            Interlocked.Add(ref finishedBytes, streamFile.FileInfo.Length);
        }
    });
```

### Playlist 写回保护（仅在审计确认有竞争时使用）

```csharp
// 最简方案：以 playlist 对象自身为锁
lock (playlist)
{
    playlist.UpdateBitrate(streamClip);
}
```

### 进度报告节流

```csharp
private static long _lastReportTicks = 0;

private static void ThrottledProgress(long finished, long total)
{
    long now = Environment.TickCount64;
    long last = Interlocked.Read(ref _lastReportTicks);
    if (now - last < 500) return;  // 500ms 最小间隔
    if (Interlocked.CompareExchange(ref _lastReportTicks, now, last) != last) return;

    double pct = 100.0 * finished / total;
    Console.Write($"\rProgress: {pct:F2}%    ");
}
```

---

## 最终依赖图

```
BDInfo（重构后）
├── System.Threading.Tasks        ← Parallel.ForEach（BCL，不需安装）
├── System.Threading              ← Interlocked, Timer（BCL）
├── System.Collections.Concurrent ← ConcurrentDictionary（BCL）
├── CommandLine                   ← CLI 解析（现有，不变）
└── DiscUtils                     ← ISO 读取（现有，不变）

新增外部依赖数量：0
```

---

## 总结

| 维度 | 评价 |
|------|------|
| **简单性** | ⭐⭐⭐⭐⭐ — 零新依赖，核心改动集中在一个方法 |
| **健壮性** | ⭐⭐⭐⭐⭐ — BCL 并行原语经过 15+ 年产线验证 |
| **性能** | ⭐⭐⭐⭐ — ThreadPool 工作窃取调度器自动负载均衡 |
| **可维护性** | ⭐⭐⭐⭐⭐ — 所有 .NET 开发者都熟悉的标准 API |
| **向后兼容** | ⭐⭐⭐⭐⭐ — `-t 1` 完全等价于串行原版 |
