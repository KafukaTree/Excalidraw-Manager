# Formula OCR Provider API v1

本文定义 Excalidraw Manager 公式编辑器与本地 OCR Provider 之间的稳定接口。目标是让公式识别与文档识别模型可以独立安装、升级、对比和替换，而不需要修改编辑器 UI 或管理器主体。

本文中的“必须”“应该”“可以”分别表示强制要求、推荐要求和可选能力。

## 1. 设计边界

- 编辑器/管理器是 **Client**：负责选择图片、限制输入大小、展示候选、人工修正、导出以及保存评测数据。
- Provider 是独立的 **本地 HTTP 服务**：负责加载一个或多个模型并返回结构化识别结果。
- Provider 必须默认只监听回环地址（`127.0.0.1` 或 `::1`），不得依赖公网服务。
- 核心接口为 `GET /v1/info`、`GET /v1/health`、`POST /v1/warmup` 和 `POST /v1/recognize`。
- v1 不接受文件路径或远程 URL，只接受请求体中的图片数据。这可以避免路径泄露、目录穿越和 SSRF。
- LaTeX 是公式结果的规范表示；MathML、AsciiMath 等属于可选派生格式。编辑器仍可使用本地 MathJax 统一渲染和导出。

## 2. 传输与兼容规则

- 使用 HTTP/1.1 或更高版本，JSON 请求和响应均为 UTF-8，`Content-Type: application/json`。
- API 版本使用语义化版本。本文对应 `1.0`；主版本不兼容，次版本只能增加可选字段或能力。
- Client 必须忽略未知响应字段；Provider 应该忽略未知的可选请求字段，但必须拒绝无效的必填字段。
- Client 只能请求 `/v1/info` 中已声明的模式、媒体类型、输出格式和模型。
- 每个请求可携带 `requestId`。Provider 必须在响应中原样返回它；未提供时 Provider 应生成一个不含隐私信息的 ID。
- 时间单位统一为毫秒，时间戳统一为 ISO 8601 UTC。
- 成功响应不得混入错误对象；失败不得使用 HTTP `200` 伪装成功。

Provider 由用户显式添加到本地配置，例如：

```json
{
  "id": "pix2tex-local",
  "baseUrl": "http://127.0.0.1:17861",
  "enabled": true,
  "tokenEnv": "EXCALIDRAW_MANAGER_PIX2TEX_TOKEN"
}
```

0.3.0 的管理器适配层从 `%LOCALAPPDATA%\ExcalidrawManager\formula-providers.json` 读取上述条目；文件可以是条目数组，也可以使用 `{ "providers": [...] }`。`tokenEnv` 只保存环境变量名，令牌本身不得写入配置文件。适配层只接受回环 HTTP 地址，并通过 `/api/models`、`/api/recognize` 和 `/api/warmup` 向编辑器提供同源代理。未创建该配置文件时，识别页保持“模型未安装”，但编辑和导出功能完全可用。

未来接入 Windows 凭据存储时可以增加 `tokenRef`；它应指向安全存储中的令牌，而不是令牌本身。Client 不应该扫描端口或自动信任局域网中发现的服务。`GET /v1/info` 是能力发现的唯一权威来源。

## 3. 能力发现：`GET /v1/info`

该接口不得触发大型模型加载，应该能够快速返回。

```json
{
  "apiVersion": "1.0",
  "provider": {
    "id": "org.example.formula-ocr",
    "name": "Example Local Formula OCR",
    "version": "0.4.2",
    "license": "Apache-2.0",
    "homepage": "https://example.invalid/formula-ocr"
  },
  "models": [
    {
      "id": "formula-base",
      "name": "Formula Base",
      "version": "2.1.0",
      "revision": "sha256:0123456789abcdef",
      "modes": ["formula"],
      "devices": ["cpu", "cuda"]
    },
    {
      "id": "page-mixed",
      "name": "Mixed Page",
      "version": "1.3.0",
      "revision": "sha256:fedcba9876543210",
      "modes": ["document"],
      "devices": ["cpu"]
    }
  ],
  "defaults": {
    "formulaModel": "formula-base",
    "documentModel": "page-mixed",
    "device": "cpu"
  },
  "capabilities": {
    "modes": ["formula", "document"],
    "inputMediaTypes": ["image/png", "image/jpeg", "image/webp"],
    "outputFormats": ["latex", "mathml"],
    "languageHints": true,
    "regions": true,
    "multipleCandidates": true,
    "confidence": true,
    "debugMetadata": true,
    "warmup": true
  },
  "limits": {
    "maxInputBytes": 10485760,
    "maxWidth": 8192,
    "maxHeight": 8192,
    "maxCandidates": 5,
    "maxConcurrentRequests": 1
  }
}
```

