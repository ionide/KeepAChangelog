# Changelog

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
