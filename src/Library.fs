namespace Ionide.KeepAChangelog.Tasks

open System
open System.Globalization
open System.Runtime.CompilerServices
open System.Text
open FsToolkit.ErrorHandling.Operator.Result
open Microsoft.Build.Utilities
open Microsoft.Build.Framework
open System.IO
open KeepAChangelogParser
open KeepAChangelogParser.Models
open KeepAChangelog
open FsToolkit.ErrorHandling
open Semver

module Result =

    let toBool =
        function
        | Ok _ -> true
        | Error _ -> false

type ChangelogExtensions =
    [<Extension>]
    static member inline ToDateTime(section: ChangelogSection) =
        DateTime.ParseExact(section.MarkdownDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)

    [<Extension>]
    static member ToTaskItemMetadata(sections: ChangelogSubSectionCollection) =
        sections
        |> Seq.map (fun section ->
            (string section.Type,
             section.ItemCollection
             |> Seq.map _.MarkdownText
             |> String.concat Environment.NewLine)
        )

    [<Extension>]
    static member ToTaskItem(unreleased: ChangelogSectionUnreleased) =
        let taskItem = TaskItem("unreleased")

        for (key, value) in unreleased.SubSectionCollection.ToTaskItemMetadata() do
            taskItem.SetMetadata(key, value)

        taskItem

    [<Extension>]
    static member Unwrapped(sections: ChangelogSectionCollection) =
        sections
        |> Seq.choose (fun section ->
            match SemVersion.TryParse(section.MarkdownVersion, SemVersionStyles.Any) with
            | false, _ -> None
            | true, version ->
                Some
                    {|
                        version = version
                        date = section.ToDateTime()
                        collection = section.SubSectionCollection
                    |}
        )

    [<Extension>]
    static member ToMarkdown(subsections: ChangelogSubSectionCollection) =
        let builder = StringBuilder()

        subsections
        |> Seq.fold
            (fun (builder: StringBuilder) subsection ->
                let state =
                    builder.AppendLine $"### {subsection.Type}" |> (fun x -> x.AppendLine "")

                subsection.ItemCollection
                |> Seq.fold (fun (builder: StringBuilder) line -> builder.AppendLine $"- {line.MarkdownText}") state
                |> (fun x -> x.AppendLine "")
            )
            builder
        |> _.ToString()
        |> _.Trim()

type ParseChangeLogs() =
    inherit Task()

    [<Required>]
    member val ChangelogFile: string = null with get, set

    [<Output>]
    member val UnreleasedChangelog: ITaskItem = null with get, set

    [<Output>]
    member val UnreleasedReleaseNotes: string = null with get, set

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

            do! this.ReadUnreleasedSection changelog
            let latestRelease = this.ProcessReleases changelog
            do! this.UpdateUnreleasedVersion latestRelease

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

    member this.ReadUnreleasedSection(changelog: Changelog) =
        let unreleasedSection = changelog.SectionUnreleased

        match unreleasedSection.MarkdownTitle with
        | "" -> Ok()
        | _ ->
            this.UnreleasedChangelog <- unreleasedSection.ToTaskItem()
            this.UnreleasedReleaseNotes <- unreleasedSection.SubSectionCollection.ToMarkdown()
            Ok()

    member this.ProcessReleases(changelog: Changelog) =
        let releases =
            changelog.SectionCollection.Unwrapped()
            |> Seq.sortByDescending _.version
            |> Seq.toList

        match releases with
        | [] -> None
        | latestRelease :: _ ->
            let mapped =
                releases
                |> List.map (fun x ->
                    let taskItem = TaskItem(x.version.ToString())
                    taskItem.SetMetadata("Date", x.date.ToString("yyyy-MM-dd"))

                    for (key, value) in x.collection.ToTaskItemMetadata() do
                        taskItem.SetMetadata(key, value)

                    taskItem :> ITaskItem
                )
                |> Array.ofList

            this.CurrentReleaseChangelog <- mapped[0]
            this.AllReleasedChangelogs <- mapped
            this.LatestReleaseNotes <- latestRelease.collection.ToMarkdown()
            Some(latestRelease.version)

    member this.UpdateUnreleasedVersion(latestVersion: SemVersion option) =
        let latestVersion = latestVersion |> Option.defaultValue (SemVersion(0, 0, 0))

        match this.UnreleasedChangelog with
        | null -> Ok()
        | _ ->
            let newUnreleased =
                latestVersion.WithPrereleaseParsedFrom "alpha"
                |> _.WithPatch(latestVersion.Patch + 1)

            this.UnreleasedChangelog.ItemSpec <- newUnreleased.ToString()
            Ok()

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
