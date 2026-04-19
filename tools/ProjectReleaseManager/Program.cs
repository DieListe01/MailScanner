using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

return await ProjectReleaseManagerApp.RunAsync(args);

internal static class ProjectReleaseManagerApp
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var options = CliOptions.Parse(args);
            var config = await AppConfig.LoadAsync(options.ConfigPath);

            return options.Command switch
            {
                "menu" => await ShowMenuAsync(config, options.ConfigPath),
                "discover" => await DiscoverAsync(config, options.ConfigPath),
                "list" => ListProjects(config),
                "status" => await ShowStatusAsync(config, options.ProjectName),
                "bump" => await BumpAsync(config, options.ProjectName, options.VersionInput),
                "release" => await ReleaseAsync(config, options.ProjectName, options.VersionInput, options.SkipBuild),
                _ => ShowHelp()
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fehler: {ex.Message}");
            return 1;
        }
    }

    private static int ShowHelp()
    {
        Console.WriteLine("ProjectReleaseManager");
        Console.WriteLine();
        Console.WriteLine("Befehle:");
        Console.WriteLine("  menu");
        Console.WriteLine("  discover");
        Console.WriteLine("  list");
        Console.WriteLine("  status <projekt>");
        Console.WriteLine("  bump <projekt> <patch|minor|major|x.y.z>");
        Console.WriteLine("  release <projekt> <patch|minor|major|x.y.z> [--skip-build]");
        Console.WriteLine();
        Console.WriteLine("Optional:");
        Console.WriteLine("  --config <pfad-zu-projects.json>");
        return 1;
    }

    private static async Task<int> ShowMenuAsync(AppConfig config, string configPath)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("ProjectReleaseManager");
            Console.WriteLine("1 - Projekte anzeigen");
            Console.WriteLine("2 - Status anzeigen");
            Console.WriteLine("3 - Version erhoehen");
            Console.WriteLine("4 - Release ausfuehren");
            Console.WriteLine("5 - Projekte unter Root suchen");
            Console.WriteLine("0 - Beenden");
            Console.WriteLine();
            Console.Write("Auswahl: ");
            var choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    ListProjects(config);
                    break;
                case "2":
                    {
                        var project = PromptForProject(config);
                        await ShowStatusAsync(config, project.Name);
                        break;
                    }
                case "3":
                    {
                        var project = PromptForProject(config);
                        var versionInput = PromptForVersionInput();
                        await BumpAsync(config, project.Name, versionInput);
                        break;
                    }
                case "4":
                    {
                        var project = PromptForProject(config);
                        var versionInput = PromptForVersionInput();
                        await ReleaseAsync(config, project.Name, versionInput, skipBuild: false);
                        break;
                    }
                case "5":
                    await DiscoverAsync(config, configPath);
                    config = await AppConfig.LoadAsync(configPath);
                    break;
                case "0":
                    return 0;
                default:
                    Console.WriteLine("Ungueltige Auswahl.");
                    break;
            }
        }
    }

    private static ProjectConfig PromptForProject(AppConfig config)
    {
        var projects = config.Projects.OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase).ToList();
        for (var index = 0; index < projects.Count; index++)
        {
            Console.WriteLine($"{index + 1} - {projects[index].Name}");
        }

        Console.Write("Projekt: ");
        var input = Console.ReadLine()?.Trim();
        if (int.TryParse(input, out var selectedIndex) && selectedIndex >= 1 && selectedIndex <= projects.Count)
        {
            return projects[selectedIndex - 1];
        }

        return config.GetProject(input);
    }

    private static string PromptForVersionInput()
    {
        Console.Write("Version (patch/minor/major/x.y.z): ");
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    private static async Task<int> DiscoverAsync(AppConfig config, string configPath)
    {
        if (string.IsNullOrWhiteSpace(config.ProjectsRoot) || !Directory.Exists(config.ProjectsRoot))
        {
            throw new InvalidOperationException($"projectsRoot existiert nicht: {config.ProjectsRoot}");
        }

        var existingPaths = new HashSet<string>(config.Projects.Select(project => config.ResolveRepositoryRoot(project)), StringComparer.OrdinalIgnoreCase);
        var repositories = Directory
            .EnumerateDirectories(config.ProjectsRoot, ".git", SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path!))
            .Where(path => !string.Equals(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), config.ProjectsRoot!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            .Where(path => !existingPaths.Contains(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (repositories.Count == 0)
        {
            Console.WriteLine("Keine neuen Git-Repositories gefunden.");
            return 0;
        }

        Console.WriteLine("Gefundene neue Repositories:");
        for (var index = 0; index < repositories.Count; index++)
        {
            Console.WriteLine($"{index + 1} - {repositories[index]}");
        }

        Console.WriteLine();
        Console.Write("Hinzufuegen (all/Nummern mit Komma/leer=abbrechen): ");
        var selection = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(selection))
        {
            return 0;
        }

        IEnumerable<string> selectedRepositories = string.Equals(selection, "all", StringComparison.OrdinalIgnoreCase)
            ? repositories
            : selection.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => int.Parse(part))
                .Where(index => index >= 1 && index <= repositories.Count)
                .Select(index => repositories[index - 1]);

        var added = 0;
        foreach (var repository in selectedRepositories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var project = ProjectDiscoveryService.CreateProjectConfig(config, repository);
            config.Projects.Add(project);
            added++;
            Console.WriteLine($"Hinzugefuegt: {project.Name}");
        }

        if (added > 0)
        {
            await config.SaveAsync(configPath);
            Console.WriteLine($"Konfiguration aktualisiert: {configPath}");
        }

        return 0;
    }

    private static int ListProjects(AppConfig config)
    {
        Console.WriteLine("Konfigurierte Projekte:");
        foreach (var project in config.Projects.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"- {project.Name} ({config.ResolveRepositoryRoot(project)})");
        }

        return 0;
    }

    private static async Task<int> ShowStatusAsync(AppConfig config, string? projectName)
    {
        var project = config.GetProject(projectName);
        var repositoryRoot = config.ResolveRepositoryRoot(project);
        EnsureDirectoryExists(repositoryRoot, "RepositoryRoot");

        var versionFile = config.ResolveVersionFile(project);
        var localVersion = VersionFileService.ReadVersion(versionFile, project.VersionScheme);
        var gitStatus = await CommandRunner.RunCheckedAsync("git", "status --short", repositoryRoot);
        var latestRelease = await GitHubService.TryGetLatestReleaseTagAsync(repositoryRoot);

        Console.WriteLine($"Projekt       : {project.Name}");
        Console.WriteLine($"Repository    : {repositoryRoot}");
        Console.WriteLine($"Version lokal : {localVersion}");
        Console.WriteLine($"GitHub Release: {(latestRelease ?? "unbekannt")}");
        Console.WriteLine();

        if (string.IsNullOrWhiteSpace(gitStatus.StandardOutput))
        {
            Console.WriteLine("Git-Status    : sauber");
        }
        else
        {
            Console.WriteLine("Git-Status:");
            Console.WriteLine(gitStatus.StandardOutput.TrimEnd());
        }

        return 0;
    }

    private static async Task<int> BumpAsync(AppConfig config, string? projectName, string? versionInput)
    {
        var project = config.GetProject(projectName);
        var repositoryRoot = config.ResolveRepositoryRoot(project);
        EnsureDirectoryExists(repositoryRoot, "RepositoryRoot");
        await EnsureTrackedTreeCleanAsync(repositoryRoot);

        var versionFile = config.ResolveVersionFile(project);
        var currentVersion = SemanticVersion.Parse(VersionFileService.ReadVersion(versionFile, project.VersionScheme));
        var targetVersion = ResolveTargetVersion(currentVersion, versionInput);

        VersionFileService.WriteVersion(versionFile, project.VersionScheme, targetVersion);
        Console.WriteLine($"Version geaendert: {currentVersion} -> {targetVersion}");
        return 0;
    }

    private static async Task<int> ReleaseAsync(AppConfig config, string? projectName, string? versionInput, bool skipBuild)
    {
        var project = config.GetProject(projectName);
        var repositoryRoot = config.ResolveRepositoryRoot(project);
        EnsureDirectoryExists(repositoryRoot, "RepositoryRoot");

        await EnsureTrackedTreeCleanAsync(repositoryRoot);
        await CommandRunner.RunCheckedAsync("git", "pull --ff-only", repositoryRoot);

        var versionFile = config.ResolveVersionFile(project);
        var currentVersion = SemanticVersion.Parse(VersionFileService.ReadVersion(versionFile, project.VersionScheme));
        var targetVersion = ResolveTargetVersion(currentVersion, versionInput);
        var versionChanged = targetVersion != currentVersion;

        if (versionChanged)
        {
            VersionFileService.WriteVersion(versionFile, project.VersionScheme, targetVersion);
            Console.WriteLine($"Version geaendert: {currentVersion} -> {targetVersion}");
        }
        else
        {
            Console.WriteLine($"Version bleibt bei {targetVersion}");
        }

        if (!skipBuild && !string.IsNullOrWhiteSpace(project.BuildCommand))
        {
            Console.WriteLine($"Build: {project.BuildCommand}");
            await CommandRunner.RunShellCheckedAsync(project.BuildCommand, repositoryRoot);
        }

        if (versionChanged)
        {
            var relativeVersionPath = Path.GetRelativePath(repositoryRoot, versionFile).Replace('\\', '/');
            await CommandRunner.RunCheckedAsync("git", $"add -- \"{relativeVersionPath}\"", repositoryRoot);
            await CommandRunner.RunCheckedAsync("git", $"commit -m \"Bump version to {targetVersion}\"", repositoryRoot);
        }

        await CommandRunner.RunCheckedAsync("git", $"push origin {project.ReleaseBranch}", repositoryRoot);

        var releaseTag = $"v{targetVersion}";
        var releaseTitle = project.ReleaseTitleTemplate
            .Replace("{Name}", project.Name, StringComparison.Ordinal)
            .Replace("{Version}", targetVersion.ToString(), StringComparison.Ordinal);

        switch (project.ReleaseStrategy)
        {
            case ReleaseStrategy.GitHubRelease:
                if (await GitHubService.ReleaseExistsAsync(repositoryRoot, releaseTag))
                {
                    Console.WriteLine($"GitHub Release {releaseTag} existiert bereits.");
                }
                else
                {
                    await CommandRunner.RunCheckedAsync(
                        "gh",
                        $"release create {releaseTag} --target {project.ReleaseBranch} --generate-notes --title \"{releaseTitle}\"",
                        repositoryRoot);
                }
                break;

            case ReleaseStrategy.WorkflowDispatch:
                await CommandRunner.RunCheckedAsync(
                    "gh",
                    $"workflow run {project.ReleaseWorkflow} --ref {project.ReleaseBranch} -f version={targetVersion}",
                    repositoryRoot);
                break;
        }

        if (project.WaitForWorkflow)
        {
            var runId = await GitHubService.FindLatestWorkflowRunIdAsync(repositoryRoot, project.ReleaseWorkflow);
            if (runId is not null)
            {
                await CommandRunner.RunCheckedAsync("gh", $"run watch {runId.Value}", repositoryRoot);
            }
        }

        var releaseUrl = await GitHubService.TryGetReleaseUrlAsync(repositoryRoot, releaseTag);
        Console.WriteLine();
        Console.WriteLine($"Release fertig: {releaseTag}");
        if (!string.IsNullOrWhiteSpace(releaseUrl))
        {
            Console.WriteLine($"URL: {releaseUrl}");
        }

        return 0;
    }

    private static async Task EnsureTrackedTreeCleanAsync(string repositoryRoot)
    {
        var status = await CommandRunner.RunCheckedAsync("git", "status --porcelain", repositoryRoot);
        var relevantLines = status.StandardOutput
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.StartsWith("?? ", StringComparison.Ordinal))
            .ToArray();

        if (relevantLines.Length > 0)
        {
            throw new InvalidOperationException(
                "Das Repository hat noch getrackte Aenderungen. Bitte zuerst committen oder aufraeumen:\n" +
                string.Join(Environment.NewLine, relevantLines));
        }
    }

    private static SemanticVersion ResolveTargetVersion(SemanticVersion currentVersion, string? versionInput)
    {
        if (string.IsNullOrWhiteSpace(versionInput))
        {
            throw new InvalidOperationException("Bitte patch, minor, major oder eine konkrete Version angeben.");
        }

        return versionInput.Trim().ToLowerInvariant() switch
        {
            "patch" => currentVersion with { Patch = currentVersion.Patch + 1 },
            "minor" => currentVersion with { Minor = currentVersion.Minor + 1, Patch = 0 },
            "major" => currentVersion with { Major = currentVersion.Major + 1, Minor = 0, Patch = 0 },
            _ => SemanticVersion.Parse(versionInput)
        };
    }

    private static void EnsureDirectoryExists(string path, string label)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"{label} existiert nicht: {path}");
        }
    }
}

