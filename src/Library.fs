namespace KeepAChangelog.Tasks

open Microsoft.Build.Utilities
open Microsoft.Build.Framework
open System.IO
open KeepAChangelogParser
open KeepAChangelogParser.Models

// type ParseChangelogs() =
//     inherit Task()

//     [<Required>]
//     member val ChangelogFile: string = null with get, set

//     [<Output>]
//     member val UnreleasedChangelog: ITaskItem = null with get, set

//     [<Output>]
//     member val CurrentReleaseChangelog: ITaskItem = null with get, set

//     [<Output>]
//     member val AllReleasedChangelogs: ITaskItem [] = null with get, set

//     [<Output>]
//     member val LatestReleaseNotes: string = null with get, set

//     override this.Execute() : bool =
//         let file = this.ChangelogFile |> FileInfo

//         if not file.Exists then
//             this.Log.LogError($"The file {file.FullName} could not be found.")
//             false
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


type ParseChangelogs() =
    inherit Task()

    [<Required>]
    member val ChangelogFile: string = null with get, set

    [<Output>]
    member val MaximeTest : string = null with get, set

    override this.Execute() : bool =
        this.Log.LogError("The file could not be found.")
        this.MaximeTest <- "test"
        true