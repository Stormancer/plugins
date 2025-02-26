using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord.WebSocket;
using NuGet.Versioning;
using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using Stormancer.Build;
using static Nuke.Common.Tools.DotNet.DotNetTasks;


[UnsetVisualStudioEnvironmentVariables]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public const string BuildType = "Plugins";
    public const string ReleaseNugetSource = "https://api.nuget.org/v3/index.json";

    public static int Main() => Execute<Build>(x => x.Publish);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly string Configuration = IsLocalBuild ? "Debug" : "Release";

    [Parameter("Nuget secret key")]
    readonly string NugetSecretKey = default!;

    [PathVariable]
    readonly Tool git = default!;

    [Parameter("Discord bot token")]
    readonly string DiscordToken = default!;

    [Parameter("Id of the Discord channel the bot must send messages to.")]
    readonly string DiscordChannelId = "456087351610048512";

    private DiscordSocketClient _client = new DiscordSocketClient();
    private SocketTextChannel? _channel;

    [Solution] readonly Solution Solution = default!;
    [GitRepository] readonly GitRepository GitRepository = default!;

    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath OutputDirectory => RootDirectory / "output";

   
    private async Task StartDiscord()
    {
        var tcs = new TaskCompletionSource<bool>();
        _client.Log += _client_Log;
        await _client.LoginAsync(Discord.TokenType.Bot, DiscordToken);
        await _client.StartAsync();
        _client.Ready += () =>
        {
            tcs.SetResult(true);
            return Task.CompletedTask;
        };
        await tcs.Task.ConfigureAwait(false);
        _channel = (SocketTextChannel)_client.GetChannel(ulong.Parse(DiscordChannelId));

    }

    private Task _client_Log(Discord.LogMessage arg)
    {
        switch (arg.Severity)
        {
            case Discord.LogSeverity.Critical:
                Serilog.Log.Error(arg.Message);
                break;
            case Discord.LogSeverity.Error:
                Serilog.Log.Error(arg.Message);
                break;
            case Discord.LogSeverity.Warning:
                Serilog.Log.Warning(arg.Message);
                break;
            case Discord.LogSeverity.Info:
                Serilog.Log.Information(arg.Message);
                break;
            case Discord.LogSeverity.Verbose:
                Serilog.Log.Verbose(arg.Message);
                break;
            case Discord.LogSeverity.Debug:
                break;
        }
        return Task.CompletedTask;

    }

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(p=>p.DeleteDirectory());
            OutputDirectory.CreateOrCleanDirectory();
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });
    Target Publish => _ => _
    .DependsOn(Compile)
    .Executes(async () =>
    {
        await StartDiscord();
        Debug.Assert(_channel != null);
        foreach (var project in Solution.AllProjects.Where(p => {
            if (!p.Path.HasExtension(".csproj"))
            {
                return false;
            }
            var property = p.GetProperty<string>("StrmPushPackage");
            
            if(!string.IsNullOrEmpty(property))
            {
                return property == "true";
            }
            return !p.Name.StartsWith("_") && !p.Name.Contains("Test") && !p.Name.Contains("sample", StringComparison.InvariantCultureIgnoreCase);
            }))
        {
            var changelogFile = Path.Combine(project.Directory, "Changelog.rst");

            var changeLog = Stormancer.Build.ChangeLog.ReadFrom(changelogFile);
            if (changeLog == null)
            {
                for (var i = 0; i < 2; i++)
                {
                    try
                    {
                        await _channel.SendMessageAsync($"*[{BuildType} {Configuration}]* Publish skipped for `{project.Name}`. No changelog found.");
                        break;
                    }
                    catch (Exception)
                    {
                        await Task.Delay(500);
                    }
                }
                continue;
            }

            foreach (var file in Directory.GetFiles(project.Directory, "*.nupkg", SearchOption.AllDirectories))
            {
                File.Delete(file);
            }
            foreach (var file in Directory.GetFiles(project.Directory, "*.snupkg", SearchOption.AllDirectories))
            {
                File.Delete(file);
            }
            var output = DotNetPack(s => s
                .SetProject(project)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                );

            string? packagePath = null;
            foreach (var line in output)
            {
                var match = Regex.Match(line.Text, "'(?<path>[A-Za-z:\\\\\\./0-9-]+\\.nupkg)'");
                // "Successfully created package 'E:\repo\src\server\grid\Stormancer.Server.Bootstrapper\bin\Debug\Stormancer.Server.Bootstrapper.4.1.0-pre.nupkg'."
                if (match.Success)
                {
                    packagePath = match.Groups["path"].Value;
                }
            }

            if (packagePath == null)
            {
                Serilog.Log.Warning($"No package was created for {project.Name}");
                continue;
            }
            var startIndex = packagePath.LastIndexOf(project.Name) + project.Name.Length + 1;

            var versionString = packagePath.Substring(startIndex, packagePath.Length - startIndex - ".nupkg".Length);
            var currentPackageVersion = new NuGetVersion(versionString);
            var versionStr = await NuGetVersionResolver.GetLatestVersion(project.Name, Configuration == "Debug");
            
            //await _channel.SendMessageAsync($"*[{BuildType} {Configuration}]* `{project.Name}` : Current package on nuget: {versionStr}.");

            var version = versionStr != null ? new NuGetVersion(versionStr) : null;



            if (version == null || currentPackageVersion > version)
            {
                await _channel.SendMessageAsync($"*[{BuildType} {Configuration}]* `{project.Name}` : Current package on nuget: {version}. New Package version : {currentPackageVersion}");
                ChangeLogRelease? changeLogRelease = null;
                if (Configuration == "Release")
                {
                    changeLogRelease = changeLog!.Versions.FirstOrDefault(v => v.Version == currentPackageVersion);
                }
                else
                {
                    changeLogRelease = changeLog!.Versions.FirstOrDefault(v => v.Version == null);

                }

                if (changeLogRelease == null)
                {
                    await _channel.SendMessageAsync($"*[{BuildType} {Configuration}]* Publish skipped for `{project.Name}`. No release in changelog for `{currentPackageVersion}`.");

                }
                else
                {
                    File.Delete(packagePath);

                    DotNetPack(s => s
                    .SetProject(project)
                    .SetConfiguration(Configuration)
                    .SetProperty("PackageReleaseNotes", MsBuildEscape(changeLogRelease.Description))
                    .EnableNoBuild()
                    );
                    try
                    {
                        DotNetNuGetPush(s => s
                            .SetApiKey(NugetSecretKey)
                            .SetTargetPath(packagePath)
                            .SetSource(ReleaseNugetSource)
                            .EnableNoSymbols()
                            );

                        var sourceBranch = Environment.GetEnvironmentVariable("BUILD_SOURCEBRANCH")?.Substring("refs/heads/".Length);
                        //git($"checkout {sourceBranch}");

                        //try
                        //{
                        //    git($"tag -d {Path.GetFileNameWithoutExtension(packagePath)}");
                        //}
                        //catch (Exception)
                        //{ }

                        git($"tag {Path.GetFileNameWithoutExtension(packagePath)}");

                        if (sourceBranch == null)
                        {
                            git($"push origin --tags");
                        }
                        else
                        {
                            git($"push origin HEAD:{sourceBranch} --tags");
                        }

                        await _channel.SendMessageAsync($"*[{BuildType} {Configuration}]* Published https://www.nuget.org/packages/{project.Name}/{currentPackageVersion}");
                    }
                    catch( Exception)
                    {
                        await _channel.SendMessageAsync($"*[{BuildType} {Configuration}]* An error occurred while pushing `{project.Name}` version `{currentPackageVersion}`.");
                    }
                }
            }
        }
        await _client.StopAsync();


    });

    private string MsBuildEscape(string value)
    {
        return value
            .Replace("%", "%25")
            .Replace("$", "%24")
            .Replace("@", "%40")
            .Replace("'", "%27")
            .Replace(";", "%3B")
            .Replace("?", "%3F")
            .Replace("*", "%2A")
            .Replace(",", "%2c");
    }
}
