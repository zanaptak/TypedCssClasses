module internal Zanaptak.TypedCssClasses.Utils

open System
open System.Globalization
open System.Text.RegularExpressions

let symbolsToUnderscores className = Regex.Replace( className , @"[^a-z0-9A-Z]" , "_" )

/// Converts the string to an array of Int32 code-points (the actual Unicode Code Point number).
let toCodePoints (source : string) : seq<int> =
  let mapper i c =
    // Ignore the low-surrogate because it's already been converted
    if c |> Char.IsLowSurrogate then None
    else Char.ConvertToUtf32 (source, i) |> Some
  source |> Seq.mapi mapper |> Seq.choose id
/// Converts the array of Int32 code-points (the actual Unicode Code Point number) to a string.
let ofCodePoints (source: seq<int>) : string =
  source |> Seq.map Char.ConvertFromUtf32 |> String.concat String.Empty

let replacementChar = Char.ConvertFromUtf32 0xFFFD

let isUnicodeScalar codePoint =
  ( codePoint >= 0 && codePoint <= 0xD7FF ) || ( codePoint >= 0xE000 && codePoint <= 0x10FFFF )

(*
HTML5 -- https://www.w3.org/TR/html5/syntax.html#parsing-html-documents

Any occurrences of any characters in the ranges U+0001 to U+0008, U+000E to U+001F, U+007F to
U+009F, U+FDD0 to U+FDEF, and characters U+000B, U+FFFE, U+FFFF, U+1FFFE, U+1FFFF, U+2FFFE,
U+2FFFF, U+3FFFE, U+3FFFF, U+4FFFE, U+4FFFF, U+5FFFE, U+5FFFF, U+6FFFE, U+6FFFF, U+7FFFE, U+7FFFF,
U+8FFFE, U+8FFFF, U+9FFFE, U+9FFFF, U+AFFFE, U+AFFFF, U+BFFFE, U+BFFFF, U+CFFFE, U+CFFFF, U+DFFFE,
U+DFFFF, U+EFFFE, U+EFFFF, U+FFFFE, U+FFFFF, U+10FFFE, and U+10FFFF are parse errors. These are all
control characters or permanently undefined Unicode characters (noncharacters).

The handling of U+0000 NULL characters varies based on where the characters are found. In general,
they are ignored except where doing so could plausibly introduce an attack vector. This handling
is, by necessity, spread across both the tokenization stage and the tree construction stage.
*)

let isValidHtml codePoint =
  not (
    codePoint <= 0x8
      || ( codePoint >= 0xE && codePoint <= 0x1F )
      || ( codePoint >= 0x7F && codePoint <= 0x9F )
      || ( codePoint >= 0xFDD0 && codePoint <= 0xFDEF )
      || codePoint = 0xB
      || ( codePoint &&& 0xFFFE = 0xFFFE ) // also catches 0xFFFF bits
  )

// Since output will be in HTML attribute, replace any invalid HTML characters.
// (Don't skip; prefer to expose problems than hide them.)
let replaceNonHtml ( text : string ) =
  text
  |> toCodePoints
  |> Seq.map ( fun cp -> if isValidHtml cp then Char.ConvertFromUtf32 cp else replacementChar )
  |> String.concat ""

// Escape for use in xmldoc
let escapeHtml ( text : string ) =
  text
  |> toCodePoints
  |> Seq.map ( fun cp ->
    match cp with
    | 0x26 -> "&amp;"
    | 0x3C -> "&lt;"
    | 0x3E -> "&gt;"
    | 0x22 -> "&quot;"
    | 0x27 -> "&#x27;" // apostrophe
    | 0x2F -> "&#x2F;" // slash (additional XSS close-tag protection)
    | cp when isValidHtml cp -> Char.ConvertFromUtf32 cp
    | _ -> replacementChar
  )
  |> String.concat ""