字段约束：

- `provider.id` 与 `models[].id` 必须是稳定、机器可读的标识；显示名称可以变化。
- `provider.version` 表示服务实现版本；`model.version` 表示模型发布版本；`model.revision` 应标识实际权重或构建修订。三者不得混用。
- `revision` 推荐使用权重文件摘要、不可变提交号或等价的可复现实验标识。
- Provider 未实现的能力必须声明为 `false` 或省略，不能仅依靠调用时报错。
- `formula` 表示单个公式或紧密公式区域；`document` 表示包含正文、多个公式和版面结构的页面图片。

## 4. 健康检查：`GET /v1/health`

健康检查必须轻量，不应执行完整推理。

```json
{
  "requestId": "health-01",
  "status": "ok",
  "ready": true,
  "loadedModels": ["formula-base"],
  "device": {
    "type": "cuda",
    "name": "NVIDIA GPU",
    "precision": "fp16"
  },
  "timestamp": "2026-07-16T03:20:15.000Z",
  "warnings": []
}
```

`status` 取值为 `ok`、`degraded` 或 `unavailable`。`ready` 表示当前是否可接收识别请求；健康但尚未加载模型时，可以为 `false`。响应不得暴露本机用户名、绝对路径、环境变量或访问令牌。

## 5. 模型预热：`POST /v1/warmup`

预热用于显式加载权重和初始化推理设备，不产生识别结果。重复调用必须安全。

请求：

```json
{
  "requestId": "warmup-01",
  "modelId": "formula-base",
  "device": "cuda",
  "modes": ["formula"],
  "timeoutMs": 120000
}
```

响应：

```json
{
  "requestId": "warmup-01",
  "status": "ready",
  "model": {
    "id": "formula-base",
    "version": "2.1.0",
    "revision": "sha256:0123456789abcdef"
  },
  "device": {
    "type": "cuda",
    "precision": "fp16"
  },
  "elapsedMs": 4832,
  "warnings": []
}
```

`status` 可为 `ready` 或 `skipped`。不支持预热时 Provider 应在 `/v1/info` 中声明 `warmup: false`，Client 不得调用该接口。超时后 Provider 应尽快取消尚未开始的任务；已经进入不可中断推理阶段的实现应在 `warnings` 中说明。

## 6. 识别：`POST /v1/recognize`

### 6.1 通用请求

```json
{
  "requestId": "rec-7f7b4b3e",
  "mode": "formula",
  "modelId": "formula-base",
  "device": "cuda",
  "input": {
    "kind": "image",
    "mediaType": "image/png",
    "dataBase64": "iVBORw0KGgoAAAANSUhEUgAA...",
    "sha256": "c881b5c1d8c4..."
  },
  "languageHints": ["zh-CN", "en"],
  "options": {
    "maxCandidates": 3,
    "outputFormats": ["latex", "mathml"],
    "preprocessing": "auto",
    "timeoutMs": 60000,
    "debug": false
  }
}
```

规则：

- `mode` 必须是 `formula` 或 `document`。
- `dataBase64` 只包含 Base64 数据，不带 `data:` URL 前缀。Provider 必须同时校验声明的媒体类型和解码后的文件魔数。
- `sha256` 可选，用于 Client 与 Provider 核对样本；它不是安全授权机制。
- `modelId` 和 `device` 可省略，此时使用 `/v1/info` 声明的默认值。
- `preprocessing` 可为 `auto`、`none` 或 Provider 在 `capabilities` 扩展字段中声明的值。
- `debug` 默认必须为 `false`。生产 UI 不应该默认开启。

