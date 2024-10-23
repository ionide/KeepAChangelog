module Tests.UnitTests

open Moq
open Microsoft.Build.Framework
open Ionide.KeepAChangelog.Tasks
open Shouldly
open Microsoft.VisualStudio.TestTools.UnitTesting
open Workspace

type TestContext =
    {
        BuildEngine: Mock<IBuildEngine>
        Errors: ResizeArray<BuildErrorEventArgs>
    }

    member this.PrintErrors() =
        this.Errors |> Seq.iter (fun error -> printfn "Error: %s" error.Message)
[<TestClass>]
type UnitTests() =

    member val context = Unchecked.defaultof<TestContext> with get, set
    [<TestInitialize>]
    member this.Initialize() =
        this.context <-
            {
                BuildEngine = Mock<IBuildEngine>()
                Errors = ResizeArray<BuildErrorEventArgs>()
            }

        this.context.BuildEngine
            .Setup(fun engine -> engine.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback(fun (args: BuildErrorEventArgs) -> this.context.Errors.Add(args))
        |> ignore

    [<TestMethod>]
    member this.``task fails when changelog file does not exist`` () =

        let myTask = ParseChangeLogs(ChangelogFile = "ThisFileDoesNotExist.md")
        myTask.BuildEngine <- this.context.BuildEngine.Object

        let success = myTask.Execute()

        success.ShouldBeFalse()
        this.context.Errors.Count.ShouldBe(1)
        this.context.Errors.[0].Code.ShouldBe("IKC0001")

    [<TestMethod>]
    member this.``task succeeds when changelog file exists (relative path)`` () =
        // When running tests, the working directory is where the dll is located
        let myTask = ParseChangeLogs(ChangelogFile = "../../../changelogs/CHANGELOG.md")

        myTask.BuildEngine <- this.context.BuildEngine.Object

        let success = myTask.Execute()

        this.context.PrintErrors()

        success.ShouldBeTrue()
        this.context.Errors.Count.ShouldBe(0)

    [<TestMethod>]
    member this.``task succeeds when changelog file exists (absolute path)`` () =
        let myTask = ParseChangeLogs(ChangelogFile = Workspace.changelogs.``CHANGELOG.md``)
        myTask.BuildEngine <- this.context.BuildEngine.Object

        let success = myTask.Execute()

        success.ShouldBeTrue()
        this.context.Errors.Count.ShouldBe(0)

    [<TestMethod>]
    member this.``task fails when changelog file is invalid`` () =
        let myTask =
            ParseChangeLogs(ChangelogFile = Workspace.changelogs.``CHANGELOG_invalid.md``)

        myTask.BuildEngine <- this.context.BuildEngine.Object

        let success = myTask.Execute()

        success.ShouldBeFalse()
        this.context.Errors.Count.ShouldBe(1)
        this.context.Errors.[0].Code.ShouldBe("IKC0002")


    [<TestMethod>]
    member this.``task correctly parses detailes from changelog file`` () =
        let myTask =
            ParseChangeLogs(ChangelogFile = Workspace.changelogs.``CHANGELOG_detailed.md``)

        myTask.BuildEngine <- this.context.BuildEngine.Object

        let success = myTask.Execute()
        success.ShouldBeTrue "Should have successfully parsed the changelog data"
        myTask.AllReleasedChangelogs.Length.ShouldBe(9, "Should have 9 versions")
        myTask.CurrentReleaseChangelog.ItemSpec.ShouldBe("0.1.8", "Should have the most recent version")
        myTask.CurrentReleaseChangelog.GetMetadata("Date").ShouldBe("2022-03-31", "Should have the most recent version's date")
        myTask.CurrentReleaseChangelog.MetadataNames |> Seq.cast |> _.ShouldContain("Changed", "Should have changed metadata")
        myTask.CurrentReleaseChangelog.MetadataNames |> Seq.cast |> _.ShouldContain("Date", "Should have date metadata")
