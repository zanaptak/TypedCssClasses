// Fable replaces methods from Fable.Core just by fullname so we can avoid
// reference issues by including the methods we need in this assembly

module Fable.Core.JsInterop

let import<'T> (selector: string) (path: string): 'T = failwith "js native"