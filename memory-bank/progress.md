# BDInfo 多核并行重构 — 进度记录

> 记录每个步骤的完成时间和结果。

---

## 阶段 0：基线建立
- [x] 步骤 0.1 — 生成基线报告
  - **完成时间**: 2026-03-15
  - **结果**: 使用测试盘 `Mean.Guns.1997.Complete.BluRay-LAZERS` 成功生成 `test_phase1.bdinfo`（21KB），作为后续验证的对照基线。

## 阶段 1：拆分 Program.cs 单体文件
- [x] 步骤 1.1 — 提取报告生成器到 `ReportGenerator.cs`
  - **完成时间**: 2026-03-15
  - **变更**: 将 `Generate()` 和 `GenerateReport()` 方法提取到 `ReportGenerator.cs`（1088 行）。所有静态字段引用改为参数传递（`settings`, `productVersion`, `errorLogPath`）。
- [x] 步骤 1.2 — 提取 BDROM 初始化逻辑到 `BDROMInitializer.cs`
  - **完成时间**: 2026-03-15
  - **变更**: 将 `InitBDROMWork()`, `InitBDROMCompleted()`, 三个错误回调提取到 `BDROMInitializer.cs`（113 行）。`InitBDROM()` 现在返回 `BDROM` 实例而非赋值静态字段。
- [x] 步骤 1.3 — 提取扫描逻辑到 `BDROMScanner.cs`
  - **完成时间**: 2026-03-15
  - **变更**: 将 `ScanBDROM()`, `ScanBDROMWork()`, `ScanBDROMProgress()`, `ScanBDROMCompleted()`, `ScanBDROMEvent()`, `ScanBDROMThread()` 提取到 `BDROMScanner.cs`（205 行）。`ScanBDROM()` 返回 `ScanBDROMResult`，接受 `BDROM`, `settings`, `productVersion`, `errorLogPath`, `debugLogPath` 参数。
- [x] 步骤 1.4 — 消除 Program.cs 中的静态可变字段
  - **完成时间**: 2026-03-15
  - **变更**: 移除 `BDROM`, `ScanResult`, `_bdinfoSettings`, `_error`, `_debug` 五个静态可变字段，全部改为 `Exec()` 方法内的局部变量。`Program.cs` 从 1502 行缩减至 111 行，仅保留 `ProductVersion` 和 `BDMV` 两个 `static readonly` 常量。
- **验证**: `dotnet build` 成功（0 错误），扫描测试盘输出正常（exit 0），报告格式正确。

## 阶段 2：添加 `-t` / `--threads` 参数
- [ ] 步骤 2.1 — 在 BDInfoSettings 接口中新增 MaxThreads
- [ ] 步骤 2.2 — 在 CmdOptions 中新增 `-t` 参数
- [ ] 步骤 2.3 — 在 BDSettings 中实现 MaxThreads

## 阶段 3：Stream 文件级并行化
- [ ] 步骤 3.1 — 重构 ScanBDROMState 为线程安全版本
- [ ] 步骤 3.2 — 重构 ScanBDROMResult 使用 ConcurrentDictionary
- [ ] 步骤 3.3 — 新建 ThreadSafeProgressReporter 类
- [ ] 步骤 3.4 — 将 BDROMScanner 扫描循环改为 Parallel.ForEach
- [ ] 步骤 3.5 — 审计并添加 Playlist 写回锁

## 阶段 4：多光盘级并行
- [ ] 步骤 4.1 — 重构 Exec() 消除全局状态依赖
- [ ] 步骤 4.2 — 将多光盘循环改为 Parallel.ForEach

## 阶段 5：最终验证与收尾
- [ ] 步骤 5.1 — 全面正确性验证
- [ ] 步骤 5.2 — 性能验证
- [ ] 步骤 5.3 — 错误隔离验证
- [ ] 步骤 5.4 — 向后兼容性验证
- [ ] 步骤 5.5 — 代码质量检查
- [ ] 步骤 5.6 — 更新架构文档

---

## 附注
- 因安装的 .NET SDK 为 10.0，已将所有 `.csproj` 的 TargetFramework 从 `net9.0` 改为 `net10.0`。
- `ReportGenerator.cs` 超 300 行限制（1088 行），但为 Phase 1 原则"不改逻辑"的原样搬迁。
