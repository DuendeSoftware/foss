// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Logicality.GitHub.Actions.Workflow;
using static GitHubContexts;

var contexts = Instance;
Component[] components = [
    new("ignore-this",
        ["IgnoreThis"],
        ["IgnoreThis.Tests"],
        "it",
        [GitHubHostedRunners.UbuntuLatest],
        ["net8.0", "net10.0"]),

    new("access-token-management",
        ["AccessTokenManagement", "AccessTokenManagement.OpenIdConnect"],
        ["AccessTokenManagement.Tests"],
        "atm",
        [GitHubHostedRunners.UbuntuLatest],
        ["net8.0", "net9.0", "net10.0"]),

    new("identity-model",
        ["IdentityModel"],
        ["IdentityModel.Tests"],
        "im",
        [GitHubHostedRunners.UbuntuLatest, GitHubHostedRunners.WindowsLatest],
        ["net8.0", "net9.0", "net10.0"]),

    new("identity-model-oidc-client",
        ["IdentityModel.OidcClient", "IdentityModel.OidcClient.Extensions"],
        ["IdentityModel.OidcClient.Tests"],
        "imoc",
        [GitHubHostedRunners.UbuntuLatest],
        ["net8.0", "net9.0", "net10.0"]),

    new("introspection",
        ["AspNetCore.Authentication.OAuth2Introspection"],
        ["AspNetCore.Authentication.OAuth2Introspection.Tests"],
        "intro",
        [GitHubHostedRunners.UbuntuLatest],
        ["net8.0", "net9.0", "net10.0"]),
];

foreach (var component in components)
{
    GenerateCiWorkflow(component);
    GenerateReleaseWorkflow(component);
}

GenerateUploadTestResultsWorkflow();


void GenerateCiWorkflow(Component component)
{
    var workflow = new Workflow(component.CiWorkflowName);
    var paths = new[]
    {
        $".github/workflows/{component.Name}-**",
        $"{component.Name}/**",
        ".editorconfig",
        "Directory.Packages.props",
        "global.json",
        "src.props",
        "test.props"
    };

    workflow.On
        .WorkflowDispatch();
    workflow.On
        .Push()
        .Paths(paths);
    workflow.On
        .PullRequest()
        .Paths(paths);

    workflow.EnvDefaults();

    var job = workflow
        .Job("build")
        .Name("Build");

    if (component.RunsOn.Length == 1)
    {
        job.RunsOn(component.RunsOn[0]);
    }
    else
    {
        job.Strategy()
            .Matrix(("os", component.RunsOn))
            .FailFast(false)
            .Job
            .RunsOn("${{ matrix.os }}");
    }

    job = job
        .Defaults().Run("bash", component.Name)
        .Job;

    job.Permissions(
        actions: Permission.Read,
        contents: Permission.Read,
        checks: Permission.Write,
        packages: Permission.Write);

    job.TimeoutMinutes(15);

    job.Step()
        .ActionsCheckout("11bd71901bbe5b1630ceea73d27597364c9af683"); // Pinned to 4.2.2

    job.StepSetupDotNet();

    job.StepRestore();

    job.StepVerifyFormatting();

    var runsOnIncludesWindows = component.RunsOn.Contains(GitHubHostedRunners.WindowsLatest);
    foreach (var testProject in component.Tests)
    {
        job.StepTest(component, testProject, runsOnIncludesWindows);

        // Temporarily disabled
        // if (runsOnIncludesWindows)
        // {
        //     job.StepTest(component.Name, testProject, false, "net481");
        // }
    }

    job.StepUploadTestResultsAsArtifact(component, runsOnIncludesWindows);

    // if (runsOnIncludesWindows)
    // {
    //     job.StepUploadTestResultsAsArtifact(component, false, "net481");
    // }

    job.StepToolRestore();

    foreach (var project in component.Projects)
    {
        job.StepPack(project);
    }

    job.StepSign();

    job.StepPush("GitHub", "https://nuget.pkg.github.com/DuendeSoftware/index.json", "GITHUB_TOKEN")
        .Env(("GITHUB_TOKEN", contexts.Secrets.GitHubToken),
            ("NUGET_AUTH_TOKEN", contexts.Secrets.GitHubToken));

    job.StepUploadArtifacts(component.Name);

    var fileName = $"{component.Name}-ci";
    WriteWorkflow(workflow, fileName);
}

