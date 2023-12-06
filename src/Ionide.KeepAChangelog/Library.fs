namespace Ionide.KeepAChangelog

module Domain =
    open SemVersion
    open System

    type ChangelogData =
        {
            Added: string
            Changed: string
            Deprecated: string
            Removed: string
            Fixed: string
            Security: string
            Custom: Map<string, string>
        }

        static member Default =
            {
                Added = String.Empty
                Changed = String.Empty
                Deprecated = String.Empty
                Removed = String.Empty
                Fixed = String.Empty
                Security = String.Empty
                Custom = Map.empty
            }

        member this.ToMarkdown() =
            let section name (body: string) =
                $"### {name}%s{Environment.NewLine}%s{Environment.NewLine}%s{body}"

            String.concat
                Environment.NewLine
                [
                    section "Added" this.Added
                    section "Changed" this.Changed
                    section "Deprecated" this.Deprecated
                    section "Removed" this.Removed
                    section "Fixed" this.Fixed
                    section "Security" this.Security
                    for KeyValue(heading, lines) in this.Custom do
                        section heading lines
                ]

    type Changelogs =
        {
            Unreleased: ChangelogData option
            Releases: (SemanticVersion * DateTime * ChangelogData option) list
        }

module Parser =

    open Domain
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
            let firstLine = CharParsers.restOfLine true

            let followingLine =
                nextCharSatisfiesNot (fun c -> c = '\n' || c = '-' || c = '*')
                >>. spaces1
                >>. CharParsers.restOfLine true

            let rest = opt (many1 (attempt followingLine))

            pipe2
                firstLine
                rest
                (fun f rest ->
                    match rest with
                    | None -> f
                    | Some parts -> String.concat " " (f :: parts)
                )
            <?> "line item"

        pipe2 bullet content (fun bullet text -> $"{bullet} {text}")

    let pSectionBody sectionName : Parser<string> =
        let nextHeader = (newline >>. regex @"[#]{1,3}\s\S" |>> ignore)
        let endOfSection = choice [ eof; nextHeader ]

        manyTill anyChar (lookAhead endOfSection) |>> System.String.Concat
        <?> $"{sectionName} section body"

    let pCustomSection: Parser<string * string> =
        let sectionName =
            skipString "###" >>. spaces1 >>. restOfLine true // TODO: maybe not the whole line?
            <?> $"custom section header"

        sectionName .>> attempt (opt newline) .>>. (pSectionBody sectionName)
        .>> attempt (opt newline)

    let pSection sectionName : Parser<string> =
        ((skipString "###" >>. spaces1 >>. skipString sectionName)
         <?> $"{sectionName} section header")
        >>. many1 newline
        >>. pSectionBody sectionName
        .>> attempt (opt newline)

    let pAdded = pSection "Added"
    let pChanged = pSection "Changed"
    let pRemoved = pSection "Removed"
    let pDeprecated = pSection "Deprecated"
    let pFixed = pSection "Fixed"
    let pSecurity = pSection "Security"
    let pOrEmptyList p = opt (attempt p)
    let pSectionLessItems = many1 pEntry .>> attempt (opt newline)

    let pSections: Parser<ChangelogData -> ChangelogData> =
        choice
            [
                attempt (pAdded |>> fun x data -> { data with Added = x })
                attempt (pChanged |>> fun x data -> { data with Changed = x })
                attempt (pRemoved |>> fun x data -> { data with Removed = x })
                attempt (pDeprecated |>> fun x data -> { data with Deprecated = x })
                attempt (pFixed |>> fun x data -> { data with Fixed = x })
                attempt (pSecurity |>> fun x data -> { data with Security = x })
                attempt (many1 pCustomSection |>> fun x data -> { data with Custom = Map.ofList x })
            ]

    let pData: Parser<ChangelogData, unit> =
        many1 pSections |>> List.fold (fun x f -> f x) ChangelogData.Default

    let pNonStructuredData: Parser<ChangelogData, unit> =
        let nextHeader = (newline >>. regex @"[#]{1,2}\s\S" |>> ignore)
        let endOfSection = choice [ eof; nextHeader ]

        (manyTill anyChar (lookAhead endOfSection) .>> attempt (opt newline))
        |>> (fun _content -> ChangelogData.Default)
        <?> "release body"

    let pHeader: Parser<unit> =
        (skipString "# Changelog" >>. skipNewline .>> skipTillStringOrEof "##")
        <?> "Changelog header"

    let mdUrl inner =
        let linkText = between (pchar '[') (pchar ']') inner

        let linkTarget = between (pchar '(') (pchar ')') (skipMany1Till anyChar (pchar ')'))

        linkText .>> opt linkTarget

    let pUnreleased: Parser<ChangelogData option, unit> =
        let unreleased = skipString "Unreleased"

        let name =
            attempt (
                skipString "##" >>. spaces1 >>. (mdUrl unreleased <|> unreleased)
                .>> skipRestOfLine true
                <?> "Unreleased label"
            )

        name >>. opt (many newline) >>. opt pData <?> "Unreleased version section"

    let validSemverChars =
        [|
            for c in '0' .. '9' -> c
            for c in 'A' .. 'Z' -> c
            for c in 'a' .. 'z' -> c
            yield '-'
            yield '.'
            yield '+'
        |]
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
        let content = choice [ pData; pNonStructuredData ]

        pipe5 vPart middle date (opt (many newline)) (opt content) (fun v _ date _ data -> v, date, data)

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
            (fun _header unreleased releases ->
                {
                    Unreleased = defaultArg unreleased None
                    Releases = releases
                }
            )

    let parseChangeLog (file: FileInfo) =
        match
            runParserOnFile
                pChangeLogs
                ()
                file.FullName
                (System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier = false))
        with
        | ParserResult.Success(result, _, _pos) -> Result.Ok result
        | ParserResult.Failure(msg, structuredError, _pos) -> Result.Error(msg, structuredError)