### 6.2 `formula` 模式响应

候选按推荐顺序排列。`latex` 是必须字段；其他格式仅在 Provider 已声明且请求了对应输出时返回。

```json
{
  "requestId": "rec-7f7b4b3e",
  "mode": "formula",
  "candidates": [
    {
      "id": "candidate-0",
      "latex": "\\int_a^b f(x)\\,dx",
      "confidence": 0.947,
      "formats": {
        "mathml": "<math xmlns=\"http://www.w3.org/1998/Math/MathML\">...</math>"
      },
      "warnings": []
    },
    {
      "id": "candidate-1",
      "latex": "\\int_{a}^{b} f(x) dx",
      "confidence": 0.812,
      "formats": {},
      "warnings": ["spacing is uncertain"]
    }
  ],
  "provider": {
    "id": "org.example.formula-ocr",
    "version": "0.4.2"
  },
  "model": {
    "id": "formula-base",
    "version": "2.1.0",
    "revision": "sha256:0123456789abcdef"
  },
  "timing": {
    "queueMs": 2,
    "preprocessMs": 18,
    "inferenceMs": 214,
    "postprocessMs": 9,
    "totalMs": 243
  },
  "device": {
    "type": "cuda",
    "precision": "fp16"
  },
  "debug": null
}
```

`confidence` 的范围为 `0.0` 到 `1.0`。无法提供有意义的概率时必须返回 `null`，不得伪造固定分数。不同 Provider 的置信度不保证可直接比较，评测工具应按 `provider + model + revision` 分组校准。候选 `id` 只要求在当前响应中唯一。

### 6.3 `document` 模式响应

文档模式必须保留阅读顺序和归一化区域。坐标 `bbox` 使用 `[x, y, width, height]`，范围均为 `0.0` 到 `1.0`，原点位于图片左上角。

```json
{
  "requestId": "rec-page-01",
  "mode": "document",
  "document": {
    "width": 1920,
    "height": 1080,
    "blocks": [
      {
        "id": "block-0",
        "type": "text",
        "bbox": [0.08, 0.10, 0.62, 0.07],
        "text": "由牛顿第二定律可得",
        "confidence": 0.98
      },
      {
        "id": "block-1",
        "type": "formula",
        "bbox": [0.22, 0.22, 0.38, 0.12],
        "candidates": [
          {
            "id": "block-1-candidate-0",
            "latex": "F=ma",
            "confidence": 0.99,
            "formats": {}
          }
        ]
      }
    ],
    "readingOrder": ["block-0", "block-1"]
  },
  "provider": {
    "id": "org.example.formula-ocr",
    "version": "0.4.2"
  },
  "model": {
    "id": "page-mixed",
    "version": "1.3.0",
    "revision": "sha256:fedcba9876543210"
  },
  "timing": {
    "queueMs": 1,
    "preprocessMs": 31,
    "inferenceMs": 682,
    "postprocessMs": 25,
    "totalMs": 739
  },
  "device": {
    "type": "cpu",
    "precision": "fp32"
  },
  "debug": null
}
```

允许的基础块类型为 `text`、`formula`、`figure`、`table` 和 `unknown`。未知扩展类型必须仍提供 `id`、`type` 与 `bbox`。多页资料在 v1 中应逐页提交；Client 使用自己的 `documentId` 和 `pageIndex` 组织页面，避免单次请求占用过多内存。

### 6.4 调试元数据

只有请求 `options.debug: true` 且 Provider 声明 `debugMetadata: true` 时，响应才可以包含 `debug` 对象，例如：

```json
{
  "preprocessing": {
    "deskewDegrees": -0.7,
    "cropApplied": true
  },
  "tokenCount": 37,
  "warnings": ["low contrast"],
  "traceId": "local-trace-a91c"
}
```

`debug` 不得包含原始图片、Base64 数据、访问令牌、任意绝对路径、完整环境变量或未经清理的异常堆栈。若需保存中间产物，应由用户显式开启实验模式，并用不透明 `artifactId` 引用受控目录中的文件。

