namespace Ionide.KeepAChangelog.Tasks;

using CSharpFunctionalExtensions;
using KeepAChangelogParser;
using KeepAChangelogParser.Models;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System;
using System.Globalization;
using SemVersion;
using System.Text;

public static class Mappings
{
    public static string EnumName<T>(this T enumValue) where T : struct, Enum => Enum.GetName(typeof(T), enumValue);

    public static Dictionary<string, string> ToTaskItemMetadata(this ChangelogSubSectionCollection sections)
    {
        var metadata = new Dictionary<string, string>();
        foreach (var section in sections)
        {
            var sectionText = System.String.Join(System.Environment.NewLine, section.ItemCollection.Select(item => item.MarkdownText));
            metadata.Add(section.Type.EnumName(), sectionText);
        }
        return metadata;
    }

    public static ITaskItem ToTaskItem(this ChangelogSectionUnreleased unreleased) => new TaskItem("Unreleased", unreleased.SubSectionCollection.ToTaskItemMetadata());

    public static ITaskItem ToTaskItem(this (SemanticVersion version, DateTime date, ChangelogSubSectionCollection subsections) release)
    {
        var sectionMetadata = release.subsections.ToTaskItemMetadata();
        sectionMetadata.Add("Date", release.date.ToString("yyyy-MM-dd"));
        return new TaskItem(release.version.ToString(), sectionMetadata);
    }

    public static Result<SemVersion.SemanticVersion> SemanticVersion(this ChangelogSection section)
    {
        var success = SemVersion.SemanticVersion.TryParse(section.MarkdownVersion, out var version);
        if (success)
        {
            return Result.Success(version);
        }
        else
        {
            return Result.Failure<SemVersion.SemanticVersion>($"Unable to parse '{section.MarkdownVersion}' as a Semantic Version.");
        }
    }

    public static DateTime ToDateTime(this ChangelogSection section) => DateTime.ParseExact(section.MarkdownDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);


    public static IEnumerable<(SemVersion.SemanticVersion version, DateTime date, ChangelogSubSectionCollection subsections)> Unwrapped(this ChangelogSectionCollection sections)
    {
        foreach (var section in sections)
        {
            var validated =
                section.SemanticVersion().Map(v => (v, section.ToDateTime(), section.SubSectionCollection));
            if (validated.IsSuccess)
            {
                yield return validated.Value;
            }
        }
    }

    public static string ToMarkdown(this ChangelogSubSectionCollection subsections)
    {
        var builder = new StringBuilder();
        foreach (var subsection in subsections)
        {
            builder.AppendLine($"### {subsection.Type.EnumName()}");
            foreach (var line in subsection.ItemCollection)
            {
                builder.AppendLine(line.MarkdownText);
            }
        }
        return builder.ToString();
    }
}

public class ParseChangelogs : Task
{

    public ParseChangelogs() : base()
    {
    }

    [Required]
    public string ChangelogFile { get; set; } = null;

    [Output]
    public ITaskItem UnreleasedChangelog { get; set; } = null;

    [Output]
    public ITaskItem CurrentReleaseChangelog { get; set; } = null;

    [Output]
    public ITaskItem[] AllReleasedChangelogs { get; set; } = null;

    [Output]
    public string LatestReleaseNotes { get; set; } = null;

    public override bool Execute()
    {
        FileInfo file = new(ChangelogFile);
        if (!file.Exists)
        {
            LogMissingFileError();
            return false;
        }
        var text = System.IO.File.ReadAllText(file.FullName);
        var thing = new ChangelogParser().Parse(text);
        if (thing.IsFailure)
        {
            LogInvalidChangelogError(thing.Error);
            return false;
        }
        else
        {
            var changelog = thing.Value;
            if (changelog.SectionUnreleased != null)
            {
                UnreleasedChangelog = changelog.SectionUnreleased.ToTaskItem();
            }

            var releases =
                changelog.SectionCollection
                    .Unwrapped()
                    .OrderByDescending(r => r.version)
                    .ToArray();

            var latestRelease = releases[0];

            var mapped = releases.Select(r => r.ToTaskItem()).ToArray();

            CurrentReleaseChangelog = mapped[0];
            AllReleasedChangelogs = mapped;
            LatestReleaseNotes = latestRelease.subsections.ToMarkdown();
            return true;
        }
    }

    private void LogAnnotatedError(string code, string helpCategory, string messageFormat, params object[] messageArgs)
    {
        Log.LogError("CHANGELOG", code, helpCategory, this.BuildEngine.ProjectFileOfTaskNode, this.BuildEngine.LineNumberOfTaskNode, this.BuildEngine.ColumnNumberOfTaskNode, this.BuildEngine.LineNumberOfTaskNode, this.BuildEngine.ColumnNumberOfTaskNode, messageFormat, messageArgs);
    }

    private void LogNoChangelogSetError() {
        LogAnnotatedError("CNG0001", "No Changelog Set", "The ChangelogFile property was not set", ChangelogFile);
    }
    private void LogMissingFileError()
    {
        LogAnnotatedError("CNG0002", "Missing Changelog File", "The Changelog file {0} was not found.", ChangelogFile);
    }
    private void LogInvalidChangelogError(string parseError)
    {
        LogAnnotatedError("CNG0003", "Invalid Changelog", "The Changelog file {0} is invalid. The error was: {1}", ChangelogFile, parseError);
    }
}

