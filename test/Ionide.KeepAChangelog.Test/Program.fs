open Ionide.KeepAChangelog

open System
open SemVersion
open Expecto
open Ionide.KeepAChangelog.Domain

let normalizeNewline (v:string) = v.Replace("\r", "")

let singleRelease =
    normalizeNewline """## [1.0.0] - 2017-06-20
### Added
- A

### Changed
- B

### Removed
- C
"""

let singleReleaseExpected =
    {
        Version = SemanticVersion.Parse "1.0.0"
        Date = DateTime(2017, 06, 20)
        Data = Some {
            ChangelogData.Default with
                Added = "- A\n"
                Changed = "- B\n"
                Removed = "- C\n"
        }
    }

let keepAChangelog =
    normalizeNewline """# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2017-06-20
### Added
- A

### Changed
- B

### Removed
- C

## [0.3.0] - 2015-12-03
### Added
- A
- B
- C

"""

let keepAChangelogExpected: Changelogs =
    {
        Unreleased = None
        Releases = [
            singleReleaseExpected
            {
                Version = SemanticVersion.Parse "0.3.0"
                Date = DateTime(2015, 12, 03)
                Data = Some {
                    ChangelogData.Default with
                        Added = "- A\n- B\n- C\n\n"
                }
            }
        ]
    }

let header =
    normalizeNewline """# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

"""

let emptyUnreleased =
    normalizeNewline """## [Unreleased]

"""

let headerAndUnreleased = header + emptyUnreleased

let headerAndUnreleasedAndRelease = header + emptyUnreleased + singleRelease
let headerAndUnreleasedAndReleaseExpected = None, singleReleaseExpected

let sample1Release = normalizeNewline """## [0.3.1] - 8.1.2022

### Added

- Add XmlDocs to the generated package

"""

let sample1ReleaseExpected =
    {
        Version = SemanticVersion.Parse "0.3.1"
        Date = DateTime(2022, 1, 8)
        Data = Some { ChangelogData.Default with Added = "- Add XmlDocs to the generated package\n\n" }
    }
    
let sample = normalizeNewline """# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.3.1] - 8.1.2022

### Added

* Add XmlDocs to the generated package

## [0.3.0] - 23.11.2021

### Added

* Expose client `CodeAction` caps as CodeActionClientCapabilities. (by @razzmatazz)
* Map CodeAction.IsPreferred & CodeAction.Disabled props. (by @razzmatazz)

## [0.2.0] - 17.11.2021

### Added

* Add support for `codeAction/resolve` (by @razzmatazz)

## [0.1.1] - 15.11.2021

### Added

* Initial implementation
"""

let sampleExpected: Changelogs = {
    Unreleased = None
    Releases = [
        {
            Version = SemanticVersion.Parse "0.3.1"
            Date = DateTime(2022, 1, 8)
            Data = Some { ChangelogData.Default with Added = "* Add XmlDocs to the generated package\n" }
        }
        {
            Version = SemanticVersion.Parse "0.3.0"
            Date = DateTime(2021, 11, 23)
            Data = Some {
            ChangelogData.Default with
                Added =
                    normalizeNewline
                        """* Expose client `CodeAction` caps as CodeActionClientCapabilities. (by @razzmatazz)
* Map CodeAction.IsPreferred & CodeAction.Disabled props. (by @razzmatazz)
"""             }
        }
        {
            Version = SemanticVersion.Parse "0.2.0"
            Date = DateTime(2021, 11, 17)
            Data = Some { ChangelogData.Default with Added = "* Add support for `codeAction/resolve` (by @razzmatazz)\n" }
        }
        {
            Version = SemanticVersion.Parse "0.1.1"
            Date = DateTime(2021, 11, 15)
            Data = Some { ChangelogData.Default with Added = "* Initial implementation\n" }
        }
    ]
}

open FParsec
open FParsec.Primitives