## 7. 错误格式与状态码

所有失败响应使用统一结构：

```json
{
  "requestId": "rec-7f7b4b3e",
  "error": {
    "code": "MODEL_NOT_READY",
    "message": "The selected model is not ready.",
    "retryable": true,
    "details": {
      "modelId": "formula-base"
    }
  }
}
```

Provider 应使用以下状态码和稳定错误码：

| HTTP | `error.code` | 含义 | 可重试 |
| --- | --- | --- | --- |
| 400 | `INVALID_REQUEST` | JSON、Base64 或字段无效 | 否 |
| 400 | `API_VERSION_UNSUPPORTED` | API 主版本不兼容 | 否 |
| 401 | `UNAUTHORIZED` | 令牌缺失或不正确 | 否 |
| 404 | `MODEL_NOT_FOUND` | 模型 ID 不存在 | 否 |
| 413 | `INPUT_TOO_LARGE` | 超过字节数或尺寸限制 | 否 |
| 415 | `UNSUPPORTED_MEDIA_TYPE` | 图片格式不受支持 | 否 |
| 422 | `MODE_UNSUPPORTED` | 当前模型不支持该模式 | 否 |
| 422 | `NO_FORMULA_DETECTED` | 图中未检测到可识别公式 | 通常否 |
| 422 | `RECOGNITION_FAILED` | 输入有效但无法生成结果 | 视 `retryable` |
| 429 | `PROVIDER_BUSY` | 并发已满 | 是 |
| 500 | `INTERNAL_ERROR` | 未分类的 Provider 错误 | 视情况 |
| 503 | `MODEL_NOT_READY` | 模型未加载或正在预热 | 是 |
| 503 | `MODEL_UNAVAILABLE` | 权重、设备或运行时不可用 | 视情况 |
| 504 | `TIMEOUT` | 超过请求时限 | 是 |

`message` 面向用户，应该简洁且可本地化；程序逻辑只能依赖稳定的 `code`。`details` 必须经过清理，不能回传敏感运行时信息。对于 `429` 和临时性 `503`，可以同时返回 `Retry-After`。

## 8. 本地安全要求

本地服务仍然可能被浏览器页面或同机恶意进程调用，因此 Provider 至少应该做到：

1. 默认仅绑定回环地址，启动日志明确显示实际监听地址。
2. 每次启动生成高熵 Bearer 令牌；Client 通过 `Authorization: Bearer ...` 发送。令牌不得出现在 URL、命令行历史或普通日志中。
3. 默认不启用 CORS；若确需浏览器直连，只允许精确的受信任 Origin、必要方法和必要请求头，不能使用通配符。
4. 限制请求体字节数、图片像素数、并发数、队列长度和推理超时；解码图片前先检查上限。
5. 不接受任意文件路径、网络 URL、脚本参数或可拼接的 shell 命令。模型清单和可执行文件必须来自受控配置。
6. 对媒体类型、文件魔数、图片解码结果和数值范围进行校验；损坏图片应返回 `INVALID_REQUEST` 或 `UNSUPPORTED_MEDIA_TYPE`。
7. 默认不持久化输入图片和识别内容。诊断日志应只记录请求 ID、模型 ID、耗时、状态码和经过清理的错误摘要。
8. Provider 进程应使用普通用户权限，并把模型缓存、临时文件和实验产物放入用户选择的目录。
9. Client 退出时应终止由它启动的 Provider；外部托管 Provider 则只断开连接，不擅自结束进程。

## 9. Provider 替换流程

Client 应按照以下顺序连接 Provider：

1. 从用户配置读取 `baseUrl` 和令牌引用，不做端口扫描。
2. 调用 `/v1/info`，拒绝不兼容的 API 主版本。
3. 比较所需模式、媒体类型、模型与限制，并据此启用或禁用 UI 功能。
4. 调用 `/v1/health`；只有 `ready: true` 时直接识别。
5. 若模型未就绪且声明支持预热，由用户操作或明确设置触发 `/v1/warmup`。
6. 调用 `/v1/recognize`，展示多个候选与模型身份；置信度为空时不显示虚假的百分比。
7. 保存人工选择或修正时，同时记录 Provider、模型、修订和规范化版本，确保后续实验可复现。

