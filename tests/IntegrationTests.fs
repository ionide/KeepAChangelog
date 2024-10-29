module Tests.IntegrationTests

open System.IO
open System.IO.Compression
open System.Reflection
open System.Runtime.InteropServices
open System.Threading.Tasks
open Ionide.KeepAChangelog.Tasks.Test
open Microsoft.VisualStudio.TestTools.UnitTesting
open BlackFox.CommandLine
open Faqt
open SimpleExec
open Workspace
open Helpers

module Utils =
    let packAndGetPackagePropertiesWithExtraArg projectName (extraArg: string option) =
        task {

            let packageCache = VirtualWorkspace.``test-package-cache``.``.``

            // Force restoring of the latest package by clearing the local packages directory
            if Directory.Exists packageCache then
                Directory.Delete(packageCache, true)

            Directory.CreateDirectory packageCache |> ignore

            // Read improves the error logging when the command fails
            let! _, _ =
                Command.ReadAsync(
                    "dotnet",
                    CmdLine.empty
                    |> CmdLine.appendPrefix "restore" projectName
                    |> CmdLine.appendPrefix "--packages" VirtualWorkspace.``test-package-cache``.``.``
                    |> CmdLine.toString,
                    workingDirectory = Workspace.fixtures.``.``
                )

            let extraArg = extraArg |> Option.defaultValue ""

            return!
                Command.ReadAsync(
                    "dotnet",
                    CmdLine.empty
                    |> CmdLine.appendPrefix "pack" projectName
                    |> CmdLine.appendPrefix "-c" "Release"
                    |> CmdLine.append "--no-restore"
                    |> CmdLine.appendIfNotNullOrEmpty extraArg
                    |> CmdLine.appendRaw "--getProperty:Version"
                    |> CmdLine.appendRaw "--getProperty:PackageVersion"
                    |> CmdLine.appendRaw "--getProperty:PackageReleaseNotes"
                    |> CmdLine.toString,
                    workingDirectory = Workspace.fixtures.``.``
                )
        }

    let packAndGetPackageProperties projectName =
        packAndGetPackagePropertiesWithExtraArg projectName None

    let getAssemblyInfoFromNupkg (projectName: string) version =
        let projectName = Path.GetFileNameWithoutExtension projectName

        let packageFile =
            Path.Combine(VirtualWorkspace.fixtures.bin.Release.``.``, $"{projectName}.{version}.nupkg")

        File.Exists(packageFile).Should().BeTrue() |> ignore

        use zip = ZipFile.OpenRead(packageFile)
        use zipStream = zip.Entries |> Seq.find (_.Name.EndsWith(".dll")) |> _.Open()
        use assemblyStream = new MemoryStream()
        zipStream.CopyTo assemblyStream

        let runtimeAssemblies =
            Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")

        use mlc = new MetadataLoadContext(PathAssemblyResolver runtimeAssemblies)
        let assembly = mlc.LoadFromStream assemblyStream

        assembly.CustomAttributes
        |> Seq.tryPick (fun attr ->
            match attr.ConstructorArguments |> Seq.map _.Value.ToString() |> Seq.toArray with
            | [| "BuildDate"; date |] -> Some date
            | _ -> None
        )