let unescapeHexStr hexStr =
  match Int32.TryParse( hexStr , NumberStyles.HexNumber , CultureInfo.InvariantCulture ) with
  | true , codePoint when isUnicodeScalar codePoint -> Char.ConvertFromUtf32 codePoint
  | _ -> replacementChar

// Capture selector text from first non-whitespace up to first {
// Then capture content inside the outer {} block (handles nested {} with .NET balancing groups regex feature)
let selectorsAndBlockCapture = @"
  \s*
  (?<selectors>
    ( \\ . | [^{\s] )  (?# start with any escaped char or non-open-brace non-space char )
    ( \\ . | [^{] )*  (?# capture all escaped chars or non-open-brace chars )
  )
  ( (?<!\\)\{ | (?<=(?<!\\)(\\\\)+)\{ )  (?# non-escaped `{`: 0 or even number of preceding backslashes )
  (?<blockcontent>
    (?>
      ( (?<!\\)\{ | (?<=(?<!\\)(\\\\)+)\{ ) (?<c>)  (?# non-escaped `{`, increment brace counter )
      |
      ( \\ . | [^{}] )+  (?# any escaped chars or non-brace chars )
      |
      ( (?<!\\)\} | (?<=(?<!\\)(\\\\)+)\} ) (?<-c>)  (?# non-escaped `}`, decrement brace counter )
    )*
    (?(c)(?!))  (?# require brace counter of 0 )
  )
  ( (?<!\\)\} | (?<=(?<!\\)(\\\\)+)\} ) (?# non-escaped `}`: 0 or even number of preceding backslashes )
"

let classCapture = @"
  ( (?<!\\)\. | (?<=(?<!\\)(\\\\)+)\. ) (?# non-escaped period: 0 or even number of preceding backslashes )
  (?<class>
    ( \\ . | [^\s\\[\](){}|:,.+>~] )+ (?# capture all escaped chars or non-delimeters )
  )
"

let selectorsAndBlocks text =
  Regex.Matches(
    text
    , selectorsAndBlockCapture
    , RegexOptions.IgnorePatternWhitespace ||| RegexOptions.ExplicitCapture
    , TimeSpan.FromSeconds 5.
  )
  |> Seq.cast
  |> Seq.map ( fun ( m : Match ) -> m.Groups.[ "selectors" ].Value , m.Groups.[ "blockcontent" ].Value )

let rawClasses text =
  Regex.Matches(
    text
    , classCapture
    , RegexOptions.IgnorePatternWhitespace ||| RegexOptions.ExplicitCapture
    , TimeSpan.FromSeconds 5.
  )
  |> Seq.cast
  |> Seq.map ( fun ( m : Match ) ->
    m.Groups.[ "class" ].Value
    |> fun t ->
      Regex.Replace(
        t
        , @"\\([0-9a-fA-F]{1,6}|.)"
        , MatchEvaluator( fun m ->
            let escapeStr = m.Groups.[ 1 ].Value
            if Regex.IsMatch( escapeStr , @"^[0-9a-fA-F]+$" ) then
              unescapeHexStr escapeStr
            else
              escapeStr
          )
      )
    |> replaceNonHtml
  )

let parseCss text transformer =
  text
  |> fun t -> Regex.Replace( t , @"\s*/\*([^*]|\*(?!/))*\*/\s*" , "" , RegexOptions.None , TimeSpan.FromSeconds 5. )
  |> selectorsAndBlocks
  |> Seq.collect ( fun ( selectors , blockContent ) ->
    if selectors.Trim().StartsWith( @"@media" ) then
      blockContent
      |> selectorsAndBlocks
      |> Seq.map fst
    else
      Seq.singleton selectors
  )
  |> Seq.collect rawClasses
  |> Seq.distinct
  |> Seq.map ( fun s ->
    {| Property = transformer s ; Value = s |}
  )
  |> Seq.distinctBy ( fun p -> p.Property )

