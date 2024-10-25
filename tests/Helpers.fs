module Ionide.KeepAChangelog.Tasks.Test.Helpers

open System.Runtime.CompilerServices
open Faqt
open Faqt.AssertionHelpers

[<Extension>]
type Assertions =

    /// Asserts that the subject is equal to the specified string when CLRF is replaced with LF in both raw and
    /// escaped forms.
    [<Extension>]
    static member BeLineEndingEquivalent(t: Testable<string>, expected: string, ?because) : And<string> =
        use _ = t.Assert()

        if isNull expected then
            nullArg (nameof expected)

        if isNull t.Subject then
            t.With("Expected", expected).With("But was", t.Subject).Fail(because)

        let expectedNormalised = expected.Replace("\r\n", "\n").Replace("\\r\\n", "\\n")

        let subjectNormalised = t.Subject.Replace("\r\n", "\n").Replace("\\r\\n", "\\n")

        if subjectNormalised <> expectedNormalised then
            t
                .With("Expected", expectedNormalised)
                .With("But was", subjectNormalised)
                .Fail(because)

        And(t)
