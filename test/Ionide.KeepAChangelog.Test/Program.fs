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
                Added = ["- A"]
                Changed = ["- B"]
                Removed = ["- C"]
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
            SemanticVersion.Parse("0.3.0"), DateTime(2015, 12, 03), Some { ChangelogData.Default with Added = ["- A";"- B";"- C"]}
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

let sample = """# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.3.1] - 8.1.2022

### Added

* 

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
        SemanticVersion.Parse "0.3.1", DateTime(2022, 1, 8), Some { ChangelogData.Default with Added = ["- Add XmlDocs to the generated package"] }
        SemanticVersion.Parse "0.3.0", DateTime(2021, 11, 23), Some { ChangelogData.Default with Added = ["- Expose client `CodeAction` caps as CodeActionClientCapabilities. (by @razzmatazz)"; "- Map CodeAction.IsPreferred & CodeAction.Disabled props. (by @razzmatazz)"] }
        SemanticVersion.Parse "0.2.0", DateTime(2021, 11, 17), Some { ChangelogData.Default with Added = ["- Add support for `codeAction/resolve` (by @razzmatazz)"] }
        SemanticVersion.Parse "0.1.1", DateTime(2021, 11, 15), Some { ChangelogData.Default with Added = ["- Initial implementation"] }
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

let tests = testList "parsing examples" [
    runSuccess "line entry" Parser.pEntry "- A" "- A"
    runSuccess "header" Parser.pHeader header ()
    runSuccess "unreleased" Parser.pUnreleased emptyUnreleased None
    runSuccess "header and unreleased" (Parser.pHeader >>. Parser.pUnreleased) headerAndUnreleased None
    runSuccess "release" Parser.pRelease singleRelease singleReleaseExpected 
    runSuccess
        "header and unreleased and released"
        (Parser.pHeader >>. Parser.pUnreleased
         .>>. Parser.pRelease)
        headerAndUnreleasedAndRelease
        headerAndUnreleasedAndReleaseExpected

    runSuccess "keepachangelog" Parser.pChangeLogs keepAChangelog keepAChangelogExpected

    runSuccess "lsp changelog" Parser.pChangeLogs sample sampleExpected
]

[<EntryPoint>]
let main argv = 
    runTestsWithCLIArgs Seq.empty argv tests