[<TestClass>]
type IntegrationTests() =
    [<TestInitialize>]
    member this.Initialize() =
        this.testPackageVersion <- $"0.0.1-test"
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

    member val testPackageVersion = null with get, set

    member this.AddPackageReference(projectName: string) : Task =
        task {
            let! struct (_, _) =
                Command.ReadAsync(
                    "dotnet",
                    CmdLine.empty
                    |> CmdLine.appendPrefix "add" projectName
                    |> CmdLine.appendPrefix "package" "Ionide.KeepAChangelog.Tasks"
                    // |> CmdLine.appendPrefix "--source" VirtualWorkspace.``test-nupkgs``.``.``
                    |> CmdLine.appendPrefix "--version" $"[{this.testPackageVersion}]"
                    |> CmdLine.toString,
                    workingDirectory = Workspace.fixtures.``.``
                )

            ()
        }

    [<TestMethod>]
    member this.``works for absolute path with keep a changelog``() : Task =
        task {
            let projectName = "WorksForAbsolutePathWithKeepAChangelog.fsproj"

            let! struct (stdout, _) = Utils.packAndGetPackageProperties projectName

            stdout
                .Should()
                .BeLineEndingEquivalent(
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

            let buildDate = Utils.getAssemblyInfoFromNupkg projectName "0.1.0"
            buildDate.Should().BeSome().WhoseValue.Should().Be("2022-01-13") |> ignore
        }

    [<TestMethod>]
    member this.``works for relative path with keep a changelog``() : Task =
        task {
            let projectName = "WorksForRelativePathWithKeepAChangelog.fsproj"

            do! this.AddPackageReference projectName

            let! struct (stdout, _) = Utils.packAndGetPackageProperties projectName

            let buildDate = Utils.getAssemblyInfoFromNupkg projectName "0.1.0"
            buildDate.Should().BeSome().WhoseValue.Should().Be("2022-01-13") |> ignore

            stdout
                .Should()
                .BeLineEndingEquivalent(
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

            do! this.AddPackageReference projectName

            let! struct (stdout, _) = Utils.packAndGetPackageProperties projectName

            stdout
                .Should()
                .BeLineEndingEquivalent(
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

            do! this.AddPackageReference projectName

            let! struct (stdout, _) = Utils.packAndGetPackageProperties projectName

            stdout
                .Should()
                .BeLineEndingEquivalent(
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
    member this.``generates a pre-release version if changelog has unreleased section``() : Task =
        task {
            let projectName = "WorksForUnreleased.fsproj"

            do! this.AddPackageReference projectName

            let! struct (stdout, _) = Utils.packAndGetPackageProperties projectName

            stdout
                .Should()
                .BeLineEndingEquivalent(
                    """{
  "Properties": {
    "Version": "0.1.1-alpha",
    "PackageVersion": "0.1.1-alpha",
    "PackageReleaseNotes": "### Removed\n\n- A test removal line\n- And another removal"
  }
}
"""
                )
            |> ignore

            let buildDate = Utils.getAssemblyInfoFromNupkg projectName "0.1.1-alpha"
            buildDate.Should().BeNone() |> ignore
        }

    [<TestMethod>]
    member this.``ignores a pre-release version if changelog has unreleased section but disabled``() : Task =
        task {
            let projectName = "WorksForUnreleasedWhenIgnored.fsproj"

            do! this.AddPackageReference projectName

            let! struct (stdout, _) = Utils.packAndGetPackageProperties projectName

            let buildDate = Utils.getAssemblyInfoFromNupkg projectName "0.1.0"
            buildDate.Should().BeSome().WhoseValue.Should().Be("2022-01-13") |> ignore

            stdout
                .Should()
                .BeLineEndingEquivalent(
                    """{
  "Properties": {
    "Version": "0.1.0",
    "PackageVersion": "0.1.0",
    "PackageReleaseNotes": "### Added\n\n- Created the package\n\n### Changed\n\n- Changed something in the package\n- Updated the target framework"
  }
}
"""
                )
            |> ignore
        }

    [<TestMethod>]
    member this.``doesn't write the build date if disabled``() : Task =
        task {
            let projectName = "IgnoresBuildDateIfConfigured.fsproj"

            do! this.AddPackageReference projectName

            let! struct (stdout, _) = Utils.packAndGetPackageProperties projectName

            stdout
                .Should()
                .BeLineEndingEquivalent(
                    """{
  "Properties": {
    "Version": "0.1.0",
    "PackageVersion": "0.1.0",
    "PackageReleaseNotes": "### Added\n\n- Created the package\n\n### Changed\n\n- Changed something in the package\n- Updated the target framework"
  }
}
"""
                )
            |> ignore

            let buildDate = Utils.getAssemblyInfoFromNupkg projectName "0.1.0"
            buildDate.Should().BeNone() |> ignore
        }
