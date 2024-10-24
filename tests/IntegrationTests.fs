module Tests.IntegrationTests

open System.IO
open System.Runtime.CompilerServices
open System.Threading.Tasks
open Microsoft.VisualStudio.TestTools.UnitTesting
open BlackFox.CommandLine
open Faqt
open SimpleExec
open Workspace

module Utils =
    let packAndGetPackageProperties projectName =
        let packageCache = VirtualWorkspace.``test-package-cache``.``.``

        if Directory.Exists packageCache then
            Directory.Delete(packageCache, true)

        Directory.CreateDirectory packageCache |> ignore

        Command.Run(
            "dotnet",
            CmdLine.empty
            |> CmdLine.appendPrefix "restore" projectName
            |> CmdLine.appendPrefix "--packages" VirtualWorkspace.``test-package-cache``.``.``
            |> CmdLine.toString,
            workingDirectory = Workspace.fixtures.``.``
        )

        Command.ReadAsync(
            "dotnet",
            CmdLine.empty
            |> CmdLine.appendPrefix "pack" projectName
            |> CmdLine.appendPrefix "-c" "Release"
            |> CmdLine.append "--no-restore"
            |> CmdLine.appendRaw "--getProperty:Version"
            |> CmdLine.appendRaw "--getProperty:PackageVersion"
            |> CmdLine.appendRaw "--getProperty:PackageReleaseNotes"
            |> CmdLine.toString,
            workingDirectory = Workspace.fixtures.``.``
        )

type StringHelper =
    [<Extension>]
    static member ReplaceEscapedNewLines(s: string) =
        s.ReplaceLineEndings().Replace("\\r\\n", "\\n")

[<TestClass>]
type IntegrationTests() =

    member val testPackageVersion = null with get, set

    member this.AddPackageReference(projectName: string) =
        let suffix = projectName.Replace(".fsproj", "")

        this.testPackageVersion <- $"0.0.1-test-{suffix}"

        // Create a package to be used in the tests
        // I didn't find a way to test the MSBuild tasks execution using MSBuild only
        // So each fsproj, will use a package reference to the package created here
        Command.Run(
            "dotnet",
            CmdLine.empty
            |> CmdLine.appendPrefix "pack" "src"
            |> CmdLine.appendPrefix "-c" "Release"
            |> CmdLine.appendPrefix "-o" VirtualWorkspace.``test-nupkgs``.``.``
            |> CmdLine.appendRaw $"-p:PackageVersion=%s{this.testPackageVersion}"
            |> CmdLine.toString,
            workingDirectory = Workspace.``..``.``.``
        )

        Command.Run(
            "dotnet",
            CmdLine.empty
            |> CmdLine.appendPrefix "add" projectName
            |> CmdLine.appendPrefix "package" "Ionide.KeepAChangelog.Tasks"
            // |> CmdLine.appendPrefix "--source" VirtualWorkspace.``test-nupkgs``.``.``
            |> CmdLine.appendPrefix "--version" $"[{this.testPackageVersion}]"
            |> CmdLine.toString,
            workingDirectory = Workspace.fixtures.``.``
        )

    [<TestMethod>]
    member this.``works for absolute path with keep a changelog``() : Task =
        task {
            let projectName = "WorksForAbsolutePathWithKeepAChangelog.fsproj"

            this.AddPackageReference projectName

            let! struct (stdout, _) = Utils.packAndGetPackageProperties projectName

            stdout
                .ReplaceEscapedNewLines()
                .Should()
                .Be(
                    """{
  "Properties": {
    "Version": "0.1.0",
    "PackageVersion": "0.1.0",
    "PackageReleaseNotes": "### Added\n\n- Created the package\n- Added a second line\n\n### Changed\n\n- Updated the package"
  }
}
"""
                )
            |> ignore
        }

    [<TestMethod>]
    member this.``works for relative path with keep a changelog``() : Task =
        task {
            let projectName = "WorksForRelativePathWithKeepAChangelog.fsproj"

            this.AddPackageReference projectName

            let! struct (stdout, _) = Utils.packAndGetPackageProperties projectName

            stdout
                .ReplaceEscapedNewLines()
                .Should()
                .Be(
                    """{
  "Properties": {
    "Version": "0.1.0",
    "PackageVersion": "0.1.0",
    "PackageReleaseNotes": "### Added\n\n- Created the package\n- Added a second line\n\n### Changed\n\n- Updated the package"
  }
}
"""
                )
            |> ignore
        }

    [<TestMethod>]
    member this.``fails if no changelog is specified``() : Task =
        task {
            let projectName = "FailIfChangelogNotSpecified.fsproj"

            this.AddPackageReference projectName

            let! struct (stdout, _) = Utils.packAndGetPackageProperties projectName

            stdout
                .ReplaceEscapedNewLines()
                .Should()
                .Be(
                    """{
  "Properties": {
    "Version": "1.0.0",
    "PackageVersion": "1.0.0",
    "PackageReleaseNotes": ""
  }
}
"""
                )
            |> ignore
        }

    [<TestMethod>]
    member this.``fails if changelog specified doesn't exist``() : Task =
        task {
            let projectName = "FailIfChangelogDoesNotExist.fsproj"

            this.AddPackageReference projectName

            let! struct (stdout, _) = Utils.packAndGetPackageProperties projectName

            stdout
                .ReplaceEscapedNewLines()
                .Should()
                .Be(
                    """{
  "Properties": {
    "Version": "1.0.0",
    "PackageVersion": "1.0.0",
    "PackageReleaseNotes": ""
  }
}
"""
                )
            |> ignore
        }
