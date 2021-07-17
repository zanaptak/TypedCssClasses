// Fable replaces methods from Fable.Core just by fullname so we can avoid
// reference issues by including the methods we need in this assembly

module Fable.Core.JsInterop

let import<'T> (selector: string) (path: string): 'T =
    failwithf "Attempted to use Fable CSS Module import expression in non-Fable context (class:%s, source:%s) [Zanaptak.TypedCssClasses]" selector path
