namespace Ionide.KeepAChangelog.Tasks

open System
open System.Globalization
open System.Runtime.CompilerServices
open System.Text
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
    // public static Dictionary<string, string> ToTaskItemMetadata(this ChangelogSubSectionCollection sections)
    // {
    //     var metadata = new Dictionary<string, string>();
    //     foreach (var section in sections)
    //     {
    //         var sectionText = System.String.Join(System.Environment.NewLine, section.ItemCollection.Select(item => item.MarkdownText));
    //         metadata.Add(section.Type.EnumName(), sectionText);
    //     }
    //     return metadata;
    // }
[<AutoOpen>]
module Utils =
    let toTaskItemMetadata (sections: ChangelogSubSectionCollection) =
        sections
        |> Seq.map (fun section ->
            (string section.Type,
             section.ItemCollection |> Seq.map _.MarkdownText |> String.concat (System.Environment.NewLine))
        )

    let toTaskItem (unreleased : ChangelogSectionUnreleased) =
        let taskItem = TaskItem("unreleased")
        for (key, value) in toTaskItemMetadata unreleased.SubSectionCollection do
            taskItem.SetMetadata(key, value)
        taskItem

    [<Extension>]
    type Extensions =
        [<Extension>]
        static member inline ToDateTime(section: ChangelogSection) = DateTime.ParseExact(section.MarkdownDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)

    let unwrapped (sections: ChangelogSectionCollection) =
        sections |> Seq.choose (fun section ->
            match SemVersion.TryParse section.MarkdownVersion with
            | false, _ -> None
            | true, version ->
                Some {| version = version; dateTime = section.ToDateTime(); collection = section.SubSectionCollection|}
        )
    let toMarkdown (subsections: ChangelogSubSectionCollection) =
        let builder = StringBuilder()
        subsections |> Seq.fold (fun (builder : StringBuilder) subsection ->
            let state = builder.AppendLine $"### {subsection.Type}"
            subsection.ItemCollection
            |> Seq.fold (fun (builder : StringBuilder) line -> builder.AppendLine line.MarkdownText) state
        ) builder
        |> _.ToString()

type ParseChangeLogs() =
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

            do! this.ReadUnreleasedSection changelog
            do! this.ProcessReleases changelog

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

    member this.ReadUnreleasedSection(changelog: Changelog) =
        match changelog.SectionUnreleased with
        | null -> Ok()
        | unreleased ->
            this.UnreleasedChangelog <- unreleased |> toTaskItem
            Ok()

    member this.ProcessReleases(changelog: Changelog) =
        let releases = changelog.SectionCollection |> unwrapped |> Seq.sortByDescending _.version |> Seq.toArray

        let latestRelease = releases |> (fun x -> x[0])

        let mapped = releases |> Array.map (fun x ->
            let taskItem = TaskItem(x.version.ToString())
            taskItem.SetMetadata("Date", x.dateTime.ToString("yyyy-MM-dd"))
            for (key, value) in x.collection |> toTaskItemMetadata do
                taskItem.SetMetadata(key, value)
            taskItem :> ITaskItem
        )

        this.CurrentReleaseChangelog <- mapped[0]
        this.AllReleasedChangelogs <- mapped
        this.LatestReleaseNotes <- latestRelease.collection |> toMarkdown
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
