module Tests.IntegrationTests

open Tests.Setup
open Moq
open Microsoft.Build.Framework
open KeepAChangelog.Tasks
open Shouldly 

[<Test>]
let ``works for 'ci' type with default config`` () =
    let buildEngine = Mock<IBuildEngine>()
    let errors = ResizeArray<BuildErrorEventArgs>()

    buildEngine.Setup(fun x -> x.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
        .Callback(fun args -> errors.Add(args))
        |> ignore

    let item = Mock<ITaskItem>()
    item.Setup(fun x -> x.GetMetadata("MaximeTest")).Returns("test") |> ignore
 
    let myTask = ParseChangelogs(ChangelogFile = "MyChangelog.md")
    myTask.BuildEngine <- buildEngine.Object

    let success = myTask.Execute()

    success.ShouldBeTrue()
