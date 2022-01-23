namespace Ionide.KeepAChangelog

open SemVersion
open System

type ChangelogData =
    { Added: string list
      Changed: string list
      Deprecated: string list
      Removed: string list
      Fixed: string list
      Security: string list
      Custom: Map<string, string list> }
    static member Default =
        { Added = []
          Changed = []
          Deprecated = []
          Removed = []
          Fixed = []
          Security = []
          Custom = Map.empty }

type Changelogs =
    { Unreleased: ChangelogData option
      Releases: (SemanticVersion * DateTime * ChangelogData option) list }

module Promote =
    open SemVersion

    type private SemVerBump =
        | Major
        | Minor
        | Patch

    let inline (|NonEmpty|_|) xs =
        match xs with
        | [] -> None
        | _ -> Some()

    /// the prerelease segment is just a count of the individual changes
    let revisionNumber (c: ChangelogData) =
        c.Added.Length
        + c.Changed.Length
        + c.Deprecated.Length
        + c.Fixed.Length
        + c.Removed.Length
        + c.Security.Length
        + (c.Custom.Values |> Seq.sumBy (fun l -> l.Length))

    // TODO: expand this logic later to allow for customization of the bump types based on change content?
    let private determineBump (c: ChangelogData) : SemVerBump =
        match c.Removed, c.Added with
        | NonEmpty, _ -> Major
        | _, NonEmpty -> Minor
        | _, _ -> Patch

    /// for unreleased changes, bump the version an assign a prerelease part based on the number of changes
    let private bumpVersion (ver: SemanticVersion) bumpType revisions =
        let prereleasePart = $"beta.{revisions}"

        match bumpType with
        | Major -> SemanticVersion(ver.Major.Value + 1, Nullable(), Nullable(), prerelease = prereleasePart)
        | Minor -> SemanticVersion(ver.Major, ver.Minor.Value + 1, Nullable(), prerelease = prereleasePart)
        | Patch -> SemanticVersion(ver.Major, ver.Minor, ver.Patch.Value + 1, prerelease = prereleasePart)

    /// <summary>given a changelog, determine the version to promote to from the unreleased changes, if any.</summary>
    /// <remarks>
    /// The version bump algoritm is as follows:
    ///   * removals require a major bump
    ///   * additions require a minor bump
    ///   * all other changes require a patch bump
    /// </remarks>
    let fromUnreleased (c: Changelogs) =
        c.Unreleased
        |> Option.bind (fun unreleased ->
            match c.Releases |> List.tryHead with
            | None -> None
            | Some (lastVersion, _, releaseData) ->
                let bumpType =
                    releaseData
                    |> Option.map determineBump
                    |> Option.defaultValue Patch

                let numberOfRevisions = revisionNumber unreleased
                let newVersion = bumpVersion lastVersion bumpType numberOfRevisions
                Some(newVersion, DateTime.Today, Some unreleased))


