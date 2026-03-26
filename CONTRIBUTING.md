# 🤝 Contributing to SlimeNexus

First off — **thank you** for taking the time to contribute! 🟢

SlimeNexus is an open-source project and we welcome contributions of all kinds: bug fixes, new features, documentation improvements, translations, and more.

---

## 📋 Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [How to Report Bugs](#how-to-report-bugs)
- [How to Suggest Features](#how-to-suggest-features)
- [Development Workflow](#development-workflow)
- [Coding Standards](#coding-standards)
- [Commit Message Convention](#commit-message-convention)
- [Pull Request Process](#pull-request-process)

---

## Code of Conduct

This project and everyone participating in it is governed by our [Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code.

---

## Getting Started

### Prerequisites

| Tool | Version |
|------|---------|
| [.NET SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) | 9.0+ |
| [Git](https://git-scm.com/) | Latest |
| [Ollama](https://ollama.ai/) | Latest (for AI features) |

### Fork & Clone

```bash
# 1. Fork the repository via GitHub UI
# 2. Clone your fork
git clone https://github.com/<your-username>/SlimeNexus.git
cd SlimeNexus

# 3. Add upstream remote
git remote add upstream https://github.com/ItaloKbb/SlimeNexus.git

# 4. Restore packages & build
dotnet restore
dotnet build
```

---

## How to Report Bugs

Before creating a bug report, please check the [existing issues](https://github.com/ItaloKbb/SlimeNexus/issues) to avoid duplicates.

Use the **🐛 Bug Report** issue template and include:

- Your OS and version (Windows 11, Ubuntu 24.04, macOS 15…)
- Your GPU model (especially if AMD)
- Steps to reproduce
- Expected vs actual behavior
- Relevant logs (found in `%APPDATA%\SlimeNexus\logs` on Windows)

---

## How to Suggest Features

We love new ideas! Open a **💡 Feature Request** issue with:

- A clear description of the problem you're solving
- Your proposed solution
- Any alternative solutions you considered
- Whether you'd like to implement it yourself

---

## Development Workflow

```bash
# 1. Keep your fork in sync
git fetch upstream
git checkout main
git merge upstream/main

# 2. Create a feature branch
git checkout -b feat/your-feature-name

# 3. Make your changes
# 4. Run tests
dotnet test

# 5. Commit using Conventional Commits (see below)
git commit -m "feat(core): add happiness decay over time"

# 6. Push and open a PR
git push origin feat/your-feature-name
```

---

## Coding Standards

- **Language**: C# 13 with nullable reference types enabled (`<Nullable>enable</Nullable>`)
- **Style**: Follow [Microsoft's C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- **Architecture**: Respect the Clean Architecture layers — never reference `Infrastructure` from `Core`
- **No magic strings**: Use `const` or strongly-typed enums
- **XML Docs**: All public APIs must have `/// <summary>` documentation
- **Tests**: New features should include unit tests in the appropriate `tests/` project

### Layer Rules

| From → To | Allowed? |
|-----------|----------|
| `Core` → any | ❌ Core has no dependencies |
| `Application` → `Core` | ✅ |
| `Infrastructure` → `Core` | ✅ |
| `UI` → `Core`, `Infrastructure`, `Api` | ✅ |
| `Api` → `Core`, `Infrastructure` | ✅ |
| `Infrastructure` → `UI` | ❌ |

---

## Commit Message Convention

We follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <description>

[optional body]
[optional footer]
```

### Types

| Type | Description |
|------|-------------|
| `feat` | New feature |
| `fix` | Bug fix |
| `docs` | Documentation only |
| `style` | Formatting, no logic change |
| `refactor` | Code restructure without fix/feat |
| `test` | Adding/fixing tests |
| `chore` | Build process, CI, tooling |
| `perf` | Performance improvement |

### Examples

```
feat(hardware): add AMD RDNA 3 GPU temperature sensor
fix(api): handle null slime state on startup
docs(readme): add hardware compatibility table
chore(ci): update dotnet version to 9.0.5
```

---

## Pull Request Process

1. **Ensure tests pass** locally (`dotnet test`)
2. **Update documentation** if your change affects public APIs or user-facing behavior
3. **Fill in the PR template** completely — incomplete PRs may be closed
4. **Link related issues** using `Closes #123` in your PR description
5. **Request a review** from a maintainer
6. **Respond to feedback** promptly — PRs with no response for 14 days may be closed

### PR Title Format

Follow the same Conventional Commits format:

```
feat(ui): add system tray animation for happy slime state
```

---

## 🏷️ Labels

| Label | Description |
|-------|-------------|
| `good first issue` | Great for newcomers |
| `help wanted` | Extra attention needed |
| `bug` | Confirmed bug |
| `enhancement` | New feature or request |
| `hardware:amd` | AMD GPU specific |
| `ai:ollama` | Ollama/Llama 3 related |
| `platform:windows` / `platform:linux` / `platform:mac` | OS-specific |

---

Thank you for making SlimeNexus better! 🟢
