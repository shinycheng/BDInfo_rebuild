# BDInfo

蓝光光盘扫描工具，支持 Full HD、Ultra HD 和 3D 蓝光光盘，可在多种操作系统上运行。提供的二进制文件为便携版，无需安装任何框架。

## ✨ 特性

- **多核并行扫描** — 使用 `Parallel.ForEach` 实现 Stream 文件级和多光盘级并行，显著提升扫描速度
- **可配置并发度** — 通过 `-t / --threads` 参数控制最大并行线程数，默认使用全部 CPU 核心
- **完全向后兼容** — `-t 1` 退化为串行扫描，输出与原版完全一致
- **线程安全** — 使用 `Interlocked` 原子操作、`ConcurrentDictionary` 和 `lock(playlist)` 保护并发安全
- **错误隔离** — 单个文件扫描失败不影响其他文件

## 命令行参数

| 短参数 | 长参数 | 说明 | 必填 | 默认值 |
| --- | --- | --- | --- | --- |
| `-p` | `--path` | ISO 文件或蓝光文件夹路径 | ✅ |  |
| `-t` | `--threads` | 最大并行扫描线程数 |  | CPU 核心数 |
| `-o` | `--reportfilename` | 报告文件名（含扩展名）。未提供扩展名时自动添加 `.txt` |  |  |
| `-g` | `--generatestreamdiagnostics` | 生成流诊断信息 |  | False |
| `-e` | `--extendedstreamdiagnostics` | 生成扩展流诊断信息 |  | False |
| `-b` | `--enablessif` | 启用 SSIF 支持 |  | False |
| `-l` | `--filterloopingplaylists` | 过滤循环播放列表 |  | False |
| `-y` | `--filtershortplaylist` | 过滤短播放列表 |  | True |
| `-v` | `--filtershortplaylistvalue` | 短播放列表过滤阈值 |  | 20 |
| `-k` | `--keepstreamorder` | 保持流顺序 |  | False |
| `-m` | `--generatetextsummary` | 生成文本摘要 |  | True |
| `-q` | `--includeversionandnotes` | 在报告中包含版本和注释 |  | False |
| `-j` | `--groupbytime` | 按时间分组 |  | False |

Linux 用户需先赋予执行权限：`chmod +x BDInfo`

## 使用方法

### Windows
```bash
# 扫描蓝光文件夹
BDInfo.exe -p 蓝光文件夹路径 -o 报告文件路径.bdinfo

# 扫描 ISO 文件
BDInfo.exe -p ISO文件路径 -o 报告文件路径.bdinfo

# 指定线程数（例如 4 线程）
BDInfo.exe -p 蓝光文件夹路径 -t 4 -o 报告文件路径.bdinfo

# 串行模式（与原版行为一致）
BDInfo.exe -p 蓝光文件夹路径 -t 1 -o 报告文件路径.bdinfo
```

### Linux / macOS
```bash
# 扫描蓝光文件夹
./BDInfo -p 蓝光文件夹路径 -o 报告文件路径.bdinfo

# 扫描 ISO 文件
./BDInfo -p ISO文件路径 -o 报告文件路径.bdinfo

# 指定线程数
./BDInfo -p 蓝光文件夹路径 -t 4 -o 报告文件路径.bdinfo
```

---

# BDExtractor

蓝光光盘 ISO 提取工具，无需挂载即可提取（非 EEF ISO 除外）。提供的二进制文件为便携版，无需安装任何框架。

## 命令行参数

| 短参数 | 长参数 | 说明 | 必填 |
| --- | --- | --- | --- |
| `-p` | `--path` | ISO 文件路径 | ✅ |
| `-o` | `--output` | 输出文件夹（未指定时提取到 ISO 同目录） |  |

Linux 用户需先赋予执行权限：`chmod +x BDExtractor`

## 使用方法

### Windows
```bash
BDExtractor.exe -p ISO文件路径 -o 输出文件夹
BDExtractor.exe -p ISO文件路径
```

### Linux / macOS
```bash
./BDExtractor -p ISO文件路径 -o 输出文件夹
./BDExtractor -p ISO文件路径
```

---

# BDInfoDataSubstractor（测试版）

从扫盘报告文本中根据多种条件提取主播放列表信息。

## 使用方法

### Windows
```bash
BDInfoDataSubstractor.exe bdinfo.txt bdinfo2.txt
```

### Linux / macOS
```bash
./BDInfoDataSubstractor bdinfo.txt bdinfo2.txt
```