internal sealed class CliOptions
{
    public string Command { get; private set; } = string.Empty;
    public string? ProjectName { get; private set; }
    public string? VersionInput { get; private set; }
    public string ConfigPath { get; private set; } = FindDefaultConfigPath();
    public bool SkipBuild { get; private set; }

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();
        if (args.Length == 0)
        {
            options.Command = "menu";
            return options;
        }

        var values = new Queue<string>(args);

        while (values.Count > 0)
        {
            var next = values.Dequeue();
            if (next.Equals("--config", StringComparison.OrdinalIgnoreCase))
            {
                options.ConfigPath = values.Count > 0
                    ? Path.GetFullPath(values.Dequeue())
                    : throw new InvalidOperationException("--config braucht einen Dateipfad.");
                continue;
            }

            if (next.Equals("--skip-build", StringComparison.OrdinalIgnoreCase))
            {
                options.SkipBuild = true;
                continue;
            }

            if (string.IsNullOrWhiteSpace(options.Command))
            {
                options.Command = next;
                continue;
            }

            if (options.ProjectName is null)
            {
                options.ProjectName = next;
                continue;
            }

            if (options.VersionInput is null)
            {
                options.VersionInput = next;
                continue;
            }

            throw new InvalidOperationException($"Unbekanntes Argument: {next}");
        }

