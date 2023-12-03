namespace Ionide.KeepAChangelog

module Domain =
    open SemVersion
    open System

    type Section =
        {
            Items: string list
            SubSections: Map<string, string list>
        }
        static member Default = {
            Items = List.empty
            SubSections = Map.empty 
        }
    
    // TODO: a changelog entry may have a description?
    type ChangelogData =
        { Added: Section
          Changed: Section
          Deprecated: Section
          Removed: Section
          Fixed: Section
          Security: Section
          Custom: Map<string, Section>
          /// Release entries not tied to a section.
          /// This should be avoided in real-world scenarios.
          SectionLessItems: string list }
        static member Default =
            { Added = Section.Default
              Changed = Section.Default
              Deprecated = Section.Default
              Removed = Section.Default
              Fixed = Section.Default
              Security = Section.Default
              Custom = Map.empty
              SectionLessItems = List.empty }

        member this.ToMarkdown () =

            let renderItems (items : string list) =
                items
                |> List.map (fun item ->
                    "* " + item
                )
                |> String.concat Environment.NewLine

            let section name (section: Section) =
                match section.Items with
                | [] -> []
                | items ->
                    $"### {name}"
                    + Environment.NewLine
                    + Environment.NewLine
                    + (renderItems items)
                    + Environment.NewLine
                    |> List.singleton

            String.concat
                Environment.NewLine
                [
                    yield! section "Added" this.Added
                    yield! section "Changed" this.Changed
                    yield! section "Deprecated" this.Deprecated
                    yield! section "Removed" this.Removed
                    yield! section "Fixed" this.Fixed
                    yield! section "Security" this.Security
                    for KeyValue(heading, lines) in this.Custom do
                        yield! section heading lines
                ]

    type Changelogs =
        { Unreleased: ChangelogData option
          Releases: (SemanticVersion * DateTime * ChangelogData option) list }

module Parser =


    open Domain
    open FParsec
    open FParsec.CharParsers
    open FParsec.Primitives
    open System.IO
    open System.Collections.Generic

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

            stream.SkipCharsOrNewlinesUntilString(str, System.Int32.MaxValue, &found)
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
            let firstLine = FParsec.CharParsers.restOfLine true

            let followingLine =
                nextCharSatisfiesNot (fun c -> c = '\n' || c = '-' || c = '*')
                >>. spaces1
                >>. FParsec.CharParsers.restOfLine true

            let rest = opt (many1 (attempt followingLine))

            pipe2 firstLine rest (fun f rest ->
                match rest with
                | None -> f
                | Some parts -> String.concat " " (f :: parts))
            <?> "line item"

        pipe2 bullet content (fun bullet text -> $"{bullet} {text}")

    let pEntriesInASection sectionName =
        let pSubSection: Parser<string * string list> =
            let sectionName =
                skipString "####"
                 >>. spaces1
                 >>. restOfLine true
            
            sectionName
            .>> many1 newline
            .>>. many pEntry
        
        let pEntryOrSubSectionOrNewline =
            choice [
                pSubSection |>> Choice2Of3
                pEntry |>> Choice1Of3
                newline |>> Choice3Of3
            ]
        
        (many pEntryOrSubSectionOrNewline <?> $"{sectionName} entries")
        |>> (fun entries ->
                (entries, Section.Default)
                ||> List.foldBack(fun entry section ->
                    match entry with
                    // Ignore blank line
                    | Choice3Of3 _ -> section
                    | Choice1Of3 item -> { section with Items = item :: section.Items  }
                    | Choice2Of3 (subSectionName, subSectionItems) ->
                        { section with SubSections = Map.add subSectionName subSectionItems section.SubSections  }
                )
        )
    
    let pCustomSection: Parser<string * Section> =
        let sectionName =
            skipString "###"
             >>. spaces1
             >>. restOfLine true // TODO: maybe not the whole line?
             <?> $"custom section header"
        sectionName
        .>>. (pEntriesInASection sectionName)
        .>> attempt (opt newline)

    let pSection sectionName : Parser<Section> =
        (skipString "###"
         >>. spaces1
         >>. skipString sectionName)
        <?> $"{sectionName} section header"
        >>. many1 newline
        >>. (pEntriesInASection sectionName)
        .>> attempt (opt newline)

    let pAdded = pSection "Added"
    let pChanged = pSection "Changed"
    let pRemoved = pSection "Removed"
    let pDeprecated = pSection "Deprecated"
    let pFixed = pSection "Fixed"
    let pSecurity = pSection "Security"
    let pOrEmptyList p = opt (attempt p)
    let pSectionLessItems =
        many1 pEntry
        .>> attempt (opt newline)

    let pSections: Parser<ChangelogData -> ChangelogData> =
        choice [
            attempt (pAdded |>> fun x data -> { data with Added = x })
            attempt (pChanged |>> fun x data -> { data with Changed = x })
            attempt (pRemoved |>> fun x data -> { data with Removed = x })
            attempt (pDeprecated |>> fun x data -> { data with Deprecated = x })
            attempt (pFixed |>> fun x data -> { data with Fixed = x })
            attempt (pSecurity |>> fun x data -> { data with Security = x })
            attempt (many1 pCustomSection |>> fun x data -> { data with Custom = Map.ofList x })
            attempt (pSectionLessItems |>> fun x data -> { data with SectionLessItems =  x })
        ]

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
        let name = attempt (
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
        let pYear =
            pint32
            |> attempt


        let pMonth =
            pint32
            |> attempt

        let pDay =
            pint32
            |> attempt

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
        pipe3
            pHeader
            (attempt (opt unreleased))
            (attempt (many pRelease))
            (fun header unreleased releases ->
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
