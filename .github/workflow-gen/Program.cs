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
        ["ubuntu-latest", "windows-latest"]),

    new("access-token-management",
        ["AccessTokenManagement", "AccessTokenManagement.OpenIdConnect"],
        ["AccessTokenManagement.Tests"],
        "atm",
        ["ubuntu-latest"]),

    new("identity-model", 
        ["IdentityModel"],
        ["IdentityModel.Tests"],
        "im",
        ["ubuntu-latest", "windows-latest"]),

    new("identity-model-oidc-client",
        ["IdentityModel.OidcClient", "IdentityModel.OidcClient.Extensions"],
        ["IdentityModel.OidcClient.Tests"],
        "imoc",
        ["ubuntu-latest"])
];

foreach (var component in components)
{
    GenerateCiWorkflow(component);
    GenerateReleaseWorkflow(component);
}

void GenerateCiWorkflow(Component component)
{
    var workflow = new Workflow($"{component.Name}/ci");
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
        .PullRequestTarget()
        .Paths(paths);

    workflow.EnvDefaults();

    var buildJob = workflow
        .Job("build")
        .Name("Build")
        .Strategy()
            .Matrix(("os", component.RunsOn))
            .FailFast(false)
            .Job
        .RunsOn("${{ matrix.os }}")
        .Defaults()
            .Run("bash", component.Name)
            .Job;

    buildJob.Permissions(checks: Permission.Write, contents: Permission.Read);

    buildJob.TimeoutMinutes(15);

    buildJob.Step()
        .ActionsCheckout();

    buildJob.StepSetupDotNet();

    foreach (var testProject in component.Tests)
    {
        buildJob.StepTestAndReport(component.Name, testProject, "net8.0");
        buildJob.StepTestAndReport(component.Name, testProject, "net9.0");
        if (component.RunsOn.Contains("windows-latest"))
        {
            buildJob.StepTestAndReport(component.Name, testProject, "net481");
        }
    }

    var packJob = workflow.Job("pack")
        .Name("Pack")
        .RunsOn(GitHubHostedRunners.UbuntuLatest)
        .Needs("build")
        .Defaults()
            .Run("bash", component.Name)
            .Job;

    packJob.Permissions(
        actions: Permission.Read,
        contents: Permission.Read,
        checks: Permission.Write,
        packages: Permission.Write);

    packJob.TimeoutMinutes(15);

    packJob.Step()
        .ActionsCheckout();

    packJob.StepSetupDotNet();

    packJob.StepInstallCACerts();

    packJob.StepToolRestore();

    foreach (var project in component.Projects)
    {
        packJob.StepPack(project);
    }

    packJob.StepSign();

    packJob.StepPush("MyGet", "https://www.myget.org/F/duende_identityserver/api/v2/package", "MYGET");

    packJob.StepPush("GitHub", "https://nuget.pkg.github.com/DuendeSoftware/index.json", "GITHUB_TOKEN")
        .Env(("GITHUB_TOKEN", contexts.Secrets.GitHubToken),
            ("NUGET_AUTH_TOKEN", contexts.Secrets.GitHubToken));

    packJob.StepUploadArtifacts(component.Name);

    var fileName = $"{component.Name}-ci";
    WriteWorkflow(workflow, fileName);
}

