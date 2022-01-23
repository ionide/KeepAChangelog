open Expecto

[<Tests>]
let tests = testList "tests" [ ParserTests.tests; TaskTests.tests ]

[<EntryPoint>]
let main argv =
    runTestsWithCLIArgs Seq.empty argv tests