void GenerateReleaseWorkflow(Component component)
{
    var workflow = new Workflow(component.ReleaseWorkflowName);

    workflow.On
        .WorkflowDispatch()
        .Inputs(
            new StringInput("version", "Version in format X.Y.Z, X.Y.Z-preview.N, or X.Y.Z-rc.N", true, "0.0.0"),
            new StringInput("branch", "(Optional) the name of the branch to release from", false, "main"),
            new BooleanInput("remove-tag-if-exists", "If set, will remove the existing tag. Use this if you have issues with the previous release action", false, false));

    workflow.EnvDefaults();

    var tagJob = workflow
        .Job("tag")
        .Name("Tag and Pack")
        .RunsOn(GitHubHostedRunners.UbuntuLatest)
        .Permissions(contents: Permission.Write, packages: Permission.Write)
        .Defaults().Run("bash", component.Name).Job;

    tagJob.Step()
        .ActionsCheckout();

    tagJob.Step()
        .Name("Validate Version Input")
        .Run($@"echo '{contexts.Event.Input.Version}' | grep -P '^\d+\.\d+\.\d+(-preview\.\d+|-rc\.\d+)?$' || (echo 'Invalid version format' && exit 1)");

    tagJob.Step()
        .Name("Checkout target branch")
        .If("github.event.inputs.branch != 'main'")
        .Run("git checkout ${{ github.event.inputs.branch }}");

    tagJob.StepSetupDotNet();


    tagJob.Step()
        .Name("Git Config")
        .RunScript(@"git config --global user.email ""github-bot@duendesoftware.com""
git config --global user.name ""Duende Software GitHub Bot""");

    tagJob.Step()
        .Name("Remove previous git tag")
        .If("github.event.inputs['remove-tag-if-exists'] == 'true'")
        .RunScript($@"if git rev-parse {component.TagPrefix}-{contexts.Event.Input.Version} >/dev/null 2>&1; then
  git tag -d {component.TagPrefix}-{contexts.Event.Input.Version}
  git push --delete origin {component.TagPrefix}-{contexts.Event.Input.Version}
else
  echo 'Tag {component.TagPrefix}-{contexts.Event.Input.Version} does not exist.'
fi");


    tagJob.Step()
        .Name("Git tag")
        .RunScript($@"git tag -a {component.TagPrefix}-{contexts.Event.Input.Version} -m ""Release v{contexts.Event.Input.Version}""
git push origin {component.TagPrefix}-{contexts.Event.Input.Version}");

    foreach (var project in component.Projects)
    {
        tagJob.StepPack(project);
    }

    tagJob.StepToolRestore();

    tagJob.StepSign(always: true);

    tagJob.StepPush("GitHub", "https://nuget.pkg.github.com/DuendeSoftware/index.json", "GITHUB_TOKEN", pushAlways: true)
        .Env(("GITHUB_TOKEN", contexts.Secrets.GitHubToken),
            ("NUGET_AUTH_TOKEN", contexts.Secrets.GitHubToken));

    tagJob.StepUploadArtifacts(component.Name, uploadAlways: true);

    var publishJob = workflow.Job("publish")
        .Name("Publish to nuget.org")
        .RunsOn(GitHubHostedRunners.UbuntuLatest)
        .Needs("tag")
        .Environment("nuget.org", "");
    ;

    publishJob.Step()
        .Uses("actions/download-artifact@fa0a91b85d4f404e444e00e005971372dc801d16") // 4.1.8
        .With(("name", "artifacts"), ("path", "artifacts"));

    publishJob.StepSetupDotNet();

    publishJob.Step()
        .Name("List files")
        .Shell("bash")
        .Run("tree");

    publishJob.StepPush("nuget.org", "https://api.nuget.org/v3/index.json", "NUGET_ORG_API_KEY", pushAlways: true);

    var fileName = $"{component.Name}-release";
    WriteWorkflow(workflow, fileName);
}

void GenerateUploadTestResultsWorkflow()
{
    var workflow = new Workflow("generate-test-reports");
    workflow.On
        .WorkflowRun()
        .Workflows(components.Select(x => x.CiWorkflowName).ToArray())
        .Types("completed");

    var job = workflow
        .Job("report")
        .Name("report")
        .RunsOn(GitHubHostedRunners.UbuntuLatest);

    job.Permissions(
        actions: Permission.Read,
        contents: Permission.Read,
        checks: Permission.Write,
        packages: Permission.Write);

    foreach (var component in components)
    {
        foreach (var testProject in component.Tests)
        {
            foreach (var tfm in component.TargetFrameworks)
            {
                job.StepGenerateReportFromTestArtifact(component, testProject, tfm);
            }

            // Temporarily disabled
            // if (component.RunsOn.Contains(GitHubHostedRunners.WindowsLatest))
            // {
            //     job.StepGenerateReportFromTestArtifact(component, $"{testProject}-net481", "test-results-net481");
            // }
        }
    }

    var fileName = $"generate-test-reports";
    WriteWorkflow(workflow, fileName);
}

void WriteWorkflow(Workflow workflow, string fileName)
{
    var filePath = $"../workflows/{fileName}.yml";
    workflow.WriteYaml(filePath);
    Console.WriteLine($"Wrote workflow to {filePath}");
}

public record Component(string Name, string[] Projects, string[] Tests, string TagPrefix, string[] RunsOn, string[] TargetFrameworks)
{
    public string CiWorkflowName => $"{Name}/ci";
    public string ReleaseWorkflowName => $"{Name}/release";
}

public static class StepExtensions
{
    /// <summary>
    /// Normalizes line endings in run scripts so the YAML library correctly detects multi-line
    /// strings and emits literal block scalar (|-) instead of folded (>-).
    /// This ensures shell scripts work correctly across Windows and Linux runners.
    /// </summary>
    public static Step RunScript(this Step step, string script)
        => step.Run(script.ReplaceLineEndings());

    public static void EnvDefaults(this Workflow workflow)
        => workflow.Env(
            ("DOTNET_NOLOGO", "true"),
            ("DOTNET_CLI_TELEMETRY_OPTOUT", "true"));

    public static void StepSetupDotNet(this Job job)
        => job.Step()
            .Name("Setup .NET")
            .ActionsSetupDotNet("d4c94342e560b34958eacfc5d055d21461ed1c5d", ["8.0.414", "9.0.305", "10.0.100"]); // v5.0.0

    public static Step IfRefMain(this Step step)
        => step.If("github.ref == 'refs/heads/main'");

    internal static Step IfWindows(this Step step)
        => step.If("matrix.os == 'windows-latest'");

    internal static Step IfNotWindows(this Step step)
        => step.If("matrix.os != 'windows-latest'");

    public static void StepTest(this Job job, Component component, string testProject, bool excludeOnWindows)
    {
        var path = $"test/{testProject}";
        var tfmList = string.Join(" ", component.TargetFrameworks);

        // Build step
        var buildStep = job.Step()
            .Name($"Build - {testProject}")
            .Run($"dotnet build -c Release {path}");

        if (excludeOnWindows)
        {
            buildStep.IfNotWindows();
        }

        // Run tests for each TFM using MTP command line experience
        var testStep = job.Step()
            .Name($"Test - {testProject}")
            .Run($"""
                for tfm in {tfmList}; do
                  dotnet run --project {path} -c Release --no-build -f $tfm -- \
                    --report-xunit-trx --report-xunit-trx-filename {testProject}-$tfm.trx \
                    --coverage --coverage-output-format cobertura \
                    --coverage-output {testProject}-$tfm.cobertura.xml
                done
                """);

        if (excludeOnWindows)
        {
            testStep.IfNotWindows();
        }
    }

    internal static void StepUploadTestResultsAsArtifact(this Job job, Component component, bool excludeOnWindows)
    {
        // Build paths for all TFMs
        var paths = component.Tests
            .SelectMany(testProject => component.TargetFrameworks
                .Select(tfm => $"{component.Name}/test/{testProject}/TestResults/{testProject}-{tfm}.trx"));

        var uploadStep = job.Step()
            .Name("Test report")
            .If("success() || failure()")
            .Uses("actions/upload-artifact@b4b15b8c7c6ac21ea08fcf65892d2ee8f75cf882") // 4.4.3
            .With(
                ("name", "test-results"),
                ("path", string.Join(Environment.NewLine, paths)),
                ("retention-days", "5"));

        if (excludeOnWindows)
        {
            uploadStep.IfNotWindows();
        }
    }

    internal static void StepGenerateReportFromTestArtifact(this Job job, Component component, string testProject, string tfm, string artifactName = "test-results")
    {
        var testProjectWithTfm = $"{testProject}-{tfm}";
        job.Step()
            .Name($"Test report - {component.Name} - {testProjectWithTfm}")
            .Uses("dorny/test-reporter@31a54ee7ebcacc03a09ea97a7e5465a47b84aea5") // v1.9.1
            .If($"github.event.workflow.name == '{component.CiWorkflowName}'")
            .With(
                ("artifact", artifactName),
                ("name", $"Test Report - {testProjectWithTfm}"),
                ("path", $"{testProjectWithTfm}.trx"),
                ("reporter", "dotnet-trx"),
                ("fail-on-error", "true"),
                ("fail-on-empty", "true"));
    }

    public static void StepToolRestore(this Job job)
        => job.Step()
            .Name("Tool restore")
            .Run("dotnet tool restore");

    public static void StepPack(this Job job, string project)
    {
        var path = $"src/{project}";
        job.Step()
            .Name($"Pack {project}")
            .Run($"dotnet pack -c Release {path} -o artifacts");
    }

    public static void StepSign(this Job job, bool always = false)
    {
        var flags = "--file-digest sha256 " +
                    "--timestamp-rfc3161 http://timestamp.digicert.com " +
                    "--azure-key-vault-url https://duendecodesigninghsm.vault.azure.net/ " +
                    "--azure-key-vault-client-id 18e3de68-2556-4345-8076-a46fad79e474 " +
                    "--azure-key-vault-tenant-id ed3089f0-5401-4758-90eb-066124e2d907 " +
                    "--azure-key-vault-client-secret ${{ secrets.SignClientSecret }} " +
                    "--azure-key-vault-certificate NuGetPackageSigning";
        var step = job.Step()
            .Name("Sign packages");
        if (!always)
        {
            step = step.IfGithubEventIsPush();
        }
        step.RunScript($"""
              for file in artifacts/*.nupkg; do
                 dotnet NuGetKeyVaultSignTool sign "$file" {flags}
              done
              """);
    }
    /// <summary>
    /// Only run this if the build is triggered on a branch IN the same repo
    /// this means it's from a trusted contributor.
    /// </summary>
    public static Step IfGithubEventIsPush(this Step step)
        => step.If("github.event_name == 'push'");

    public static Step StepPush(this Job job, string destination, string sourceUrl, string secretName, bool pushAlways = false)
    {
        var apiKey = $"${{{{ secrets.{secretName} }}}}";
        var step = job.Step()
            .Name($"Push packages to {destination}");

        if (!pushAlways)
        {
            step.IfRefMain();
        }
        return step.Run($"dotnet nuget push artifacts/*.nupkg --source {sourceUrl} --api-key {apiKey} --skip-duplicate");
    }
    public static Step StepVerifyFormatting(this Job job)
        => job.Step()
            .Name("Verify Formatting")
            .Run("dotnet format *.slnf --verify-no-changes --no-restore");

    public static Step StepRestore(this Job job)
        => job.Step()
            .Name("Restore")
            .Run("dotnet restore *.slnf");

    public static void StepUploadArtifacts(this Job job, string componentName, bool uploadAlways = false)
    {
        var path = $"{componentName}/artifacts/*.nupkg";
        var step = job.Step()
            .Name("Upload Artifacts");

        if (!uploadAlways)
        {
            step.IfGithubEventIsPush();
        }

        step.Uses("actions/upload-artifact@b4b15b8c7c6ac21ea08fcf65892d2ee8f75cf882") // 4.4.3
            .With(
                ("name", "artifacts"),
                ("path", path),
                ("overwrite", "true"),
                ("retention-days", "15"));
    }
}

public class GitHubContexts
{
    public static GitHubContexts Instance { get; } = new();
    public virtual GitHubContext GitHub { get; } = new();
    public virtual SecretsContext Secrets { get; } = new();
    public virtual EventContext Event { get; } = new();

    public abstract class Context(string name)
    {
        protected string Name => name;

        protected string Expression(string s) => "${{ " + s + " }}";
    }

    public class GitHubContext() : Context("github")
    {
    }

    public class SecretsContext() : Context("secrets")
    {
        public string GitHubToken => Expression($"{Name}.GITHUB_TOKEN");
    }

    public class EventContext() : Context("github.event")
    {
        public EventsInputContext Input { get; } = new();
    }

    public class EventsInputContext() : Context("github.event.inputs")
    {
        public string Version => Expression($"{Name}.version");
    }
}
