module KeepAChangelog.Tasks

open Microsoft.Build.Utilities
open Microsoft.Build.Framework
open System.IO
open KeepAChangelogParser
open KeepAChangelogParser.Models
open KeepAChangelog
open FsToolkit.ErrorHandling

module Result =

    let toBool =
        function
        | Ok _ -> true
        | Error _ -> false

type ParseChangelog() =
    inherit Task()

    [<Required>]
    member val ChangelogFile: string = null with get, set

    [<Output>]
    member val UnreleasedChangelog: ITaskItem = null with get, set

    [<Output>]
    member val CurrentReleaseChangelog: ITaskItem = null with get, set

    [<Output>]
    member val AllReleasedChangelogs: ITaskItem[] = null with get, set

    [<Output>]
    member val LatestReleaseNotes: string = null with get, set

    override this.Execute() : bool =
        let file = this.ChangelogFile |> FileInfo

        // Using result CE to make code easier to read by avoiding nested if statements
        result {
            do! this.CheckFileExists file
            let! changelog = this.ParseChangelog file

            // changelog.

            // Done
            return true
        }
        |> Result.toBool

    member this.CheckFileExists(fileInfo: FileInfo) =
        if fileInfo.Exists then
            Ok()
        else
            this.LogError(Log.changelogFileNotFound fileInfo.FullName)
            Error()

    member this.ParseChangelog(fileInfo: FileInfo) : Result<Changelog, unit> =
        let changelogContent = File.ReadAllText(fileInfo.FullName)
        let parserResult = ChangelogParser().Parse(changelogContent)

        if parserResult.IsSuccess then
            Ok parserResult.Value
        else
            this.LogError(Log.invalidChangelog fileInfo.FullName parserResult.Error)
            Error()

    /// <summary>
    /// Helper method to log an error with the given log data.
    /// </summary>
    /// <param name="logData"></param>
    member this.LogError(logData: Log.LogData) =
        this.Log.LogError(
            "CHANGELOG",
            logData.ErrorCode,
            logData.HelpKeyword,
            this.BuildEngine.ProjectFileOfTaskNode,
            this.BuildEngine.LineNumberOfTaskNode,
            this.BuildEngine.ColumnNumberOfTaskNode,
            this.BuildEngine.LineNumberOfTaskNode,
            this.BuildEngine.ColumnNumberOfTaskNode,
            logData.Message,
            logData.MessageArgs
        )

//         else
//             // match Parser.parseChangeLog file with
//             // | Ok changelogs ->
//             //     changelogs.Unreleased
//             //     |> Option.iter (fun unreleased ->
//             //         this.UnreleasedChangelog <-
//             //             TaskItem()
//             //             |> Util.mapChangelogData unreleased
//             //             |> Util.mapUnreleasedInfo)

//             //     let sortedReleases =
//             //         // have to use LINQ here because List.sortBy* require IComparable, which
//             //         // semver doesn't implement
//             //         changelogs.Releases.OrderByDescending(fun (v, _, _) -> v)

//             //     let items =
//             //         sortedReleases
//             //         |> Seq.map (fun (version, date, data) ->
//             //             TaskItem()
//             //             |> Util.mapReleaseInfo version date
//             //             |> fun d -> match data with Some data -> Util.mapChangelogData data d | None -> d
//             //         )
//             //         |> Seq.toArray

//             //     this.AllReleasedChangelogs <- items
//             //     this.CurrentReleaseChangelog <- items.FirstOrDefault()

//             //     sortedReleases
//             //     |> Seq.tryHead
//             //     |> Option.iter (fun (version, date, data) ->
//             //         data
//             //         |> Option.iter (fun data ->
//             //             this.LatestReleaseNotes <- data.ToMarkdown())
//             //         )

//                 true
//             // | Error (formatted, msg) ->

//             //     this.Log.LogError(
//             //         $"Error parsing Changelog at {file.FullName}. The error occurred at {msg.Position}.{System.Environment.NewLine}{formatted}"
//             //     )

//             //     false