let runSuccess label p text expected =
    test $"parsing {label}" {

        match FParsec.CharParsers.run p text with
        | FParsec.CharParsers.Success (r, _, _) ->
            Expect.equal r expected "Should have produced expected value"
        | FParsec.CharParsers.Failure (m, _, _) ->
            failwithf "%A" m
    }

let runSuccessNormalized label (p: Parser<string,unit>) text (expected:string) =
    test $"parsing {label}" {
        match FParsec.CharParsers.run p text with
        | FParsec.CharParsers.Success (r, _, _) ->
            let normalizedR = r.Replace("\r", "")
            let normalizedExpected = expected.Replace("\r", "")
            Expect.equal normalizedR normalizedExpected "Should have produced expected value"
        | FParsec.CharParsers.Failure (m, _, _) ->
            failwithf "%A" m
    }

let parsingExamples = testList "parsing examples" [
    runSuccess "line entry" Parser.pEntry "- A" "- A"
    runSuccess "header" Parser.pHeader header ()
    runSuccess "unreleased" Parser.pUnreleased emptyUnreleased None
    runSuccess "header and unreleased" (Parser.pHeader >>. Parser.pUnreleased) headerAndUnreleased None
    runSuccess "release" Parser.pRelease singleRelease singleReleaseExpected
    runSuccess "sample 1 release" Parser.pRelease sample1Release sample1ReleaseExpected
    runSuccess
        "header and unreleased and released"
        (Parser.pHeader >>. Parser.pUnreleased
         .>>. Parser.pRelease)
        headerAndUnreleasedAndRelease
        headerAndUnreleasedAndReleaseExpected
    runSuccess "keepachangelog" Parser.pChangeLogs keepAChangelog keepAChangelogExpected
    runSuccess "lsp changelog" Parser.pChangeLogs sample sampleExpected
]

let changelogDataTest =
    test "Transform ChangelogData to Markdown" {
        let changelogData =
            {
                Added = "* Added line 1\n* Added line 2\n"
                Changed = "* Changed line 1\n* Changed line 2\n"
                Deprecated = "* Deprecated line 1\n* Deprecated line 2\n"
                Removed = "* Removed line 1\n* Removed line 2\n"
                Fixed = "* Fixed line 1\n* Fixed line 2\n"
                Security = "* Security line 1\n* Security line 2\n"
                Custom =
                    [
                        "CustomHeaderA", "* Custom line 1\n* Custom line 2\n"
                        "CustomHeaderB", "* Custom line 3\n* Custom line 4\n"
                    ]
                    |> Map.ofList
            }

        let expected =
            normalizeNewline """### Added

* Added line 1
* Added line 2

### Changed

* Changed line 1
* Changed line 2

### Deprecated

* Deprecated line 1
* Deprecated line 2

### Removed

* Removed line 1
* Removed line 2

### Fixed

* Fixed line 1
* Fixed line 2

### Security

* Security line 1
* Security line 2

### CustomHeaderA

* Custom line 1
* Custom line 2

### CustomHeaderB

* Custom line 3
* Custom line 4
"""

        Expect.equal (normalizeNewline (changelogData.ToMarkdown())) expected "Should have produced expected value"
}

let FableSample = """# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased

### Fixed

#### Python

* Fix #3617: Fix comparaison between list option when one is None
* Fix #3615: Fix remove from dictionary with tuple as key
* Fix #3598: Using obj () now generated an empty dict instead of None
* Fix #3597: Do not translate .toString methods to str
* Fix #3610: Cleanup Python regex handling
* Fix #3628: System.DateTime.Substract not correctly transpiled

## 4.6.0 - 2023-11-27

### Changed

#### All

* Updated .NET metadata to 8.0.100 (by @ncave)

### Added

#### All

* Fix #3584: Unit type compiles to undeclared variable (by @ncave)

#### Python

* Support `DateTime(..., DateTimeKind.Utc).ToString("O")` (by @MangelMaxime)

#### Rust

* Added `Guid.TryParse`, `Guid.ToByteArray` (by @ncave)

### Fixed

#### Python

* Fixed char to string type regression with binary operator (by @dbrattli)
* Fix `DateTime(..., DateTimeKind.Local).ToString("O")` (by @MangelMaxime)
* Fix calling `value.ToString(CultureInfo.InvariantCulture)` (by @MangelMaxime)
* Fix #3605: Fix record equality comparison to works with optional fields (by @MangelMaxime and @dbrattli)
* PR #3608: Rewrite `time_span.py` allowing for better precision by using a number representation intead of native `timedelta`. (by @MangelMaxime)
"""