更换 Provider 只改变配置和能力协商，不改变编辑器的 LaTeX 输入、渲染与导出路径。Provider 离线时，手动键盘编辑功能必须继续可用。

## 10. Benchmark 与 correction dataset 兼容

建议将样本元数据保存为 UTF-8 JSON Lines，图片使用内容寻址的独立文件存储。这样可以对同一图片运行多个模型而不重复复制，也可以避免在日志中嵌入大段 Base64。

推荐记录结构：

```json
{
  "schemaVersion": "1.0",
  "sampleId": "sample-20260716-0001",
  "task": "formula",
  "source": {
    "assetRef": "sha256/c8/81/c881b5c1d8c4.png",
    "sha256": "c881b5c1d8c4...",
    "mediaType": "image/png",
    "width": 1480,
    "height": 420,
    "provenance": "user-screenshot",
    "license": "private"
  },
  "groundTruth": {
    "latex": "\\int_a^b f(x)\\,dx",
    "alternatives": [],
    "normalizationVersion": "latex-normalizer/1.0"
  },
  "prediction": {
    "capturedAt": "2026-07-16T03:28:10.000Z",
    "provider": {
      "id": "org.example.formula-ocr",
      "version": "0.4.2"
    },
    "model": {
      "id": "formula-base",
      "version": "2.1.0",
      "revision": "sha256:0123456789abcdef"
    },
    "candidates": [
      {
        "id": "candidate-0",
        "latex": "\\int_a^b f(x)\\,dx",
        "confidence": 0.947
      }
    ],
    "timing": {
      "totalMs": 243
    }
  },
  "correction": {
    "selectedCandidateId": "candidate-0",
    "correctedLatex": "\\int_a^b f(x)\\,dx",
    "reason": "accepted",
    "createdAt": "2026-07-16T03:28:22.000Z"
  },
  "split": "test",
  "tags": ["printed", "integral", "zh-course"]
}
```

文档模式沿用同一外层结构，将 `task` 设为 `document`，并在 `groundTruth.blocks`、`prediction.document.blocks` 中保存相同的块 ID、类型、归一化坐标和阅读顺序。数据工具应保留 Provider 的原始响应快照，同时把用户修正单独记录，不能用修正结果覆盖原始预测。

为支持长期实验，数据集实现还应该：

- 固定并记录 LaTeX 规范化器和渲染器版本；原始 LaTeX 与规范化结果分别保存。
- 至少支持精确匹配、规范化编辑距离和渲染图相似度；文档模式可增加块检测 IoU、阅读顺序和公式块准确率。
- 按 `provider.id + provider.version + model.id + model.version + model.revision` 汇总，避免把不同权重误认为同一个模型。
- 把训练、验证、测试划分固定在样本级；同一来源的裁剪图不得跨划分泄漏。
- 将图片授权、来源和隐私级别作为样本字段。默认 correction 数据只保存在本地，导出或用于训练前必须由用户明确同意。
- 允许增加未知字段，读取器必须忽略自己不认识的扩展字段，以保持向后兼容。

v1 不要求 Provider 接收反馈。未来如增加 `/v1/feedback`，必须作为 `/v1/info` 中显式声明的可选能力，并保持“本地保存、用户确认后发送”为默认策略。

## 11. 最小合规清单

一个可被 Excalidraw Manager 接入的 v1 Provider 至少必须满足：

- 返回有效的 `/v1/info` 和 `/v1/health`。
- 至少支持 `formula` 或 `document` 中的一种模式。
- 接受 Base64 图片并执行严格的类型、尺寸与字节数校验。
- 返回稳定的 Provider/模型版本信息和 `timing.totalMs`。
- 公式模式至少返回一个带 `latex` 的候选，或返回统一错误对象。
- 不能给出可信置信度时返回 `null`。
- 默认仅监听回环地址，不读取请求提供的本机路径或 URL。
- 即使调试模式开启，也不得泄露令牌、原始输入、绝对路径或完整异常环境。
