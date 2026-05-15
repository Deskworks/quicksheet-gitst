# quicksheet-gitst

A [QuickSheet](https://github.com/cemheren/QuickSheet) extension that shows git repository status on your desktop wallpaper. See branch, modified/staged/untracked file counts, stashes, and last commit at a glance.

## What it does

Scans one or more git repositories and displays a status dashboard — like a persistent `git status` for all your projects.

```
┌──────────────┬──────────┬─────────────────┬───────┬────────────────────────────┐
│ Repo         │ Branch   │ Status          │ Stash │ Last Commit                │
├──────────────┼──────────┼─────────────────┼───────┼────────────────────────────┤
│ QuickSheet   │ main     │ ~3 +1 ?2        │ 📦2   │ fix: handle array format   │
│ myapp        │ feature  │ ✅ clean         │ —     │ feat: add OAuth login      │
│ infra        │ main     │ ~1 ↑2           │ 📦1   │ update k8s manifests       │
└──────────────┴──────────┴─────────────────┴───────┴────────────────────────────┘
```

### Status symbols

| Symbol | Meaning |
|--------|---------|
| `~N` | Modified files (unstaged) |
| `+N` | Staged files |
| `?N` | Untracked files |
| `↑N` | Commits ahead of upstream |
| `↓N` | Commits behind upstream |
| `✅ clean` | Working tree is clean |
| `📦N` | Stash entries |

## Requirements

- `git` CLI
- .NET 9 SDK

## Install

In any QuickSheet cell:

```
ext: github:cemheren/quicksheet-gitst
```

## Usage

| Command | Description |
|---------|-------------|
| `gitst:` | Scan current directory (and 1 level deep) for repos |
| `gitst: ~/Projects/myapp` | Specific repo path |
| `gitst: ~/proj/a, ~/proj/b` | Multiple repos (comma-separated) |

Supports `~` expansion and environment variables.

## How it works

Runs `git` commands (`rev-parse`, `status --porcelain`, `rev-list`, `stash list`, `log`) for each repo path. Results cached for 30 seconds.

## License

MIT
