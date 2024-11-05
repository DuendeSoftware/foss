using Logicality.GitHub.Actions.Workflow;

Component[] components = [
    new("ignore-this",
        ["IgnoreThis"],
        ["IgnoreThis.Tests"]),

    new("access-token-management",
        ["AccessTokenManagement", "AccessTokenManagement.OpenIdConnect"],
        ["AccessTokenManagement.Tests"]),

    new("identity-model", 
        ["IdentityModel"],
        ["IdentityModel.Tests"]),

    new("identity-model-oidc-client",
        ["IdentityModel.OidcClient", "IdentityModel.OidcClient.DPoP", "IdentityModel.OidcClient.IdentityTokenValidator"],
        ["IdentityModel.OidcClient.Tests", "IdentityModel.OidcClient.DPoP.Tests", "IdentityModel.OidcClient.IdentityTokenValidator.Tests"])
];

foreach (var component in components)
{
    GenerateCiWorkflow(component);
    GenerateReleaseWorkflow(component);
}

void GenerateCiWorkflow(Component component)
{
    var workflow = new Workflow($"{component.Name}/ci");
    var paths    = new[] { $".github/workflows/{component.Name}-**", $"src/{component.Name}/**" };

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

    job.Step()
        .ActionsCheckout();

    job.StepSetupDotNet();

    foreach (var testProject in component.Tests)
    {
        job.StepTestAndReport(component.Name, testProject);
    }

    job.StepInstallCACerts();

    job.StepToolRestore();

    foreach (var project in component.Projects)
    {
        job.StepPack(project);
    }

    job.StepSign();

    job.StepPush("MyGet", "https://www.myget.org/F/duende_identityserver/api/v2/package", "MYGET");

    job.StepPush("GitHub", "https://nuget.pkg.github.com/DuendeSoftware/index.json", "GITHUB_TOKEN")
        .Env(("GITHUB_TOKEN", "${{ secrets.GITHUB_TOKEN }}"),
            ("NUGET_AUTH_TOKEN", "${{ secrets.GITHUB_TOKEN }}"));

    job.StepUploadArtifacts(component.Name);

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

    var tagJob = workflow.Job("tag")
        .Name("Tag and Pack")
        .RunsOn(GitHubHostedRunners.UbuntuLatest)
        .Permissions(contents: Permission.Write, packages: Permission.Write)
        .Defaults().Run("pwsh", component.Name)
        .Job;

    tagJob.Step()
        .ActionsCheckout();

    tagJob.StepSetupDotNet();

    tagJob.Step()
        .Name("Git tag")
        .Run("""
             git config --global user.email "github-bot@duendesoftware.com"
             git config --global user.name "Duende Software GitHub Bot"
             git tag -a it-${{ github.event.inputs.version }} -m "Release v${{ github.event.inputs.version }}"
             git push origin it-${{ github.event.inputs.version }}
             """);

    tagJob.StepInstallCACerts();

    foreach (var project in component.Projects)
    {
        tagJob.StepPack(project);
    }

    tagJob.StepSign();

    tagJob.StepPush("MyGet", "https://www.myget.org/F/duende_identityserver/api/v2/package", "MYGET");

    tagJob.StepPush("GitHub", "https://nuget.pkg.github.com/DuendeSoftware/index.json", "GITHUB_TOKEN")
        .Env(("GITHUB_TOKEN", "${{ secrets.GITHUB_TOKEN }}"),
            ("NUGET_AUTH_TOKEN", "${{ secrets.GITHUB_TOKEN }}"));

    tagJob.StepUploadArtifacts(component.Name);

    var publishJob = workflow.Job("publish")
        .Name("Publish to nuget.org")
        .RunsOn(GitHubHostedRunners.UbuntuLatest)
        .Environment("nuget.org", "");

    publishJob.Step()
        .Uses("actions/download-artifact@v4")
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

record Component(string Name, string[] Projects, string[] Tests);

public static class StepExtensions
{
    public static void EnvDefaults(this Workflow workflow)
        => workflow.Env(
            ("DOTNETT_NOLOGO", "true"),
            ("DOTNET_CLI_TELEMETRY_OPTOUT", "true"));

    public static void StepSetupDotNet(this Job job)
    => job.Step()
        .Name("Setup .NET")
        .ActionsSetupDotNet("8.0.x");

    public static Step IfRefMain(this Step step) 
        => step.If("github.ref == 'refs/heads/main'");

    public static void StepTestAndReport(this Job job, string componentName, string testProject)
    {
        var path        = $"test/{testProject}";
        var logFileName = "Tests.trx";
        var flags = $"--logger \"console;verbosity=normal\" "      +
                    $"--logger \"trx;LogFileName={logFileName}\" " +
                    $"--collect:\"XPlat Code Coverage\"";
        job.Step()
            .Name($"Test - {testProject}")
            .Run($"dotnet test -c Release {path} {flags}");

        job.Step()
            .Name($"Test report - {testProject}")
            .Uses("dorny/test-reporter@v1")
            .If("success() || failure()")
            .With(
                ("name", $"Test Report - {testProject}"),
                ("path", $"{componentName}/{path}/TestResults/{logFileName}"),
                ("reporter", "dotnet-trx"),
                ("fail-on-error", "true"),
                ("fail-on-empty", "true"));
    }

    // These intermediate certificates are required for signing and are not installed on the GitHub runners by default.
    public static void StepInstallCACerts(this Job job)
        => job.Step()
            .Name("Install Sectigo CodeSiging CA certificates") 
            .WorkingDirectory(".github/workflows")
            //.IfRefMain()
            .Run("""
                 sudo apt-get update
                 sudo apt-get install -y ca-certificates
                 sudo cp SectigoPublicCodeSigningRootCrossAAA.crt /usr/local/share/ca-certificates/
                 sudo update-ca-certificates
                 """);

    public static void StepToolRestore(this Job job)
        => job.Step()
            .Name("Tool restore")
            //.IfRefMain()
            .Run("dotnet tool restore");

    public static void StepPack(this Job job, string project)
    {
        var path = $"src/{project}";
        job.Step()
            .Name($"Pack {project}")
            //.IfRefMain()
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
            //.IfRefMain()
            .Run($"""
                 for file in artifacts/*.nupkg; do
                    dotnet NuGetKeyVaultSignTool sign "$file" {flags}
                 done
                 """);
    }

    public static Step StepPush(this Job job, string destination, string sourceUrl, string secretName)
    {
        var apiKey = $"${{ secrets.{secretName} }}";
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
            .Uses("actions/upload-artifact@v4")
            .With(
                ("name", "artifacts"),
                ("path", path),
                ("overwrite", "true"),
                ("retention-days", "15"));
    }
}
