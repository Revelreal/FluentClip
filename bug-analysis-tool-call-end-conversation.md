# AI对话调用工具后结束问题 - 分析笔记

## 调查时间
2026-03-25

## 问题描述
AI对话一调用工具就会结束当前对话

## 代码流程分析

### 正常工具调用流程（流式）
1. `SendStreamingRequestAsync` 循环读取 SSE 数据
2. 收到 `[DONE]` 信号时检查 `finish_reason`
3. 如果是 `tool_calls`：
   - 执行 `ExecuteToolCallAsync`
   - 添加 `assistant` 消息（带 ToolCalls）和 `tool` 消息到 `_conversationHistory`
   - 递归调用 `SendStreamingRequestAsync` 继续对话
   - **return** 退出当前方法
4. 如果 `finish_reason != "tool_calls"`：
   - 执行到 line 987 调用 `OnComplete?.Invoke()`
   - 对话正常结束

### 潜在问题点

#### 1. OnComplete 被过早调用
在 line 987，`OnComplete?.Invoke()` 在 `if (fullResponse.Length > 0)` 之后被调用。
如果 AI 返回的响应**同时包含 content 和 tool_calls**，代码可能：
- 先通过 `delta.TryGetProperty("content")` 分支处理了 content（添加到 fullResponse）
- 后续 tool_calls 处理后 return 不会调用 OnComplete
- 但如果流式响应格式不同可能导致问题

#### 2. HandleComplete 显示提示
`MainWindow.xaml.cs` line 720 调用 `ShowRandomTip()`，40%概率显示随机提示。
用户可能误以为显示提示就是对话结束。

#### 3. 递归调用后 OnComplete 被调用
在 line 790 执行 `await SendStreamingRequestAsync(...)` 后，当前方法栈返回。
但如果递归调用内部发生异常或提前退出，外层方法可能会继续执行到 line 987。

## 待验证
1. 在 `AgentService.cs` 添加日志，确认递归调用 `SendStreamingRequestAsync` 是否正常执行
2. 检查 API 返回的流式数据格式是否同时包含 content 和 tool_calls
3. 确认 `OnComplete` 是否在不应该的时候被调用

## 建议的调试步骤
1. 在 `SendStreamingRequestAsync` 的关键位置添加日志：
   - 进入方法时
   - 执行工具调用前
   - 递归调用 `SendStreamingRequestAsync` 前
   - `OnComplete?.Invoke()` 调用前
2. 运行应用，触发工具调用，检查日志输出
3. 查看 API 返回的完整 SSE 响应内容

## 相关代码位置
- `AgentService.cs:750-795` - [DONE] 信号处理
- `AgentService.cs:800-956` - 流式响应解析
- `AgentService.cs:987` - OnComplete 调用
- `MainWindow.xaml.cs:713-723` - HandleComplete 实现