        return options;
    }

    private static string FindDefaultConfigPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "projects.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "tools", "ProjectReleaseManager", "projects.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "projects.json")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return Path.GetFullPath(candidates[0]);
    }
}

internal sealed class AppConfig
{
    [JsonPropertyName("projectsRoot")]
    public string? ProjectsRoot { get; set; }

    [JsonPropertyName("projects")]
    public List<ProjectConfig> Projects { get; set; } = new();

    public static async Task<AppConfig> LoadAsync(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Konfigurationsdatei nicht gefunden: {configPath}");
        }

        await using var stream = File.OpenRead(configPath);
        var config = await JsonSerializer.DeserializeAsync<AppConfig>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        if (config is null || config.Projects.Count == 0)
        {
            throw new InvalidOperationException("Die Konfiguration enthaelt keine Projekte.");
        }

        config.ProjectsRoot = ExpandPath(config.ProjectsRoot);
        return config;
    }

    public async Task SaveAsync(string configPath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        var directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(this, options), new UTF8Encoding(false));
    }

    public ProjectConfig GetProject(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Bitte einen Projektnamen angeben.");
        }

        var project = Projects.FirstOrDefault(project => string.Equals(project.Name, name, StringComparison.OrdinalIgnoreCase));
        if (project is null)
        {
            throw new InvalidOperationException($"Projekt nicht gefunden: {name}");
        }

        return project;
    }

    public string ResolveRepositoryRoot(ProjectConfig project)
    {
        return ResolvePath(project.RepositoryRoot);
    }

    public string ResolveVersionFile(ProjectConfig project)
    {
        var repositoryRoot = ResolveRepositoryRoot(project);
        var versionFile = ExpandPath(project.VersionFile);
        return Path.IsPathRooted(versionFile)
            ? versionFile
            : Path.GetFullPath(Path.Combine(repositoryRoot, versionFile));
    }

    private string ResolvePath(string rawPath)
    {
        var expanded = ExpandPath(rawPath);
        if (Path.IsPathRooted(expanded))
        {
            return expanded;
        }

        if (string.IsNullOrWhiteSpace(ProjectsRoot))
        {
            return Path.GetFullPath(expanded);
        }

        return Path.GetFullPath(Path.Combine(ProjectsRoot, expanded));
    }

    private static string ExpandPath(string? rawPath)
    {
        return Environment.ExpandEnvironmentVariables(rawPath ?? string.Empty);
    }
}

