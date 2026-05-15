using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuickSheetGitst;

/// <summary>
/// QuickSheet extension: Git repository status dashboard.
/// Shows branch, modified/staged/untracked counts, stashes, and recent commits
/// for one or more repos on your wallpaper.
/// </summary>
class Program
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static DateTime _lastFetch = DateTime.MinValue;
    private static List<RepoStatus> _cached = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    static async Task Main()
    {
        using var reader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);

        var register = new { type = "register", prefix = "gitst", version = "1" };
        Console.WriteLine(JsonSerializer.Serialize(register, JsonOpts));

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var msgType = root.GetProperty("type").GetString();

                if (msgType == "activate")
                {
                    var id = root.GetProperty("id").GetString() ?? "";
                    // QuickSheet sends user args as "params" (JSON array of strings)
                    var args = "";
                    if (root.TryGetProperty("params", out var p) && p.ValueKind == JsonValueKind.Array)
                    {
                        var parts = new List<string>();
                        foreach (var item in p.EnumerateArray())
                            parts.Add(item.GetString() ?? "");
                        args = string.Join(",", parts);
                    }
                    await HandleActivate(id, args);
                }
            }
            catch { }
        }
    }

    static async Task HandleActivate(string id, string args)
    {
        SendJson(new { type = "status", id, message = "🔍 Scanning repos..." });

        try
        {
            var repos = await GetRepoStatuses(args);

            if (repos.Count == 0)
            {
                SendWrite(id, new[] { new[] { "No git repos found" } });
                return;
            }

            var grid = new List<string[]>();
            grid.Add(new[] { "Repo", "Branch", "Status", "Stash", "Last Commit" });

            foreach (var r in repos.Take(15))
            {
                string status = "";
                if (r.Modified > 0) status += $"~{r.Modified} ";
                if (r.Staged > 0) status += $"+{r.Staged} ";
                if (r.Untracked > 0) status += $"?{r.Untracked} ";
                if (r.Ahead > 0) status += $"↑{r.Ahead} ";
                if (r.Behind > 0) status += $"↓{r.Behind} ";
                status = status.TrimEnd();
                if (string.IsNullOrEmpty(status)) status = "✅ clean";

                string stash = r.Stashes > 0 ? $"📦{r.Stashes}" : "—";
                string name = r.Name.Length > 16 ? r.Name[..15] + "…" : r.Name;
                string branch = r.Branch.Length > 14 ? r.Branch[..13] + "…" : r.Branch;
                string commit = r.LastCommit.Length > 30 ? r.LastCommit[..29] + "…" : r.LastCommit;

                grid.Add(new[] { name, branch, status, stash, commit });
            }

            SendWrite(id, grid.ToArray());
        }
        catch (Exception ex)
        {
            SendJson(new { type = "error", id, message = $"Error: {ex.Message}" });
        }
    }

    static async Task<List<RepoStatus>> GetRepoStatuses(string args)
    {
        if (DateTime.Now - _lastFetch < CacheDuration && _cached.Count > 0)
            return _cached;

        var paths = new List<string>();

        if (string.IsNullOrWhiteSpace(args))
        {
            // Default: scan current directory for git repos (1 level deep)
            string cwd = Environment.CurrentDirectory;
            if (Directory.Exists(Path.Combine(cwd, ".git")))
                paths.Add(cwd);
            else
            {
                foreach (var dir in Directory.GetDirectories(cwd))
                {
                    if (Directory.Exists(Path.Combine(dir, ".git")))
                        paths.Add(dir);
                }
            }
        }
        else
        {
            // Explicit paths, comma-separated
            foreach (var p in args.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string expanded = Environment.ExpandEnvironmentVariables(p);
                if (expanded.StartsWith("~/"))
                    expanded = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), expanded[2..]);
                if (Directory.Exists(expanded))
                    paths.Add(Path.GetFullPath(expanded));
            }
        }

        var results = new List<RepoStatus>();
        foreach (var path in paths.Take(20))
        {
            var status = await GetSingleRepoStatus(path);
            if (status != null)
                results.Add(status);
        }

        _cached = results;
        _lastFetch = DateTime.Now;
        return results;
    }

    static async Task<RepoStatus?> GetSingleRepoStatus(string repoPath)
    {
        try
        {
            var status = new RepoStatus { Name = Path.GetFileName(repoPath) };

            // Branch name
            status.Branch = (await RunGit(repoPath, "rev-parse --abbrev-ref HEAD")).Trim();

            // Porcelain status
            string porcelain = await RunGit(repoPath, "status --porcelain");
            foreach (var line in porcelain.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length < 2) continue;
                char x = line[0], y = line[1];
                if (x == '?' && y == '?') status.Untracked++;
                else if (x != ' ' && x != '?') status.Staged++;
                if (y != ' ' && y != '?') status.Modified++;
            }

            // Ahead/behind
            string ab = await RunGit(repoPath, "rev-list --left-right --count HEAD...@{upstream}");
            if (!string.IsNullOrWhiteSpace(ab))
            {
                var parts = ab.Trim().Split('\t');
                if (parts.Length == 2)
                {
                    int.TryParse(parts[0], out int ahead);
                    int.TryParse(parts[1], out int behind);
                    status.Ahead = ahead;
                    status.Behind = behind;
                }
            }

            // Stash count
            string stash = await RunGit(repoPath, "stash list");
            status.Stashes = stash.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            if (string.IsNullOrWhiteSpace(stash)) status.Stashes = 0;

            // Last commit subject
            status.LastCommit = (await RunGit(repoPath, "log -1 --format=%s")).Trim();

            return status;
        }
        catch
        {
            return null;
        }
    }

    static async Task<string> RunGit(string workDir, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc == null) return "";

        var output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return output;
    }

    static void SendWrite(string id, string[][] grid)
    {
        SendJson(new { type = "write", id, cells = grid });
    }

    static void SendJson(object obj)
    {
        Console.WriteLine(JsonSerializer.Serialize(obj, JsonOpts));
    }
}

class RepoStatus
{
    public string Name { get; set; } = "";
    public string Branch { get; set; } = "";
    public int Modified { get; set; }
    public int Staged { get; set; }
    public int Untracked { get; set; }
    public int Ahead { get; set; }
    public int Behind { get; set; }
    public int Stashes { get; set; }
    public string LastCommit { get; set; } = "";
}
