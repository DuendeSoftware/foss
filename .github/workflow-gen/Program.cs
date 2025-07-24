// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Logicality.GitHub.Actions.Workflow;
using static GitHubContexts;

var contexts = Instance;
Component[] components = [
    new("ignore-this",
        ["IgnoreThis"],
        ["IgnoreThis.Tests"],
        "it"),

    new("access-token-management",
        ["AccessTokenManagement", "AccessTokenManagement.OpenIdConnect"],
        ["AccessTokenManagement.Tests"],
        "atm"),

    new("identity-model", 
        ["IdentityModel"],
        ["IdentityModel.Tests"],
        "im"),

    new("identity-model-oidc-client",
        ["IdentityModel.OidcClient", "IdentityModel.OidcClient.Extensions"],
        ["IdentityModel.OidcClient.Tests"],
        "imoc")
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
    var paths    = new[]
    {
        $".github/workflows/{component.Name}-**", 
        $"{component.Name}/**",
        "Directory.Packages.props"
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
        .Name("Build")
        .RunsOn(GitHubHostedRunners.UbuntuLatest)
        .Defaults().Run("bash", component.Name)
        .Job;

    job.Permissions(
        actions: Permission.Read,
        contents: Permission.Read,
        checks: Permission.Write, 
        packages: Permission.Write);

    job.TimeoutMinutes(15);

    job.Step()
        .ActionsCheckout();

    job.StepSetupDotNet();

    foreach (var testProject in component.Tests)
    {
        job.StepTest(component.Name, testProject);
    }

    job.StepUploadTestResultsAsArtifact(component);

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
            new StringInput("version", "Version in format X.Y.Z or X.Y.Z-preview.", true, "0.0.0"),
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
        .Name("Checkout target branch")
        .If("github.event.inputs.branch != 'main'")
        .Run("git checkout ${{ github.event.inputs.branch }}");

    tagJob.StepSetupDotNet();


    tagJob.Step()
        .Name("Git Config")
        .Run(@"git config --global user.email ""github-bot@duendesoftware.com""
git config --global user.name ""Duende Software GitHub Bot""");

    tagJob.Step()
        .Name("Remove previous git tag")
        .If("github.event.inputs['remove-tag-if-exists'] == 'true'")
        .Run($@"if git rev-parse {component.TagPrefix}-{contexts.Event.Input.Version} >/dev/null 2>&1; then
  git tag -d {component.TagPrefix}-{contexts.Event.Input.Version}
  git push --delete origin {component.TagPrefix}-{contexts.Event.Input.Version}
else
  echo 'Tag {component.TagPrefix}-{contexts.Event.Input.Version} does not exist.'
fi");


    tagJob.Step()
        .Name("Git tag")
        .Run($@"git tag -a {component.TagPrefix}-{contexts.Event.Input.Version} -m ""Release v{contexts.Event.Input.Version}""
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

    tagJob.StepPush("MyGet", "https://www.myget.org/F/duende_identityserver/api/v2/package", "MYGET", pushAlways: true);

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
            job.StepGenerateReportFromTestArtifact(component, testProject);
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

record Component(string Name, string[] Projects, string[] Tests, string TagPrefix)
{
    public string CiWorkflowName => $"{Name}/ci";
    public string ReleaseWorkflowName => $"{Name}/release";
}

public static class StepExtensions
{
    public static void EnvDefaults(this Workflow workflow)
        => workflow.Env(
            ("DOTNET_NOLOGO", "true"),
            ("DOTNET_CLI_TELEMETRY_OPTOUT", "true"));

    public static void StepSetupDotNet(this Job job)
        => job.Step()
            .Name("Setup .NET")
            .ActionsSetupDotNet("3e891b0cb619bf60e2c25674b222b8940e2c1c25", ["6.0.x", "8.0.x", "9.0.x"]); // v4.1.0

    public static Step IfRefMain(this Step step) 
        => step.If("github.ref == 'refs/heads/main'");

    public static void StepTest(this Job job, string componentName, string testProject)
    {
        var path        = $"test/{testProject}";
        var logFileName = $"{testProject}.trx";
        var flags = $"--logger \"console;verbosity=normal\" "      +
                    $"--logger \"trx;LogFileName={logFileName}\" " +
                    $"--collect:\"XPlat Code Coverage\"";
        job.Step()
            .Name($"Test - {testProject}")
            .Run($"dotnet test -c Release {path} {flags}");

    }

    internal static void StepUploadTestResultsAsArtifact(this Job job, Component component)
    {
        job.Step()
            .Name($"Test report")
            .If("success() || failure()")
            .Uses("actions/upload-artifact@b4b15b8c7c6ac21ea08fcf65892d2ee8f75cf882") // 4.4.3
            .With(
                ("name", "test-results"),
                ("path", string.Join(Environment.NewLine, component.Tests
                    .Select(testProject => $"{component.Name}/test/{testProject}/TestResults/{testProject}.trx"))),
                ("retention-days", "5"));
    }

    internal static void StepGenerateReportFromTestArtifact(this Job job, Component component, string testProject)
    {
        var path = $"test/{testProject}";
        job.Step()
            .Name($"Test report - {component.Name} - {testProject}")
            .Uses("dorny/test-reporter@31a54ee7ebcacc03a09ea97a7e5465a47b84aea5") // v1.9.1
            .If($"github.event.workflow.name == '{component.CiWorkflowName}'")
            .With(
                ("artifact", "test-results"),
                ("name", $"Test Report - {testProject}"),
                ("path", $"{testProject}.trx"),
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
        var flags = "--file-digest sha256 "                                                +
                    "--timestamp-rfc3161 http://timestamp.digicert.com "                   +
                    "--azure-key-vault-url https://duendecodesigninghsm.vault.azure.net/ " +
                    "--azure-key-vault-client-id 18e3de68-2556-4345-8076-a46fad79e474 "    +
                    "--azure-key-vault-tenant-id ed3089f0-5401-4758-90eb-066124e2d907 "    +
                    "--azure-key-vault-client-secret ${{ secrets.SignClientSecret }} "     +
                    "--azure-key-vault-certificate NuGetPackageSigning";
        var step = job.Step()
            .Name("Sign packages");
        if (!always)
        {
            step = step.IfGithubEventIsPush();
        }
        step.Run($"""
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
    public static  GitHubContexts Instance { get; } = new();
    public virtual GitHubContext  GitHub   { get; } = new();
    public virtual SecretsContext Secrets  { get; } = new();
    public virtual EventContext   Event    { get; } = new();

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
        public EventsInputContext Input { get; } = new ();
    }

    public class EventsInputContext() : Context("github.event.inputs")
    {
        public string Version => Expression($"{Name}.version");
    }
}