module Parser =
    open FParsec
    open System.IO

    type Parser<'t> = Parser<'t, unit>

    let pipe6 p1 p2 p3 p4 p5 p6 fn =
        parse {
            let! a = p1
            let! b = p2
            let! c = p3
            let! d = p4
            let! e = p5
            let! f = p6
            return fn a b c d e f
        }

    let pipe7 p1 p2 p3 p4 p5 p6 p7 fn =
        parse {
            let! a = p1
            let! b = p2
            let! c = p3
            let! d = p4
            let! e = p5
            let! f = p6
            let! g = p7
            return fn a b c d e f g
        }

    let skipTillStringOrEof str : Parser<unit, _> =
        fun stream ->
            let mutable found = false

            stream.SkipCharsOrNewlinesUntilString(str, Int32.MaxValue, &found)
            |> ignore

            Reply(())

    let pBullet: Parser<char> = (attempt (pchar '-') <|> pchar '*') <?> "bullet"

    let pEntry: Parser<string> =
        let bullet = attempt (pBullet .>> spaces1)

        let content =
            // we need to parse all of this line, sure
            // but we also need to keep parsing next lines until
            // * we find a bullet, or
            // * we get an empty line
            let firstLine = restOfLine true

            let followingLine =
                nextCharSatisfiesNot (fun c -> c = '\n' || c = '-' || c = '*')
                >>. spaces1
                >>. restOfLine true

            let rest = opt (many1 (attempt followingLine))

            pipe2 firstLine rest (fun f rest ->
                match rest with
                | None -> f
                | Some parts -> String.concat " " (f :: parts))
            <?> "line item"

        pipe2 bullet content (fun bullet text -> $"{bullet} {text}")

    let pCustomSection: Parser<string * string list> =
        let sectionName =
            skipString "###" >>. spaces1 >>. restOfLine true // TODO: maybe not the whole line?
            <?> $"custom section header"

        sectionName
        .>>. (many pEntry <?> $"{sectionName} entries")
        .>> attempt (opt newline)

    let pSection sectionName : Parser<string list> =
        (skipString "###"
         >>. spaces1
         >>. skipString sectionName)
        <?> $"{sectionName} section header"
        >>. many1 newline
        >>. (many pEntry <?> $"{sectionName} entries")
        .>> attempt (opt newline)

    let pAdded = pSection "Added"
    let pChanged = pSection "Changed"
    let pRemoved = pSection "Removed"
    let pDeprecated = pSection "Deprecated"
    let pFixed = pSection "Fixed"
    let pSecurity = pSection "Security"
    let pOrEmptyList p = opt (attempt p)

    let pSections: Parser<ChangelogData -> ChangelogData> =
        choice [ attempt (pAdded |>> fun x data -> { data with Added = x })
                 attempt (
                     pChanged
                     |>> fun x data -> { data with Changed = x }
                 )
                 attempt (
                     pRemoved
                     |>> fun x data -> { data with Removed = x }
                 )
                 attempt (
                     pDeprecated
                     |>> fun x data -> { data with Deprecated = x }
                 )
                 attempt (pFixed |>> fun x data -> { data with Fixed = x })
                 attempt (
                     pSecurity
                     |>> fun x data -> { data with Security = x }
                 )
                 attempt (
                     many1 pCustomSection
                     |>> fun x data -> { data with Custom = Map.ofList x }
                 ) ]

    let pData: Parser<ChangelogData, unit> =
        many1 pSections
        |>> List.fold (fun x f -> f x) ChangelogData.Default

    let pHeader: Parser<unit> =
        (skipString "# Changelog" >>. skipNewline
         .>> skipTillStringOrEof "##")
        <?> "Changelog header"

    let mdUrl inner =
        let linkText = between (pchar '[') (pchar ']') inner

        let linkTarget = between (pchar '(') (pchar ')') (skipMany1Till anyChar (pchar ')'))

        linkText .>> opt linkTarget

    let pUnreleased: Parser<ChangelogData option, unit> =
        let unreleased = skipString "Unreleased"

        let name =
            attempt (
                skipString "##"
                >>. spaces1
                >>. (mdUrl unreleased <|> unreleased)
                .>> skipRestOfLine true
                <?> "Unreleased label"
            )

        name >>. opt (many newline) >>. opt pData
        <?> "Unreleased version section"

    let validSemverChars =
        [| for c in '0' .. '9' -> c
           for c in 'A' .. 'Z' -> c
           for c in 'a' .. 'z' -> c
           yield '-'
           yield '.'
           yield '+' |]
        |> Set.ofArray

    let pSemver: Parser<_> =
        many1Chars (satisfy validSemverChars.Contains)
        |>> fun text -> SemVersion.SemanticVersion.Parse text

    let pDate: Parser<_> =
        let pYear = pint32 |> attempt


        let pMonth = pint32 |> attempt

        let pDay = pint32 |> attempt

        let ymdDashes =
            let dash = pchar '-'
            pipe5 pYear dash pMonth dash pDay (fun y _ m _ d -> System.DateTime(y, m, d))


        let dmyDots =
            let dot = pchar '.'
            pipe5 pDay dot pMonth dot pYear (fun d _ m _ y -> System.DateTime(y, m, d))

        attempt dmyDots <|> ymdDashes


    let pVersion = mdUrl pSemver <|> pSemver

    let pRelease: Parser<SemVersion.SemanticVersion * System.DateTime * ChangelogData option> =
        let vPart = skipString "##" >>. spaces1 >>. pVersion
        let middle = spaces1 .>> pchar '-' .>> spaces1
        let date = pDate .>> skipRestOfLine true

        pipe5 vPart middle date (opt (many newline)) (opt pData) (fun v _ date _ data -> v, date, data)

    let pChangeLogs: Parser<Changelogs, unit> =
        let unreleased =
            pUnreleased
            |>> fun unreleased ->
                    match unreleased with
                    | None -> None
                    | Some u when u = ChangelogData.Default -> None
                    | Some unreleased -> Some unreleased

        pipe3 pHeader (attempt (opt unreleased)) (attempt (many pRelease)) (fun header unreleased releases ->
            { Unreleased = defaultArg unreleased None
              Releases = releases })

    let parseChangeLog (file: FileInfo) =
        match
            runParserOnFile
                pChangeLogs
                ()
                file.FullName
                (System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier = false))
            with
        | ParserResult.Success (result, _, pos) -> Result.Ok result
        | ParserResult.Failure (msg, structuredError, pos) -> Result.Error(msg, structuredError)
