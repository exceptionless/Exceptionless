using System.Net;
using System.Net.Sockets;
using System.Text;

public sealed record WorktreePorts(
    int DashboardHttps,
    int DashboardHttp,
    int DashboardOtlp,
    int ResourceService,
    int ApiHttp,
    int ApiHttps,
    int JobsHttp,
    int OldAppHttp,
    int OldAppHttps,
    int OldAppLiveReload,
    int AppHttps,
    int Storybook,
    int EmailStorybook)
{
    public string ApiHttpUrl => $"http://localhost:{ApiHttp}";
    public string ApiHttpsUrl => $"https://localhost:{ApiHttps}";
    public string OldAppHttpsUrl => $"https://angular-ex.dev.localhost:{OldAppHttps}";
}

public static class WorktreeScope
{
    public static string? Resolve()
    {
        string? explicitScope = Environment.GetEnvironmentVariable("Scope");
        if (!String.IsNullOrWhiteSpace(explicitScope))
        {
            return Sanitize(explicitScope);
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string dotGit = Path.Combine(dir.FullName, ".git");
            if (File.Exists(dotGit))
            {
                return Sanitize(ResolveGitWorktreeName(dotGit) ?? dir.Name);
            }

            if (Directory.Exists(dotGit))
            {
                return null;
            }
        }

        return null;
    }

    public static WorktreePorts AssignFreePorts()
    {
        int[] ports = FreePorts(13);
        var assignments = new WorktreePorts(
            ports[0],
            ports[1],
            ports[2],
            ports[3],
            ports[4],
            ports[5],
            ports[6],
            ports[7],
            ports[8],
            ports[9],
            ports[10],
            ports[11],
            ports[12]);

        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", $"https://localhost:{assignments.DashboardHttps};http://localhost:{assignments.DashboardHttp}");
        Environment.SetEnvironmentVariable("DOTNET_DASHBOARD_OTLP_ENDPOINT_URL", $"https://localhost:{assignments.DashboardOtlp}");
        Environment.SetEnvironmentVariable("DOTNET_RESOURCE_SERVICE_ENDPOINT_URL", $"https://localhost:{assignments.ResourceService}");

        return assignments;
    }

    private static int[] FreePorts(int count)
    {
        var listeners = new List<TcpListener>(count);
        try
        {
            for (int i = 0; i < count; i++)
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                listeners.Add(listener);
            }

            return listeners.Select(l => ((IPEndPoint)l.LocalEndpoint).Port).ToArray();
        }
        finally
        {
            foreach (var listener in listeners)
            {
                listener.Stop();
            }
        }
    }

    private static string? ResolveGitWorktreeName(string dotGitPath)
    {
        string content = File.ReadAllText(dotGitPath).Trim();
        const string gitDirPrefix = "gitdir:";
        if (!content.StartsWith(gitDirPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string gitDir = content[gitDirPrefix.Length..].Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFileName(gitDir);
    }

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char ch in value.ToLowerInvariant())
        {
            builder.Append(Char.IsLetterOrDigit(ch) ? ch : '-');
        }

        string cleaned = builder.ToString().Trim('-');
        while (cleaned.Contains("--", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);
        }

        return cleaned.Length > 40 ? cleaned[..40].Trim('-') : cleaned;
    }
}
