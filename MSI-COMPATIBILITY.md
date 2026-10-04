# MSI Afterburner 兼容性排查

日期：2026-10-04，Windows 11 本机真实交互桌面。

用户已确认：普通窗口、Win+D 往返和 Win+Tab 切换正常。MSI Afterburner 聚焦时，按钮处于开启状态但没有黑色屏障。

## 当前事实

只读 token 查询：MSI Afterburner 的 TokenElevation=1，普通权限诊断进程为0；运行中的 FocusShade 使用普通权限。代码新增的 HigherElevation 判断会在该目标聚焦时直接暂停遮罩，保留 Enabled，返回普通应用后恢复。因此当前无黑色屏障是明确的兼容降级，不能称为已支持 MSI。

为检验是否误判，临时关闭普通遮罩，使用普通权限独立探针尝试真实窗口排序；不改变目标位置、大小或焦点。实际结果：

| SetWindowPos 标志 | 含义 | 结果 |
| --- | --- | --- |
| 0x4213 | 现行异步排序 | False，错误5 |
| 0x4613 | 异步排序，跳过位置变更通知 | False，错误5 |
| 0x0613 | 同步排序，跳过位置变更通知 | False，错误5 |
| 0x0213 | 同步排序 | False，错误5 |

错误5为 Access denied。目标原始 TOPMOST=False，全部调用结束后仍为False；每次记录均确认前台焦点不变。恢复请求也被拒绝，但没有成功修改过目标的属性。探针没有移动或调整窗口大小，结束后恢复原遮罩开关状态。

另创建工具自身的隐藏窗口，尝试将它插在 MSI 下方：仍返回False/错误5；目标属性和焦点不变，探针窗口始终未显示。仅移动自己的遮罩也不能通过引用该高权限目标完成现行三层排序。

Microsoft SetWindowPos 文档要求失败时读取 GetLastError，并说明异步参数只是投递窗口排序请求；因此本次判断依据真实返回值，而非仅凭 TokenElevation。
https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos

## 历史差异的证据范围

保留的19:30旧二进制经只读IL检查，Controller.Update调用WindowLayers.KeepAbove，后者调用SetWindowPos。该旧文件也依赖窗口排序，不能认定用户提到的旧版必然采用镂空方案。更早的报告记录过region遮罩及随后删除fallback，但没有记录用户当时使用的具体版本、MSI权限和FocusShade权限。尚不能准确解释此前为何能用，不能把未验证的历史状态当作事实。

## 当前处理

没有修改现行代码，没有重启程序，没有更改MSI设置或请求管理员权限。保留权限受限时暂停显示、开启状态不变、普通应用返回恢复的策略。

保持用户要求的纯三层架构时，需要工具具备足够权限才能控制这台机器上当前 MSI 的窗口层级。用户可选择以管理员身份运行 FocusShade；该运行方式尚未在本次排查中实际验证，不承诺受保护窗口、安全桌面或独占全屏支持。没有引入镂空、分块追踪或无文档安全绕过。
## 最终完整对照与处理

用户单独测试删减前完整版本后确认旧版也失败。两版WindowLayers.cs相同，普通权限下调用均失败。按用户明确要求改为默认 requireAdministrator，恢复稳定版。用户确认管理员模式下 MSI 操作与遮罩一切正常；实际稳定进程 TokenElevation=1。临时最高权限任务注册、执行及清理通过，但真实重新登录触发尚未验证。
