namespace Fable.Reaction

open Fable.Core.JS

module Error =
    let onError(text: string, ex: exn) = console.error (text,ex)