open Ionide.KeepAChangelog

open System
open SemVersion
open Expecto
open Ionide.KeepAChangelog.Domain

let singleRelease =
    """## [1.0.0] - 2017-06-20
### Added
- A

### Changed
- B

### Removed
- C

"""

let singleReleaseExpected =
    (SemanticVersion.Parse "1.0.0", DateTime(2017, 06, 20), Some {
            ChangelogData.Default with
                Added = { Section.Default with Items = ["- A"] }
                Changed = { Section.Default with Items = ["- B"] }
                Removed = { Section.Default with Items = ["- C"] }
            })

let keepAChangelog =
    """# Changelog
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
            SemanticVersion.Parse("0.3.0"), DateTime(2015, 12, 03), Some { ChangelogData.Default with Added = { Section.Default with Items =["- A";"- B";"- C"] } }
        ]
    }

let header =
    """# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

"""

let emptyUnreleased =
    """## [Unreleased]

"""

let headerAndUnreleased = header + emptyUnreleased

let headerAndUnreleasedAndRelease = header + emptyUnreleased + singleRelease
let headerAndUnreleasedAndReleaseExpected = None, singleReleaseExpected

let sample1Release = """## [0.3.1] - 8.1.2022

### Added

- Add XmlDocs to the generated package

"""

let sample1ReleaseExpected =
    SemanticVersion.Parse "0.3.1", DateTime(2022, 1, 8), Some { ChangelogData.Default with Added = { Section.Default with Items = ["- Add XmlDocs to the generated package"] } }

let sample = """# Changelog
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
        SemanticVersion.Parse "0.3.1", DateTime(2022, 1, 8), Some { ChangelogData.Default with Added = { Section.Default with Items = ["* Add XmlDocs to the generated package"] } }
        SemanticVersion.Parse "0.3.0", DateTime(2021, 11, 23), Some { ChangelogData.Default with Added = { Section.Default with Items = ["* Expose client `CodeAction` caps as CodeActionClientCapabilities. (by @razzmatazz)"; "* Map CodeAction.IsPreferred & CodeAction.Disabled props. (by @razzmatazz)"] } }
        SemanticVersion.Parse "0.2.0", DateTime(2021, 11, 17), Some { ChangelogData.Default with Added = { Section.Default with Items = ["* Add support for `codeAction/resolve` (by @razzmatazz)"] } }
        SemanticVersion.Parse "0.1.1", DateTime(2021, 11, 15), Some { ChangelogData.Default with Added = { Section.Default with Items = ["* Initial implementation"] } }
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
                Added = { Section.Default with Items = [ "Added line 1"; "Added line 2" ] }
                Changed = { Section.Default with Items = [ "Changed line 1"; "Changed line 2" ] }
                Deprecated = { Section.Default with Items = [ "Deprecated line 1"; "Deprecated line 2" ] }
                Removed = { Section.Default with Items = [ "Removed line 1"; "Removed line 2" ] }
                Fixed = { Section.Default with Items = [ "Fixed line 1"; "Fixed line 2" ] }
                Security = { Section.Default with Items = [ "Security line 1"; "Security line 2" ] }
                Custom =
                    [
                        "CustomHeaderA", { Section.Default with Items = [ "Custom line 1"; "Custom line 2" ] }
                        "CustomHeaderB", { Section.Default with Items = [ "Custom line 3"; "Custom line 4" ] }
                    ]
                    |> Map.ofList
            }

        printfn "%A" (changelogData.ToMarkdown())

        let expected =
            """### Added

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

        Expect.equal (changelogData.ToMarkdown()) expected "Should have produced expected value"
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
            Fixed = {
                Items =  []
                SubSections = [
                        "Python", [
                            "* Fix #3617: Fix comparaison between list option when one is None"
                            "* Fix #3615: Fix remove from dictionary with tuple as key"
                            "* Fix #3598: Using obj () now generated an empty dict instead of None"
                            "* Fix #3597: Do not translate .toString methods to str"
                            "* Fix #3610: Cleanup Python regex handling"
                            "* Fix #3628: System.DateTime.Substract not correctly transpiled"
                        ]
                    ]
                    |> Map.ofList
            }
    }
    Releases = [
        SemanticVersion.Parse "4.6.0",
        DateTime(2023, 11, 27),
        Some {
            ChangelogData.Default with
                Changed = {
                    Section.Default with
                        SubSections =[
                            "All", [
                                "* Updated .NET metadata to 8.0.100 (by @ncave)"
                            ]
                        ] |> Map.ofList
                }
                Added = {
                    Section.Default with
                        SubSections = [
                            "All", [
                                "* Fix #3584: Unit type compiles to undeclared variable (by @ncave)"
                            ]
                            "Python", [
                                "* Support `DateTime(..., DateTimeKind.Utc).ToString(\"O\")` (by @MangelMaxime)"
                            ]
                            "Rust", [
                                "* Added `Guid.TryParse`, `Guid.ToByteArray` (by @ncave)"
                            ]
                        ] |> Map.ofList
                }
                Fixed = {
                    Section.Default with
                        SubSections = [
                            "Python", [
                                "* Fixed char to string type regression with binary operator (by @dbrattli)"
                                "* Fix `DateTime(..., DateTimeKind.Local).ToString(\"O\")` (by @MangelMaxime)"
                                "* Fix calling `value.ToString(CultureInfo.InvariantCulture)` (by @MangelMaxime)"
                                "* Fix #3605: Fix record equality comparison to works with optional fields (by @MangelMaxime and @dbrattli)"
                                "* PR #3608: Rewrite `time_span.py` allowing for better precision by using a number representation intead of native `timedelta`. (by @MangelMaxime)"
                            ]
                        ] |> Map.ofList
                }
        }
    ]
}

let subSectionTests = testList "subsections" [
    runSuccess "Fable example" Parser.pChangeLogs FableSample FableSampleExpected
]

[<Tests>]
let tests = testList "All" [
    parsingExamples
    changelogDataTest
]

[<EntryPoint>]
let main argv =
    runTestsWithCLIArgs Seq.empty argv tests
