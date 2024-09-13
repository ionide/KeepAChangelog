module KeepAChangelog.Log

(*
    Log module allowing to define log data for different errors.

    It makes it easier to keep track of the error codes, because everything is in one place.
*)

type LogData =
    {
        ErrorCode: string
        HelpKeyword: string
        Message: string
        MessageArgs: obj array
    }

let changelogFileNotFound (filePath: string) =
    {
        ErrorCode = "IKC0001"
        HelpKeyword = "Missing Changelog file"
        Message = "The Changelog file {0} was not found."
        MessageArgs =
            [|
                box filePath
            |]
    }

let invalidChangelog (filePath: string) (error: string) =
    {
        ErrorCode = "IKC0002"
        HelpKeyword = "Invalid Changelog file"
        Message = "The Changelog file {0} is invalid. The error was: {1}"
        MessageArgs =
            [|
                box filePath
                box error
            |]
    }
