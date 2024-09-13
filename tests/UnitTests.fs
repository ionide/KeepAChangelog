module Tests.UnitTests

open Tests.Setup
open Moq
open Microsoft.Build.Framework
open KeepAChangelog.Tasks
open Shouldly
open Workspace

type TestContext =
    {
        BuildEngine: Mock<IBuildEngine>
        Errors: ResizeArray<BuildErrorEventArgs>
    }

    member this.PrintErrors() =
        this.Errors |> Seq.iter (fun error -> printfn "Error: %s" error.Message)

let private setupBuildEngine () =
    let context =
        {
            BuildEngine = Mock<IBuildEngine>()
            Errors = ResizeArray<BuildErrorEventArgs>()
        }

    context.BuildEngine
        .Setup(fun engine -> engine.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
        .Callback(fun (args: BuildErrorEventArgs) -> context.Errors.Add(args))
    |> ignore

    context

[<Test>]
let ``task fails when changelog file does not exist`` () =
    let context = setupBuildEngine ()

    let myTask = ParseChangelog(ChangelogFile = "ThisFileDoesNotExist.md")
    myTask.BuildEngine <- context.BuildEngine.Object

    let success = myTask.Execute()

    success.ShouldBeFalse()
    context.Errors.Count.ShouldBe(1)
    context.Errors.[0].Code.ShouldBe("IKC0001")

[<Test>]
let ``task succeeds when changelog file exists (relative path)`` () =
    let context = setupBuildEngine ()

    // When running tests, the working directory is where the dll is located
    let myTask = ParseChangelog(ChangelogFile = "../../../fixtures/CHANGELOG.md")

    myTask.BuildEngine <- context.BuildEngine.Object

    let success = myTask.Execute()

    context.PrintErrors()

    success.ShouldBeTrue()
    context.Errors.Count.ShouldBe(0)

[<Test>]
let ``task succeeds when changelog file exists (absolute path)`` () =
    let context = setupBuildEngine ()

    let myTask = ParseChangelog(ChangelogFile = Workspace.fixtures.``CHANGELOG.md``)
    myTask.BuildEngine <- context.BuildEngine.Object

    let success = myTask.Execute()

    success.ShouldBeTrue()
    context.Errors.Count.ShouldBe(0)

[<Test>]
let ``task fails when changelog file is invalid`` () =
    let context = setupBuildEngine ()

    let myTask =
        ParseChangelog(ChangelogFile = Workspace.fixtures.``CHANGELOG_invalid.md``)

    myTask.BuildEngine <- context.BuildEngine.Object

    let success = myTask.Execute()

    success.ShouldBeFalse()
    context.Errors.Count.ShouldBe(1)
    context.Errors.[0].Code.ShouldBe("IKC0002")
