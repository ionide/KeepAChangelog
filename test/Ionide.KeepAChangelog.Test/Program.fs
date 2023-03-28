open System
open SemVersion
open Expecto
open Microsoft.Build.Utilities
open Microsoft.Build.Framework

let getMockedTask () =
    let t = Ionide.KeepAChangelog.Tasks.ParseChangelogs()

    let engine =
        { new IBuildEngine with
            member x.LogErrorEvent(e: BuildErrorEventArgs) = Console.WriteLine(e.Message)

            member this.BuildProjectFile
                (
                    projectFileName: string,
                    targetNames: string[],
                    globalProperties: Collections.IDictionary,
                    targetOutputs: Collections.IDictionary
                ) : bool =
                failwith "Not Implemented"

            member this.ColumnNumberOfTaskNode: int = 1
            member this.ContinueOnError: bool = failwith "Not Implemented"
            member this.LineNumberOfTaskNode: int = 2
            member this.LogCustomEvent(e: CustomBuildEventArgs) : unit = failwith "Not Implemented"
            member this.LogMessageEvent(e: BuildMessageEventArgs) : unit = failwith "Not Implemented"
            member this.LogWarningEvent(e: BuildWarningEventArgs) : unit = failwith "Not Implemented"
            member this.ProjectFileOfTaskNode: string = System.IO.Path.GetTempFileName() }

    t.BuildEngine <- engine
    t

let changelogFile = System.IO.Path.GetFullPath "./CHANGELOG.md"

let canRunTask =
    test "Can run task" {
        let t = getMockedTask ()
        t.ChangelogFile <- changelogFile
        let result = t.Execute()
        Expect.equal result true "Should have successfully parsed the changelog data"
        let versions = t.AllReleasedChangelogs
        Expect.equal versions.Length 9 "Should have 9 versions"
        let mostRecent = t.CurrentReleaseChangelog
        Expect.equal mostRecent.ItemSpec "0.1.8" "Should have the most recent version"
        Expect.equal (mostRecent.GetMetadata("Date")) "2022-03-31" "Should have the most recent version's date"
        let metadatas = mostRecent.MetadataNames |> Seq.cast |> Seq.toList
        Expect.containsAll metadatas [ "Changed"; "Date" ] "Should have the metadatas"
    }

let tokenize file =
    let text = System.IO.File.ReadAllText file

    let tokenizerTy =
        typeof<KeepAChangelogParser.ChangelogParser>.Assembly.GetTypes ()
        |> Array.find (fun t -> t.Name = "ChangelogTokenizer")

    let tokenizeMethod =
        tokenizerTy.GetMethod("Tokenize", [| typeof<string>; typeof<string> |])

    let tokenizer =
        tokenizerTy
            .GetConstructor(Array.empty)
            .Invoke(Array.empty)

    tokenizeMethod.Invoke(tokenizer, [| text; System.Environment.NewLine |]) :?> seq<obj>

let debuggerDisplay (o: obj) =
    o
        .GetType()
        .GetProperty(
            "debuggerDisplay",
            Reflection.BindingFlags.NonPublic
            ||| Reflection.BindingFlags.Instance
        )
        .GetValue(o)
    :?> string

let sampleTokenize =
    test "tokenization" {
        let tokens = tokenize changelogFile

        for token in tokens do
            printfn "%s" (debuggerDisplay token)
    }

[<Tests>]
let tests =
    testList
        "All"
        [ canRunTask
          // sampleTokenize
          ]

[<EntryPoint>]
let main argv =
    runTestsWithCLIArgs Seq.empty argv tests
