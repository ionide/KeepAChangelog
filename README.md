# Ionide.KeepAChangelog

This project implements a Changelog parser according to the spec at KeepAChangelog. It also provides MSBuild tasks and targets to automate the setting of **Versions** and **Package Release Notes** for your NuGet packages, so that the Changelogs are your source of truth.

When configured, this package will set the `Version`, `PackageVersion`, and `PackageReleaseNotes` of your packable project with the matching data from the latest Changelog release, as well as adding AssemblyMetadata for the `BuildDate` in the `YYYY-mm-dd` format.

## Installation

The MSBuild package is authored as a set of tasks and targets that are used automatically.  You just have to install the `Ionide.KeepAChangelog.Tasks` package and you're all set!

```xml
<ItemGroup>
    <PackageReference Include="Ionide.KeepAChangelog.Tasks" Version="<insert here>" PrivateAssets="all" />
</ItemGroup>
```

## Examples

It might be helpful to see how this library can help you.  Imagine you have a project file like this:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Main.fs" />
  </ItemGroup>
</Project>
```

and a CHANGELOG.md file like this:

```md
# Changelog 

## 1.0.0 - 2022-01-14

### Added

* Initial release
```

packaging the project with this library results in the same result as packing a project that looks like this:

```xml
<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup>
   <TargetFramework>netstandard2.0</TargetFramework>
   <GenerateDocumentationFile>true</GenerateDocumentationFile>
   <Versions>1.0.0</Versions>
   <ReleaseNotes>
## 1.0.0 - 2022-01-14

### Added

* Initial release
   </ReleaseNotes>
 </PropertyGroup>
 <ItemGroup>
   <Compile Include="Main.fs" />
 </ItemGroup>
</Project>
```

If your changelog has multiple versions, the latest one will be used.

## Customization

There's really only one property that matters for these targets, and that's `ChangelogFile`. This needs to point to the Changelog file you want to read, but it defaults to `CHANGELOG.md` in the root of a given project in case you want to adhere to defaults.

## API

When the task runs, it writes several output items and properties:

|Name|Type|Description|
|----|----|-----------|
| UnreleasedChangelog | UnreleasedChangelogData option | If present, there was an 'Unreleased' section in the Changelog. This structure will contain the sections present. |
| CurrentReleaseChangelog | ReleaseChangelogData option | If present, there was at least one released logged in the Changelog. This structure will contain the details of each one. |
| AllReleasedChangelogs | ReleaseChangelogData list | Contains the ordered list of all released in the ChangelogFile, descending. |
| LatestReleaseNotes | string option | If present, contains the concatenated list of all Changelog sections for the latest release. This is a convenience property so that you don't have to String.Join all the lines in the `ReleaseChangelogData` yourself! |

### ChangelogData

This TaskItem has metadata for each of the known sections of a Changelog:

* Added
* Changed
* Deprecated
* Removed
* Fixed
* Security

In each case, the value of the metadata is the newline-concatenated list of all of the Changelog Entries for that section.

### UnreleasedChangelogData

This structure is a `ChangelogData` with an `Identity` of `"Unreleased"`.

### ReleaseChangelogData

This structure is the same as `ChangelogData`, but it contains two more items of metadata:

* the `Identity` of the `TaskItem` is the Semantic Version of the release
* the `Date` of the `TaskItem` is the `YYYY-MM-DD`-formatted date of the release