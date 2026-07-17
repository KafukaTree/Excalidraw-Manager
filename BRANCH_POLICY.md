# Product-line branch policy

This repository maintains two independent, long-lived product lines.

| Branch | Product line | Release tags |
| --- | --- | --- |
| `main` | Original Windows WinForms edition | `windows-v*` |
| `edition/cross-platform` | Tauri edition for macOS, Windows, and Linux | `cross-platform-v*` |

## Non-merge policy

- Never merge `main` into `edition/cross-platform`.
- Never merge `edition/cross-platform` into `main`.
- Never open a pull request whose head and base are these two product-line
  branches.
- Share a necessary fix only by reviewing and cherry-picking the smallest
  relevant commit. Resolve product-line-specific behavior in a new commit on
  the destination branch.
- Build, test, tag, and publish the two product lines independently.
- Changing this policy requires an explicit repository-owner decision. It must
  not happen as a side effect of feature development or release preparation.

Git and repository administrators can technically override branch protections.
The branch names, this policy, and GitHub protection rules together make an
accidental cross-product merge a deliberate and visible action instead of a
normal development step.

---

# 产品线分支政策

本仓库长期维护两条互相独立的产品线：

| 分支 | 产品线 | 发布标签 |
| --- | --- | --- |
| `main` | 原 Windows WinForms 版本 | `windows-v*` |
| `edition/cross-platform` | 面向 macOS、Windows、Linux 的 Tauri 版本 | `cross-platform-v*` |

## 禁止整支合并

- 禁止把 `main` 合并到 `edition/cross-platform`。
- 禁止把 `edition/cross-platform` 合并到 `main`。
- 禁止在这两条产品线分支之间创建 Pull Request。
- 确需共享的修复只能先审查，再挑选最小范围的单个提交进行 `cherry-pick`；
  产品线差异必须在目标分支另行处理。
- 两条产品线独立构建、测试、打标签和发布。
- 只有仓库所有者明确决定时才能修改本政策，功能开发或发布流程不得顺带改变它。

Git 和仓库管理员在技术上始终可以绕过保护规则。分支命名、本政策与 GitHub
保护规则的作用，是让跨产品线合并成为明确且可见的管理决定，而不是普通开发操作。
