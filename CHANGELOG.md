# Changelog

## [0.3.0-alpha003] - 2025.09.01

### Changed

* [Remove parser, port C# Task, and other modernisation](https://github.com/ionide/KeepAChangelog/pull/30) thanks @tboby
* [Add formatting and Warnings as Errors](https://github.com/ionide/KeepAChangelog/pull/33) thanks @tboby
* [Improve tests, correctly trigger warnings, and fix output markdown format](https://github.com/ionide/KeepAChangelog/pull/32) thanks @tboby

### Fixed

* [Fix bundled dependencies into the package for net6.0](https://github.com/ionide/KeepAChangelog/pull/29) thanks @MangelMaxime 
* [Fix warnings so that they actually trigger!](https://github.com/ionide/KeepAChangelog/pull/31) thanks @tboby



## [0.2.0] - 2023.12.05

### Changed

* [Updated the parser to support arbitrary content under each primary section](https://github.com/ionide/KeepAChangelog/pull/22) (thanks @nojaf!)

## [0.1.8] - 2022.03.31

### Changed

* Minor packaging fix for non-Core MSBuild versions

## [0.1.7] - 2022.03.31

### Changed

* Better packaging of the task to prevent task DLL dependencies from impacting your project's dependencies
* Updated the parser to provide to `ToMarkdown()` member for more general use

## [0.1.6] - 2022.03.31

### Changed

- bump SDK

## [0.1.5] - 2022.03.31

### Fixed

- Embed a deps.json file in the package.

## [0.1.4] - 2022.03.20

### Fixed

- Support supplying package versions for project-to-project references in multitargeting scenarios.


## [0.1.3] - 2022.03.20

### Fixed

- Support supplying package versions for project-to-project references.

## [0.1.2] - 2022.02.13

### Added

- Now supports multiTargeted builds and packs by the addition of the buildMultiTargeting folder. The outer build is hooked at the GenerateNuspec target.

## [0.1.1] - 2022.01.14

### Added

- Now writes an assembly-level AssemblyMetadataAttribute with the key "BuildDate" whose
value is the `YYYY-mm-dd`-formatted date in the release changelog

## [0.1.0] - 2022.01.13

### Added

- Created the package