let FableSampleExpected :Changelogs = {
    Unreleased = Some {
        ChangelogData.Default with
            Fixed =
                normalizeNewline
                    """#### Python

* Fix #3617: Fix comparaison between list option when one is None
* Fix #3615: Fix remove from dictionary with tuple as key
* Fix #3598: Using obj () now generated an empty dict instead of None
* Fix #3597: Do not translate .toString methods to str
* Fix #3610: Cleanup Python regex handling
* Fix #3628: System.DateTime.Substract not correctly transpiled
"""
    }
    Releases = [
        {
            Version = SemanticVersion.Parse "4.6.0"
            Date = DateTime(2023, 11, 27)
            Data = Some {
            ChangelogData.Default with
                Changed =
                    normalizeNewline """#### All

* Updated .NET metadata to 8.0.100 (by @ncave)
"""
                Added =
                    normalizeNewline """#### All

* Fix #3584: Unit type compiles to undeclared variable (by @ncave)

#### Python

* Support `DateTime(..., DateTimeKind.Utc).ToString("O")` (by @MangelMaxime)

#### Rust

* Added `Guid.TryParse`, `Guid.ToByteArray` (by @ncave)
"""
                Fixed =
                    normalizeNewline """#### Python

* Fixed char to string type regression with binary operator (by @dbrattli)
* Fix `DateTime(..., DateTimeKind.Local).ToString("O")` (by @MangelMaxime)
* Fix calling `value.ToString(CultureInfo.InvariantCulture)` (by @MangelMaxime)
* Fix #3605: Fix record equality comparison to works with optional fields (by @MangelMaxime and @dbrattli)
* PR #3608: Rewrite `time_span.py` allowing for better precision by using a number representation intead of native `timedelta`. (by @MangelMaxime)
"""
        }
        }
    ]
}

let SectionLessSample = normalizeNewlines """# Changelog

## 4.2.1 - 2023-09-29

* Fix package to include Fable libraries folders

## 4.2.0 - 2023-09-29

* Fix #3480: Function decorated with `[<NamedParams>]` without arguments provided should take an empty object
* Fix #3528: Consider functions hidden by a signature file as private (@nojaf)
* Improve error message when Fable doesn't find the `fable-library` folder.

    This is especially useful when working on Fable itself, and should save time to others.
    Each time I got this is error, I needed several minutes to remember the cause of it.
"""

let SectionLessSampleExpected: Changelogs = {
    Unreleased = None
    Releases = [
        {
            Version = SemanticVersion.Parse "4.2.1"
            Date = DateTime(2023, 9, 29)
            Data = Some ChangelogData.Default
        }
        {
            Version = SemanticVersion.Parse "4.2.0"
            Date = DateTime(2023, 9, 29)
            Data = Some ChangelogData.Default
        }
    ] 
}

let fableTests = testList "Fable" [
    runSuccess "Multiple languages" Parser.pChangeLogs FableSample FableSampleExpected
    runSuccess "SectionLess items" Parser.pChangeLogs SectionLessSample SectionLessSampleExpected
]

[<Tests>]
let tests = testList "All" [
    parsingExamples
    changelogDataTest
    fableTests
]

[<EntryPoint>]
let main argv =
    runTestsWithCLIArgs Seq.empty argv tests