void GenerateReleaseWorkflow(Component component)
{
    var workflow = new Workflow($"{component.Name}/release");

    workflow.On
        .WorkflowDispatch()
        .Inputs(new StringInput("version", "Version in format X.Y.Z or X.Y.Z-preview.", true, "0.0.0"));

    workflow.EnvDefaults();

    var tagJob = workflow
        .Job("tag")
        .Name("Tag and Pack")
        .RunsOn(GitHubHostedRunners.UbuntuLatest)
        .Permissions(contents: Permission.Write, packages: Permission.Write)
        .Defaults().Run("bash", component.Name).Job;

    tagJob.Step()
        .ActionsCheckout("11bd71901bbe5b1630ceea73d27597364c9af683");

    tagJob.StepSetupDotNet();

    tagJob.Step()
        .Name("Git tag")
        .Run($@"git config --global user.email ""github-bot@duendesoftware.com""
git config --global user.name ""Duende Software GitHub Bot""
git tag -a {component.TagPrefix}-{contexts.Event.Input.Version} -m ""Release v{contexts.Event.Input.Version}""
git push origin {component.TagPrefix}-{contexts.Event.Input.Version}");

    tagJob.StepInstallCACerts();

    foreach (var project in component.Projects)
    {
        tagJob.StepPack(project);
    }

    tagJob.StepToolRestore();

    tagJob.StepSign();

    tagJob.StepPush("MyGet", "https://www.myget.org/F/duende_identityserver/api/v2/package", "MYGET");

    tagJob.StepPush("GitHub", "https://nuget.pkg.github.com/DuendeSoftware/index.json", "GITHUB_TOKEN")
        .Env(("GITHUB_TOKEN", contexts.Secrets.GitHubToken),
            ("NUGET_AUTH_TOKEN", contexts.Secrets.GitHubToken));

    tagJob.StepUploadArtifacts(component.Name);

    var publishJob = workflow.Job("publish")
        .Name("Publish to nuget.org")
        .RunsOn(GitHubHostedRunners.UbuntuLatest)
        .Needs("tag")
        .Environment("nuget.org", "");

    publishJob.Step()
        .Uses("actions/download-artifact@fa0a91b85d4f404e444e00e005971372dc801d16") // 4.1.8
        .With(("name", "artifacts"), ("path", "artifacts"));

    publishJob.StepSetupDotNet();

    publishJob.Step()
        .Name("List files")
        .Shell("bash")
        .Run("tree");

    publishJob.StepPush("nuget.org", "https://api.nuget.org/v3/index.json", "NUGET_ORG_API_KEY");

    var fileName = $"{component.Name}-release";
    WriteWorkflow(workflow, fileName);
}

void WriteWorkflow(Workflow workflow, string fileName)
{
    var filePath = $"../workflows/{fileName}.yml";
    workflow.WriteYaml(filePath);
    Console.WriteLine($"Wrote workflow to {filePath}");
}

record Component(string Name, string[] Projects, string[] Tests, string TagPrefix, string[] RunsOn);

public static class StepExtensions
{
    public static void EnvDefaults(this Workflow workflow)
        => workflow.Env(
            ("DOTNETT_NOLOGO", "true"),
            ("DOTNET_CLI_TELEMETRY_OPTOUT", "true"));

    public static void StepSetupDotNet(this Job job)
        => job.Step()
            .Name("Setup .NET")
            .ActionsSetupDotNet("3e891b0cb619bf60e2c25674b222b8940e2c1c25", ["6.0.x", "8.0.x", "9.0.x"]); // v4.1.0

    public static Step IfRefMain(this Step step) 
        => step.If("github.ref == 'refs/heads/main'");

    public static Step IfWindows(this Step step)
        => step.If("matrix.os == 'windows-latest'");

    public static void StepTestAndReport(this Job job, string componentName, string testProject, string framework)
    {
        var path        = $"test/{testProject}";
        var logFileName = $"tests-{framework}.trx";
        var flags = $"--configuration Release "                    +
                    $"--framework {framework} "                    +
                    $"--logger \"console;verbosity=normal\" "      +
                    $"--logger \"trx;LogFileName={logFileName}\" " +
                    $"--collect:\"XPlat Code Coverage\"";
        var isWindows = framework == "net481";

        var testStep = job.Step()
            .Name($"Test - {testProject}-{framework}")
            .Run($"dotnet test {path} {flags}");

        var testReportStep = job.Step()
            .Name($"Test report {testProject}-{framework}")
            .Uses("dorny/test-reporter@31a54ee7ebcacc03a09ea97a7e5465a47b84aea5") // v1.9.1
            .If("success() || failure()")
            .With(
                ("name", $"Test Report {testProject}-{framework}"),
                ("path", $"{componentName}/{path}/TestResults/{logFileName}"),
                ("reporter", "dotnet-trx"),
                ("fail-on-error", "true"),
                ("fail-on-empty", "true"));

        if (isWindows)
        {
            testStep.IfWindows();
            testReportStep.IfWindows();
        }
    }

    // These intermediate certificates are required for signing and are not installed on the GitHub runners by default.
    public static void StepInstallCACerts(this Job job)
        => job.Step()
            .Name("Install Sectigo CodeSiging CA certificates") 
            .WorkingDirectory(".github/workflows")
            .Run("""
                 sudo apt-get update
                 sudo apt-get install -y ca-certificates
                 sudo cp SectigoPublicCodeSigningRootCrossAAA.crt /usr/local/share/ca-certificates/
                 sudo update-ca-certificates
                 """);

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

    public static void StepSign(this Job job)
    {
        var flags = "--file-digest sha256 "                                             +
                    "--timestamp-rfc3161 http://timestamp.digicert.com "                +
                    "--azure-key-vault-url https://duendecodesigning.vault.azure.net/ " +
                    "--azure-key-vault-client-id 18e3de68-2556-4345-8076-a46fad79e474 " +
                    "--azure-key-vault-tenant-id ed3089f0-5401-4758-90eb-066124e2d907 " +
                    "--azure-key-vault-client-secret ${{ secrets.SignClientSecret }} "  +
                    "--azure-key-vault-certificate CodeSigning";
        job.Step()
            .Name("Sign packages")
            .Run($"""
                 for file in artifacts/*.nupkg; do
                    dotnet NuGetKeyVaultSignTool sign "$file" {flags}
                 done
                 """);
    }

    public static Step StepPush(this Job job, string destination, string sourceUrl, string secretName)
    {
        var apiKey = $"${{{{ secrets.{secretName} }}}}";
        return job.Step()
            .Name($"Push packages to {destination}")
            .IfRefMain()
            .Run($"dotnet nuget push artifacts/*.nupkg --source {sourceUrl} --api-key {apiKey} --skip-duplicate");
    }

    public static void StepUploadArtifacts(this Job job, string componentName)
    {
        var path = $"{componentName}/artifacts/*.nupkg";
        job.Step()
            .Name("Upload Artifacts")
            .IfRefMain()
            .Uses("actions/upload-artifact@b4b15b8c7c6ac21ea08fcf65892d2ee8f75cf882") // 4.4.3
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
