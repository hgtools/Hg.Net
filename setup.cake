#load nuget:https://www.myget.org/F/cake-contrib/api/v2?package=Cake.Recipe&prerelease

Environment.SetVariableNames();

BuildParameters.SetParameters(
    context: Context,
    buildSystem: BuildSystem,
    sourceDirectoryPath: "./src/",
    title: "Hg.Net",
    repositoryOwner: "vCipher",
    repositoryName: "Hg.Net",
    appVeyorAccountName: "vCipher",
    shouldRunCodecov: false,
    shouldRunDupFinder: false,
    shouldRunInspectCode: false,
    solutionFilePath: "./src/Mercurial.Net.sln");


Information("{0}", Encoding.Default.WebName);
Information("{0}", Console.OutputEncoding.WebName);
Information("{0}", Console.InputEncoding.WebName);
Information("{0}", System.Environment.GetEnvironmentVariable("HGENCODING"));
BuildParameters.PrintParameters(Context);

ToolSettings.SetToolSettings(
    context: Context,
    dupFinderExcludePattern: new string[] {
        BuildParameters.RootDirectoryPath + "/src/*Tests/**/*.cs",
        BuildParameters.RootDirectoryPath + "/src/**/*.AssemblyInfo.cs"
    });

Build.Run();