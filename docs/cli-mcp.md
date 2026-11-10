# CLI 与 MCP 入口（本地 fork 实验）

> 上游自 2026-09-11 起为 Read-Only Reference License。本目录下的 CLI/MCP 仅作本地 fork 改造，
> 不发布、不分发；改用前请阅读 `FORK-NOTICE.md` 与 `LICENSE`。

## 生成物

- `KirinTool.Cli/`：薄 exe 工程，引用主工程，输出 `KirinTool.Cli.dll`
- `Cli/`：CLI 命令、闸门与 MCP server 实现，编译进主 exe，因此 `Kirin-Tool --mcp` 也可用
- 两个入口共用 `CliRunner`，命令实现只有一份

## 命令

只读（第一批，已落地）：

| 命令 | 说明 | 复用 |
|---|---|---|
| `info` | 工具/fastboot/设备信息、能力与 rescue 计划 | `FastbootClient`、`FastbootReadResponses`、`ChipCapabilities`/`RescuePartitionPlan` |
| `doctor` | fastboot/adb 路径与版本、设备枚举、loader 清单、只读能力探测 | `BinaryLocator`、`ProcessRunner`、`FastbootClient`、`FastbootReadResponses`、`FirmwareUnlocker.CpuAddresses` |
| `getvar <name>` | 单个 fastboot 变量 | `FastbootClient.QueryVariableAsync` + `FastbootReadResponses.Variable` |
| `extract <UPDATE.APP> [outdir]` | 列出并导出全部分区 | `Services.PartitionExtractor.Read/ExtractAsync`（`Utils.UpdateApp`） |
| `dump <partition> -o <dir>` | `oem dump-emmc` 导出分区 | `Services.FastbootFlasherService.DumpPartitionAsync` |

写操作（第二批，仅骨架 + 闸门，永不真正写入）：`flash-ota`、`flash-xml`、`vcom-unlock`、`oeminfo`、`downgrade-enable`。
无 `--yes` 时打印计划清单；即使给了 `--yes` 也只打印 `confirmed-but-write-path-not-implemented`，一律 exit 4。

## 约定

- `--json` 输出结构化 JSON 到 stdout；进度/诊断一律 stderr，stdout 可安全管道
- 退出码：0 成功 / 1 用法 / 2 无设备 / 3 设备拒绝 / 4 被闸门拒 / 5 文件或校验失败
- fastboot 解析顺序：`KIRIN_FASTBOOT` 环境变量 → 程序目录及 `fastboot/` → PATH → `/usr/bin`、`/usr/local/bin`；adb 同理（`KIRIN_ADB`）
- 设备拒绝判定：输出包含 `FAILED`/`invalid command`/`not allowed`/`locked` 等 → exit 3

## MCP

`KirinTool.Cli mcp` 或 `Kirin-Tool --mcp`：stdio、**换行分隔的 JSON-RPC 2.0**（每行一条报文），
日志只写 stderr。支持 `initialize`、`notifications/initialized`、`ping`、`tools/list`、`tools/call`。

工具名：`info`、`doctor`、`getvar`、`extract`、`dump`、`flash_ota`、`flash_xml`、`vcom_unlock`、`oeminfo`、`downgrade_enable`。
写工具的 `inputSchema.required` 必含 `confirm: boolean`；缺省（false）时只返回计划清单，`isError=false`，
`_meta.exitCode` 为 4。

自测：

```bash
printf '%s\n' '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}' \
  '{"jsonrpc":"2.0","id":2,"method":"tools/list"}' | ./KirinTool.Cli mcp
```
