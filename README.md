# quicksheet-gitst

A [QuickSheet](https://github.com/cemheren/QuickSheet) extension that shows git repository status on your desktop wallpaper. See branch, modified/staged/untracked file counts, stashes, and last commit at a glance.

## What it does

Writes a `git status`-style dashboard into the grid, one row per repo, starting at the cell where you invoke it. Refreshes every 30 seconds.

## Install

In any QuickSheet cell:

```
ext: github:Deskworks/quicksheet-gitst
```

Requires `git` CLI and .NET 9 SDK.

## Usage

Type one of these in a cell:

| Cell contents                      | Shows                                          |
|------------------------------------|------------------------------------------------|
| `gitst:`                           | All repos in current dir (1 level deep)        |
| `gitst: ~/Projects/myapp`          | A single specific repo                         |
| `gitst: ~/proj/a, ~/proj/b`        | Multiple repos (comma-separated)               |

Supports `~` expansion and environment variables.

## Example

Input — type `gitst:` in cell **A1**.

Output — extension writes header + one row per repo, starting at A1:

|     | A           | B        | C            | D     | E                          |
|-----|-------------|----------|--------------|-------|----------------------------|
| **1** | Repo        | Branch   | Status       | Stash | Last Commit                |
| **2** | QuickSheet  | main     | ~3 +1 ?2     | 📦2   | fix: handle array format   |
| **3** | myapp       | feature  | ✅ clean     | —     | feat: add OAuth login      |
| **4** | infra       | main     | ~1 ↑2        | 📦1   | update k8s manifests       |

Up to 15 repos are written. When no repos are found, A1 gets `No git repos found`.

### Status column

| Symbol     | Meaning                          |
|------------|----------------------------------|
| `~N`       | Modified files (unstaged)        |
| `+N`       | Staged files                     |
| `?N`       | Untracked files                  |
| `↑N`       | Commits ahead of upstream        |
| `↓N`       | Commits behind upstream          |
| `✅ clean` | Working tree is clean            |
| `📦N`      | Stash entries                    |

## How it works

Runs `git` commands (`rev-parse`, `status --porcelain`, `rev-list`, `stash list`, `log`) per repo. Results cached for 30 seconds.

## License

MIT
