module TaskTests

open Ionide.KeepAChangelog
open Expecto
open Ionide.KeepAChangelog.Tasks
open Microsoft.Build.Utilities
open Microsoft.Build.Framework
open Microsoft.Build
open System
open System.Collections
open System.Collections.Generic
open Microsoft.Build.Evaluation
open Microsoft.Build.Logging
open System.Collections.Concurrent
open System.Text

type MockLogger() =
    interface ILogger with
        member val Verbosity = LoggerVerbosity.Normal with get, set
        member val Parameters = null with get, set
        member x.Initialize _ = ()
        member x.Shutdown() = ()



type MockEngine() =
    let projectCollection = new ProjectCollection()
    let globalProperties = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    let objectCache = ConcurrentDictionary<obj, obj>()
    let locker = obj ()
    let log = StringBuilder()

    member x.MockLogger = MockLogger()
    member x.Log = log.ToString()

    interface IBuildEngine9 with
        member val AllowFailureWithoutError = false with get, set

        override this.BuildProjectFile
            (
                projectFileName: string,
                targetNames: string [],
                globalProperties: System.Collections.IDictionary,
                targetOutputs: System.Collections.IDictionary,
                toolsVersion: string
            ) : bool =
            let finalGlobalProperties =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

            // Finally, whatever global properties were passed into the task ... those are the final winners.
            if globalProperties <> null then
                for (newGlobalProperty: DictionaryEntry) in globalProperties |> Seq.cast do
                    finalGlobalProperties[(string) newGlobalProperty.Key] <- (string) newGlobalProperty.Value

            let project =
                projectCollection.LoadProject(projectFileName, finalGlobalProperties, toolsVersion)

            let loggers: ILogger seq = [ this.MockLogger; ConsoleLogger() ]

            project.Build(targetNames, loggers)

        override this.BuildProjectFile
            (
                projectFileName: string,
                targetNames: string [],
                globalProperties: System.Collections.IDictionary,
                targetOutputs: System.Collections.IDictionary
            ) : bool =
            (this: IBuildEngine9)
                .BuildProjectFile(projectFileName, targetNames, globalProperties, targetOutputs, null)

        override this.BuildProjectFilesInParallel
            (
                projectFileNames: string [],
                targetNames: string [],
                globalProperties: System.Collections.IDictionary [],
                removeGlobalProperties: System.Collections.Generic.IList<string> [],
                toolsVersion: string [],
                returnTargetOutputs: bool
            ) : BuildEngineResult =
            failwith "b"

        override this.BuildProjectFilesInParallel
            (
                projectFileNames: string [],
                targetNames: string [],
                globalProperties: System.Collections.IDictionary [],
                targetOutputsPerProject: System.Collections.IDictionary [],
                toolsVersion: string [],
                useResultsCache: bool,
                unloadProjectsOnCompletion: bool
            ) : bool =
            let includeTargetOutputs = targetOutputsPerProject <> null

            let result =
                (this: IBuildEngine9)
                    .BuildProjectFilesInParallel(
                        projectFileNames,
                        targetNames,
                        globalProperties,
                        Array.zeroCreate projectFileNames.Length,
                        toolsVersion,
                        includeTargetOutputs
                    )

            if includeTargetOutputs then
                for i in 0 .. targetOutputsPerProject.Length do
                    if targetOutputsPerProject[i] <> null then
                        for (output: KeyValuePair<string, ITaskItem []>) in result.TargetOutputsPerProject[i] do
                            targetOutputsPerProject[i]
                                .Add(output.Key, output.Value)

            result.Result

        override this.ColumnNumberOfTaskNode = 0
        override this.ContinueOnError = false

        override this.GetGlobalProperties() = globalProperties

        override this.GetRegisteredTaskObject(key: obj, lifetime: RegisteredTaskObjectLifetime) : obj =
            match objectCache.TryGetValue(key) with
            | true, obj -> obj
            | false, o -> o

        override this.IsRunningMultipleNodes: bool = false
        override this.LineNumberOfTaskNode: int = 0

        override this.LogCustomEvent(e: CustomBuildEventArgs) : unit =
            lock locker (fun _ -> log.AppendLine e.Message |> ignore)

        override this.LogErrorEvent(e: BuildErrorEventArgs) : unit =
            lock locker (fun _ -> log.AppendLine e.Message |> ignore)

        override this.LogMessageEvent(e: BuildMessageEventArgs) : unit =
            lock locker (fun _ -> log.AppendLine e.Message |> ignore)

        override this.LogTelemetry
            (
                eventName: string,
                properties: System.Collections.Generic.IDictionary<string, string>
            ) : unit =
            let mutable message = $"""{eventName}:{Environment.NewLine}"""

            for (property: KeyValuePair<string, string>) in properties do
                message <-
                    message
                    + $"{property.Key}={property.Value};{Environment.NewLine}"

            lock locker (fun _ -> log.AppendLine message |> ignore)

        override this.LogWarningEvent(e: BuildWarningEventArgs) : unit =
            lock locker (fun _ -> log.AppendLine e.Message |> ignore)

        override this.ProjectFileOfTaskNode: string = ""
        override this.Reacquire() : unit = ()

        override this.RegisterTaskObject
            (
                key: obj,
                obj: obj,
                lifetime: RegisteredTaskObjectLifetime,
                allowEarlyCollection: bool
            ) : unit =
            objectCache[key] <- obj

        override this.ReleaseCores(coresToRelease: int) : unit = ()
        override this.RequestCores(requestedCores: int) : int = requestedCores
        override this.ShouldTreatWarningAsError(warningCode: string) : bool = false

        override this.UnregisterTaskObject(key: obj, lifetime: RegisteredTaskObjectLifetime) : obj =
            match objectCache.TryRemove key with
            | true, o -> o
            | false, o -> o

        override this.Yield() : unit = ()

let tests =
    testList
        "MSBuild Task"
        [ test "should fail without changelog file" {
              let t = ParseChangelogs()
              let engine = MockEngine()
              t.BuildEngine <- engine
              t.ChangelogFile <- "farts"
              let result = t.Execute()
              Expect.isFalse result "should have failed"

              Expect.stringContains
                  engine.Log
                  "farts could not be found"
                  "should have errored complaining about missing file"
          } ]