internal sealed class ProjectConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("repositoryRoot")]
    public string RepositoryRoot { get; set; } = string.Empty;

    [JsonPropertyName("versionFile")]
    public string VersionFile { get; set; } = "Directory.Build.props";

    [JsonPropertyName("versionScheme")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VersionScheme VersionScheme { get; set; } = VersionScheme.DirectoryBuildProps;

    [JsonPropertyName("buildCommand")]
    public string? BuildCommand { get; set; }

    [JsonPropertyName("releaseBranch")]
    public string ReleaseBranch { get; set; } = "main";

    [JsonPropertyName("releaseWorkflow")]
    public string ReleaseWorkflow { get; set; } = "release.yml";

    [JsonPropertyName("releaseStrategy")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ReleaseStrategy ReleaseStrategy { get; set; } = ReleaseStrategy.GitHubRelease;

    [JsonPropertyName("releaseTitleTemplate")]
    public string ReleaseTitleTemplate { get; set; } = "{Name} {Version}";

    [JsonPropertyName("waitForWorkflow")]
    public bool WaitForWorkflow { get; set; } = true;
}

internal enum VersionScheme
{
    DirectoryBuildProps,
    PackageJson
}

internal enum ReleaseStrategy
{
    GitHubRelease,
    WorkflowDispatch
}

internal static class VersionFileService
{
    public static string ReadVersion(string filePath, VersionScheme scheme)
    {
        var text = File.ReadAllText(filePath, Encoding.UTF8);
        return scheme switch
        {
            VersionScheme.DirectoryBuildProps => ReadXmlTag(text, "Version"),
            VersionScheme.PackageJson => ReadJsonVersion(text),
            _ => throw new InvalidOperationException($"VersionScheme nicht unterstuetzt: {scheme}")
        };
    }

    public static void WriteVersion(string filePath, VersionScheme scheme, SemanticVersion version)
    {
        var text = File.ReadAllText(filePath, Encoding.UTF8);
        var updated = scheme switch
        {
            VersionScheme.DirectoryBuildProps => WriteDirectoryBuildProps(text, version),
            VersionScheme.PackageJson => WritePackageJson(text, version),
            _ => throw new InvalidOperationException($"VersionScheme nicht unterstuetzt: {scheme}")
        };

        File.WriteAllText(filePath, updated, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string ReadXmlTag(string content, string tagName)
    {
        var match = Regex.Match(content, $"<{tagName}>(.*?)</{tagName}>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
        {
            throw new InvalidOperationException($"Tag <{tagName}> nicht gefunden.");
        }

        return match.Groups[1].Value.Trim();
    }

    private static string WriteDirectoryBuildProps(string content, SemanticVersion version)
    {
        var versionText = version.ToString();
        var assemblyVersion = $"{versionText}.0";
        content = ReplaceXmlTag(content, "Version", versionText);
        content = ReplaceXmlTag(content, "AssemblyVersion", assemblyVersion);
        content = ReplaceXmlTag(content, "FileVersion", assemblyVersion);
        content = ReplaceXmlTag(content, "InformationalVersion", versionText);
        return content;
    }

    private static string ReplaceXmlTag(string content, string tagName, string value)
    {
        var updated = Regex.Replace(content, $"<{tagName}>.*?</{tagName}>", $"<{tagName}>{value}</{tagName}>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (ReferenceEquals(updated, content) || updated == content)
        {
            throw new InvalidOperationException($"Tag <{tagName}> konnte nicht aktualisiert werden.");
        }

        return updated;
    }

    private static string ReadJsonVersion(string content)
    {
        using var document = JsonDocument.Parse(content);
        if (!document.RootElement.TryGetProperty("version", out var versionProperty))
        {
            throw new InvalidOperationException("package.json enthaelt kein 'version'-Feld.");
        }

        return versionProperty.GetString() ?? throw new InvalidOperationException("package.json 'version' ist leer.");
    }

    private static string WritePackageJson(string content, SemanticVersion version)
    {
        return Regex.Replace(content, "\"version\"\\s*:\\s*\".*?\"", $"\"version\": \"{version}\"", RegexOptions.IgnoreCase | RegexOptions.Singleline);
    }
}

internal readonly record struct SemanticVersion(int Major, int Minor, int Patch)
{
    public static SemanticVersion Parse(string text)
    {
        var cleaned = text.Trim().TrimStart('v', 'V');
        var parts = cleaned.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor) || !int.TryParse(parts[2], out var patch))
        {
            throw new InvalidOperationException($"Ungueltige Version: {text}. Erwartet x.y.z");
        }

        return new SemanticVersion(major, minor, patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

internal static class CommandRunner
{
    public static Task<CommandResult> RunCheckedAsync(string fileName, string arguments, string workingDirectory)
        => RunInternalAsync(fileName, arguments, workingDirectory, shell: false);

    public static Task<CommandResult> RunShellCheckedAsync(string command, string workingDirectory)
        => RunInternalAsync("cmd.exe", $"/c {command}", workingDirectory, shell: false);

    private static async Task<CommandResult> RunInternalAsync(string fileName, string arguments, string workingDirectory, bool shell)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = shell,
            RedirectStandardOutput = !shell,
            RedirectStandardError = !shell,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        if (!shell)
        {
            process.OutputDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data is not null)
                {
                    stdout.AppendLine(eventArgs.Data);
                    Console.WriteLine(eventArgs.Data);
                }
            };

            process.ErrorDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data is not null)
                {
                    stderr.AppendLine(eventArgs.Data);
                    Console.Error.WriteLine(eventArgs.Data);
                }
            };
        }

        if (!process.Start())
        {
            throw new InvalidOperationException($"Prozess konnte nicht gestartet werden: {fileName} {arguments}");
        }

        if (!shell)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        await process.WaitForExitAsync();

        var result = new CommandResult(process.ExitCode, stdout.ToString(), stderr.ToString());
        if (result.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(result.StandardError)
                ? $"Befehl fehlgeschlagen: {fileName} {arguments}"
                : result.StandardError.Trim();
            throw new InvalidOperationException(message);
        }

        return result;
    }
}

internal readonly record struct CommandResult(int ExitCode, string StandardOutput, string StandardError);

internal static class ProjectDiscoveryService
{
    public static ProjectConfig CreateProjectConfig(AppConfig config, string repositoryRoot)
    {
        var name = Path.GetFileName(repositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var versionFile = DetectVersionFile(repositoryRoot);
        var versionScheme = versionFile.EndsWith("package.json", StringComparison.OrdinalIgnoreCase)
            ? VersionScheme.PackageJson
            : VersionScheme.DirectoryBuildProps;
        var releaseWorkflow = DetectReleaseWorkflow(repositoryRoot);
        var releaseStrategy = string.IsNullOrWhiteSpace(releaseWorkflow)
            ? ReleaseStrategy.GitHubRelease
            : ReleaseStrategy.GitHubRelease;

        return new ProjectConfig
        {
            Name = name,
            RepositoryRoot = MakeRelativeOrAbsolute(config.ProjectsRoot, repositoryRoot),
            VersionFile = Path.GetRelativePath(repositoryRoot, versionFile).Replace('\\', '/'),
            VersionScheme = versionScheme,
            BuildCommand = DetectBuildCommand(repositoryRoot),
            ReleaseBranch = "main",
            ReleaseWorkflow = string.IsNullOrWhiteSpace(releaseWorkflow) ? "release.yml" : releaseWorkflow,
            ReleaseStrategy = releaseStrategy,
            ReleaseTitleTemplate = $"{name} {{Version}}",
            WaitForWorkflow = !string.IsNullOrWhiteSpace(releaseWorkflow)
        };
    }

    private static string DetectVersionFile(string repositoryRoot)
    {
        var directoryBuildProps = Path.Combine(repositoryRoot, "Directory.Build.props");
        if (File.Exists(directoryBuildProps))
        {
            return directoryBuildProps;
        }

        var packageJson = Path.Combine(repositoryRoot, "package.json");
        if (File.Exists(packageJson))
        {
            return packageJson;
        }

        throw new InvalidOperationException($"Keine bekannte Versionsdatei in {repositoryRoot} gefunden.");
    }

    private static string DetectBuildCommand(string repositoryRoot)
    {
        var slnx = Directory.GetFiles(repositoryRoot, "*.slnx", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (slnx is not null)
        {
            return $"dotnet build \"{Path.GetFileName(slnx)}\" -c Release";
        }

        var sln = Directory.GetFiles(repositoryRoot, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (sln is not null)
        {
            return $"dotnet build \"{Path.GetFileName(sln)}\" -c Release";
        }

        var appProject = Directory.GetFiles(Path.Combine(repositoryRoot, "src"), "*.csproj", SearchOption.AllDirectories)
            .FirstOrDefault(path => Path.GetFileName(path).Contains("App", StringComparison.OrdinalIgnoreCase));
        if (appProject is not null)
        {
            return $"dotnet build \"{Path.GetRelativePath(repositoryRoot, appProject).Replace('\\', '/')}\" -c Release";
        }

        return "dotnet build -c Release";
    }

    private static string? DetectReleaseWorkflow(string repositoryRoot)
    {
        var workflowDirectory = Path.Combine(repositoryRoot, ".github", "workflows");
        if (!Directory.Exists(workflowDirectory))
        {
            return null;
        }

        var releaseWorkflow = Directory.GetFiles(workflowDirectory, "*.yml", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .FirstOrDefault(fileName => fileName is not null && fileName.Contains("release", StringComparison.OrdinalIgnoreCase));

        return releaseWorkflow;
    }

    private static string MakeRelativeOrAbsolute(string? root, string path)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return path;
        }

        var relative = Path.GetRelativePath(root, path);
        return relative.StartsWith("..", StringComparison.Ordinal) ? path : relative.Replace('\\', '/');
    }
}

internal static class GitHubService
{
    public static async Task<string?> TryGetLatestReleaseTagAsync(string repositoryRoot)
    {
        try
        {
            var result = await CommandRunner.RunCheckedAsync("gh", "release list --limit 1 --json tagName", repositoryRoot);
            using var document = JsonDocument.Parse(result.StandardOutput);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            {
                return null;
            }

            return document.RootElement[0].GetProperty("tagName").GetString();
        }
        catch
        {
            return null;
        }
    }

    public static async Task<bool> ReleaseExistsAsync(string repositoryRoot, string tag)
    {
        try
        {
            await CommandRunner.RunCheckedAsync("gh", $"release view {tag} --json url", repositoryRoot);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<long?> FindLatestWorkflowRunIdAsync(string repositoryRoot, string workflowFile)
    {
        await Task.Delay(TimeSpan.FromSeconds(5));
        var result = await CommandRunner.RunCheckedAsync(
            "gh",
            $"run list --workflow {workflowFile} --limit 1 --json databaseId",
            repositoryRoot);

        using var document = JsonDocument.Parse(result.StandardOutput);
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
        {
            return null;
        }

        return document.RootElement[0].GetProperty("databaseId").GetInt64();
    }

    public static async Task<string?> TryGetReleaseUrlAsync(string repositoryRoot, string tag)
    {
        try
        {
            var result = await CommandRunner.RunCheckedAsync("gh", $"release view {tag} --json url", repositoryRoot);
            using var document = JsonDocument.Parse(result.StandardOutput);
            return document.RootElement.GetProperty("url").GetString();
        }
        catch
        {
            return null;
        }
    }
}
