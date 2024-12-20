module Tests.UnitTests

open Ionide.KeepAChangelog.Tasks.Test
open Moq
open Microsoft.Build.Framework
open Ionide.KeepAChangelog.Tasks
open Faqt
open Faqt.Operators
open Microsoft.VisualStudio.TestTools.UnitTesting
open Workspace
open Helpers

type TestContext = {
    BuildEngine: Mock<IBuildEngine>
    Errors: ResizeArray<BuildErrorEventArgs>
} with

    member this.PrintErrors() =
        this.Errors |> Seq.iter (fun error -> printfn "Error: %s" error.Message)

[<TestClass>]
type UnitTests() =

    member val context = Unchecked.defaultof<TestContext> with get, set

    [<TestInitialize>]
    member this.Initialize() =
        this.context <- {
            BuildEngine = Mock<IBuildEngine>()
            Errors = ResizeArray<BuildErrorEventArgs>()
        }

        this.context.BuildEngine
            .Setup(fun engine -> engine.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback(fun (args: BuildErrorEventArgs) -> this.context.Errors.Add(args))
        |> ignore

    [<TestMethod>]
    member this.``task fails when changelog file does not exist``() =

        let myTask = ParseChangeLogs(ChangelogFile = "ThisFileDoesNotExist.md")
        myTask.BuildEngine <- this.context.BuildEngine.Object

        let success = myTask.Execute()

        %success.Should().BeFalse()
        %this.context.Errors.Count.Should().Be(1)
        %this.context.Errors.[0].Code.Should().Be("IKC0001")

    [<TestMethod>]
    member this.``task succeeds when changelog file exists (relative path)``() =
        // When running tests, the working directory is where the dll is located
        let myTask = ParseChangeLogs(ChangelogFile = "../../../changelogs/CHANGELOG.md")

        myTask.BuildEngine <- this.context.BuildEngine.Object

        let success = myTask.Execute()

        this.context.PrintErrors()

        %success.Should().BeTrue()
        %this.context.Errors.Count.Should().Be(0)

    [<TestMethod>]
    member this.``task succeeds when changelog file exists (absolute path)``() =
        let myTask = ParseChangeLogs(ChangelogFile = Workspace.changelogs.``CHANGELOG.md``)
        myTask.BuildEngine <- this.context.BuildEngine.Object

        let success = myTask.Execute()

        %success.Should().BeTrue()
        %this.context.Errors.Count.Should().Be(0)

    [<TestMethod>]
    member this.``task fails when changelog file is invalid``() =
        let myTask =
            ParseChangeLogs(ChangelogFile = Workspace.changelogs.``CHANGELOG_invalid.md``)

        myTask.BuildEngine <- this.context.BuildEngine.Object

        let success = myTask.Execute()

        %success.Should().BeFalse()
        %this.context.Errors.Count.Should().Be(1)
        %this.context.Errors.[0].Code.Should().Be("IKC0002")

    [<TestMethod>]
    member this.``task correctly parses details from changelog file``() =
        let myTask =
            ParseChangeLogs(ChangelogFile = Workspace.changelogs.``CHANGELOG_detailed.md``)

        myTask.BuildEngine <- this.context.BuildEngine.Object

        let success = myTask.Execute()
        %success.Should().BeTrue "Should have successfully parsed the changelog data"
        %myTask.AllReleasedChangelogs.Length.Should().Be(9, "Should have 9 versions")

        %myTask.CurrentReleaseChangelog.ItemSpec
            .Should()
            .Be("0.1.8", "Should have the most recent version")

        %myTask.CurrentReleaseChangelog
            .GetMetadata("Date")
            .Should()
            .Be("2022-03-31", "Should have the most recent version's date")

        %(myTask.CurrentReleaseChangelog.MetadataNames
          |> Seq.cast
          |> _.Should().Contain("Changed", "Should have changed metadata"))

        %(myTask.CurrentReleaseChangelog.MetadataNames
          |> Seq.cast
          |> _.Should().Contain("Date", "Should have date metadata"))

    [<TestMethod>]
    member this.``task produces expected markdown``() =
        let myTask = ParseChangeLogs(ChangelogFile = Workspace.changelogs.``CHANGELOG.md``)

        myTask.BuildEngine <- this.context.BuildEngine.Object

        let success = myTask.Execute()
        %success.Should().BeTrue "Should have successfully parsed the changelog data"

        %myTask.LatestReleaseNotes
            .Should()
            .BeLineEndingEquivalent(
                """### Added

- Created the package

### Changed

- Changed something in the package
- Updated the target framework"""
            )
