module internal Zanaptak.TypedCssClasses.Utils

open System
open System.Globalization
open System.Text.RegularExpressions
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Web
open Zanaptak.TypedCssClasses.Internal.ProviderImplementation.ProvidedTypes
open Zanaptak.TypedCssClasses.Internal.FSharpData.ProvidedTypes
open Zanaptak.TypedCssClasses.Internal.FSharpData.Helpers
open FSharp.Quotations

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

// https://github.com/dotnet/fsharp/blob/master/src/fsharp/PrettyNaming.fs
/// The characters that are allowed to be the first character of an identifier.
let IsIdentifierFirstCharacter c =
    if c = '_' then true
    else
        match Char.GetUnicodeCategory c with
        // Letters
        | UnicodeCategory.UppercaseLetter
        | UnicodeCategory.LowercaseLetter
        | UnicodeCategory.TitlecaseLetter
        | UnicodeCategory.ModifierLetter
        | UnicodeCategory.OtherLetter
        | UnicodeCategory.LetterNumber -> true
        | _ -> false

// https://github.com/dotnet/fsharp/blob/master/src/fsharp/PrettyNaming.fs
/// The characters that are allowed to be in an identifier.
let IsIdentifierPartCharacter c =
    if c = '\'' then true   // Tick
    else
        match Char.GetUnicodeCategory c with
        // Letters
        | UnicodeCategory.UppercaseLetter
        | UnicodeCategory.LowercaseLetter
        | UnicodeCategory.TitlecaseLetter
        | UnicodeCategory.ModifierLetter
        | UnicodeCategory.OtherLetter
        | UnicodeCategory.LetterNumber
        // Numbers
        | UnicodeCategory.DecimalDigitNumber
        // Connectors
        | UnicodeCategory.ConnectorPunctuation // includes '_'
        // Combiners
        | UnicodeCategory.NonSpacingMark
        | UnicodeCategory.SpacingCombiningMark -> true
        | _ -> false

// https://github.com/dotnet/fsharp/blob/master/src/fsharp/lexhelp.fs
let keywords =
    [
        "abstract" ; "and" ; "as" ; "assert" ; "asr" ; "base" ; "begin" ; "class" ; "const" ; "default" ; "delegate" ; "do"
        "done" ; "downcast" ; "downto" ; "elif" ; "else" ; "end" ; "exception" ; "extern" ; "false" ; "finally" ; "fixed"
        "for" ; "fun" ; "function" ; "global" ; "if" ; "in" ; "inherit" ; "inline" ; "interface" ; "internal" ; "land"
        "lazy" ; "let" ; "lor" ; "lsl" ; "lsr" ; "lxor" ; "match" ; "member" ; "mod" ; "module" ; "mutable" ; "namespace"
        "new" ; "null" ; "of" ; "open" ; "or" ; "override" ; "private" ; "public" ; "rec" ; "return" ; "sig" ; "static"
        "struct" ; "then" ; "to" ; "true" ; "try" ; "type" ; "upcast" ; "use" ; "val" ; "void" ; "when" ; "while" ; "with"
        "yield" ; "_" ; "__token_OBLOCKSEP" ; "__token_OWITH" ; "__token_ODECLEND" ; "__token_OTHEN" ; "__token_OELSE"
        "__token_OEND" ; "__token_ODO" ; "__token_OLET" ; "__token_constraint" ; "break" ; "checked" ; "component"
        "constraint" ; "continue" ; "fori" ;  "include" ;  "mixin" ; "parallel" ; "params" ;  "process" ; "protected"
        "pure" ; "sealed" ; "trait" ;  "tailcall" ; "virtual" ; "__SOURCE_DIRECTORY__" ; "__SOURCE_FILE__" ; "__LINE__"
    ]
    |> Set.ofList
let isKeyword s = keywords |> Set.contains s

let symbolsToUnderscores s =
    // Process as codepoints so that surrogate pairs are handled as 1 character
    s
    |> toCodePoints
    |> Seq.mapi ( fun i cp ->
        if cp > 0xFFFF then [| '_' |] // replace supplementary char
        else
            let ch = char cp
            // Replace symbols even if valid identifier chars, for consistency (visual and editor selection/cursor behavior)
            if ch = '\'' then [| '_' |] // replace despite valid identifier char
            elif Char.GetUnicodeCategory ch = UnicodeCategory.ConnectorPunctuation then [| '_' |] // replace despite valid identifier char
            elif IsIdentifierPartCharacter ch then
                if i > 0 then [| ch |] // interior position, valid interior char in interior position
                elif IsIdentifierFirstCharacter ch then [| ch |] // first position, valid first char
                else [| '_' ; ch |] // fisrt position, valid interior char but not valid first char, prefix with _
            else [| '_' |] // replace all other chars
    )
    |> Seq.toArray
    |> Array.collect id
    |> String
    |> fun s -> if isKeyword s then s + "_" else s // suffix with _ if final identifier is a keyword

// Insert underscores at word boundaries inferred from case changes
let wordCaseBoundaries s =
    s
    |> fun s -> Regex.Replace ( s , @"(\p{Lu})(\p{Ll})" , "_$1$2" , RegexOptions.None , TimeSpan.FromSeconds 10. ) // upper then lower
    |> fun s -> Regex.Replace ( s , @"([^_\p{Lu}])([\p{Lu}])" , "$1_$2" , RegexOptions.None , TimeSpan.FromSeconds 10. ) // upper after non upper
    |> fun s -> Regex.Replace ( s , @"(\p{Nd})([\p{Lu}\p{Ll}])" , "$1_$2" , RegexOptions.None , TimeSpan.FromSeconds 10. ) // upper/lower after digit

let capitalize ( s : string ) = s.[ 0 ].ToString().ToUpperInvariant() + s.[ 1 .. ].ToLowerInvariant()

type Case = Pascal | Camel

// convert to mixed case, based on word boundaries identified by symbols and case changes
let toCase ( case : Case ) ( s : string ) =
    s
    |> symbolsToUnderscores
    |> wordCaseBoundaries
    |> fun s -> s.Split( [| '_' |] , StringSplitOptions.RemoveEmptyEntries )
    |> Array.mapi ( fun i s -> if i > 0 || case = Pascal then capitalize s else s.ToLowerInvariant() )
    |> String.concat ""
    |> fun s -> if String.IsNullOrWhiteSpace s then "__" else s
    |> fun s -> if IsIdentifierFirstCharacter s.[ 0 ] then s else "_" + s // first position not valid, prefix with _
    |> fun s -> if isKeyword s then s + "_" else s // suffix with _ if final identifier is a keyword

let toPascalCase ( s : string ) = toCase Pascal s
let toCamelCase ( s : string ) = toCase Camel s

// http://fssnip.net/bj
let levenshtein ( word1 : string ) ( word2 : string ) =
    let chars1, chars2 = word1.ToCharArray(), word2.ToCharArray()
    let m, n = chars1.Length, chars2.Length
    let table : int[,] = Array2D.zeroCreate (m + 1) (n + 1)
    for i in 0..m do
        for j in 0..n do
            match i, j with
            | i, 0 -> table.[i, j] <- i * 10000
            | 0, j -> table.[i, j] <- j * 10000
            | _, _ ->
                let delete = table.[i-1, j] + 10000
                let insert = table.[i, j-1] + 10000 // ins/del highest cost, changes length
                let substitute =
                    if chars1.[i - 1] = chars2.[j - 1] then
                        table.[i-1, j-1] // same character no cost
                    elif Char.ToUpperInvariant( chars1.[i - 1] ) = Char.ToUpperInvariant( chars2.[j - 1] ) then
                        table.[i-1, j-1] + 1 // case change lowest cost
                    else
                        table.[i-1, j-1] + 100 // substitution cost less than ins/del, keeps same length
                table.[i, j] <- List.min [delete; insert; substitute]
    table.[m, n] //, table

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

let rec selectorPreludesFromCss text =
    Regex.Matches(
        text
        , selectorsAndBlockCapture
        , RegexOptions.IgnorePatternWhitespace ||| RegexOptions.ExplicitCapture
        , TimeSpan.FromSeconds 10.
    )
    |> Seq.cast
    |> Seq.map ( fun ( m : Match ) -> m.Groups.[ "selectors" ].Value.Trim() , m.Groups.[ "blockcontent" ].Value )
    |> Seq.collect ( fun ( selectors , blockContent ) ->
        if selectors.StartsWith( "@media" ) || selectors.StartsWith( "@supports" ) then
            selectorPreludesFromCss blockContent
        else
            Seq.singleton selectors
    )

let classNamesFromSelectorText text =
    Regex.Matches(
        text
        , classCapture
        , RegexOptions.IgnorePatternWhitespace ||| RegexOptions.ExplicitCapture
        , TimeSpan.FromSeconds 10.
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
                , RegexOptions.None
                , TimeSpan.FromSeconds 10.
            )
        |> replaceNonHtml
    )

let classNamesFromCss text =
    text
    |> selectorPreludesFromCss
    |> Seq.collect classNamesFromSelectorText
    |> Seq.distinct

let basicSuffixGenerator () =
    let nameSet = new HashSet<_>()
    fun ( baseName : string ) ->
        let mutable name = baseName
        let mutable trySuffix = 1
        while nameSet.Contains name do
            trySuffix <- trySuffix + 1
            name <- baseName + "_" + string trySuffix
        nameSet.Add name |> ignore
        name

type ExtendedSuffixGenerator () =
    let nameSet = new HashSet<_>()

    member this.Single ( baseName : string ) =
        let mutable name = baseName
        let mutable trySuffix = ""
        while nameSet.Contains name do
            // shouldn't happen, but just in case
            if trySuffix = "" then
                trySuffix <- "__1_of_1"
            else
                trySuffix <- "_" + trySuffix
            name <- baseName + trySuffix
        nameSet.Add name |> ignore
        name

    member this.Multiple ( baseName : string ) count =
        let mutable leadingUnderscores = "__"
        let digits = ( float count |> Math.Log10 |> int ) + 1
        let numStr num = ( string num ).PadLeft( digits , '0' )
        let nameArray underscores = Array.init count ( fun i -> sprintf "%s%s%s_of_%s" baseName underscores ( numStr ( i + 1 ) ) ( numStr count ) )
        let mutable names = nameArray leadingUnderscores
        while names |> Array.exists ( fun name -> nameSet.Contains name ) do
            // If any in a group match an existing name, try again with additional underscore
            leadingUnderscores <- leadingUnderscores + "_"
            names <- nameArray leadingUnderscores
        names |> Array.iter ( fun name -> nameSet.Add name |> ignore )
        names

let getPropertiesFromClassNames naming nameCollisions classNames =
    let transformer =
        match naming with
        | Naming.Underscores -> symbolsToUnderscores
        | Naming.CamelCase -> toCamelCase
        | Naming.PascalCase -> toPascalCase
        | _ -> id

    let initialProperties =
        classNames
        |> Array.map ( fun s -> { Name = transformer s ; Value = s } )
        |> Array.filter ( fun p -> not ( p.Name.Contains( "``" ) ) ) // impossible to represent as verbatim property name, user will have to use string value

    match nameCollisions with

    | NameCollisions.Omit ->
        initialProperties
        |> Array.groupBy ( fun p -> p.Name )
        |> Array.filter ( fun ( _ , props ) -> props.Length = 1 )
        |> Array.collect snd

    | NameCollisions.ExtendedSuffix ->
        let nameGen = ExtendedSuffixGenerator()
        initialProperties
        |> Array.groupBy ( fun p -> p.Name )
        |> Array.sortBy ( fun ( propName , props ) -> props.Length , propName )
        |> Array.collect ( fun ( propName , props ) ->
            if Array.length props = 1 then
                [| { props.[ 0 ] with Name = nameGen.Single propName } |]
            else
                let sorted = props |> Array.sortBy ( fun p -> levenshtein propName p.Value , p.Value )
                let names = nameGen.Multiple propName props.Length
                Array.zip sorted names
                |> Array.map ( fun ( p , name ) -> { p with Name = name } )
        )

    | _ ->
        let nameGen = basicSuffixGenerator ()
        // A name that exactly matches raw value is exempt from conflict resolution, reserve unique name immediately.
        // register unique base names in case later suffixed name conflicts
        // e.g. xyz_2 followed by group of xyz, xyz, xyz, they need to know xyz_2 is already reserved
        let sameNames , differentNames = initialProperties |> Array.partition ( fun p -> p.Name = p.Value )
        let sameNamesFinal = sameNames |> Array.map ( fun p -> { p with Name = nameGen p.Name } )
        let differentNamesFinal =
            differentNames
            |> Array.groupBy ( fun p -> p.Name )
            |> Array.collect ( fun ( propName , props ) ->
                if Array.length props = 1 then
                    [| { props.[ 0 ] with Name = nameGen propName } |]
                else
                    // entries with same property name, closest to underying text value gets first chance at unique base name, others get numbered suffix
                    props
                    |> Array.sortBy ( fun p -> levenshtein propName p.Value , p.Value )
                    |> Array.map ( fun p -> { p with Name = nameGen propName } )
            )
        Array.append sameNamesFinal differentNamesFinal

let tryParseCssClassNames text =

    // Remove comments
    let text = Regex.Replace( text , @"\s*/\*([^*]|\*(?!/))*\*/\s*" , "" , RegexOptions.None , TimeSpan.FromSeconds 10. )

    // Check for existence of one declaration block to indicate valid css.
    // Used when file open fails and we are subsequently checking if file-like string is actually inline CSS.
    // If no declarations found, we can assume it was in fact an incorrectly-specified file and report meaningful error.
    let cssContainsBlock =
        Regex.IsMatch(
            text
            , selectorsAndBlockCapture
            , RegexOptions.IgnorePatternWhitespace ||| RegexOptions.ExplicitCapture
            , TimeSpan.FromSeconds 10.
        )

    if cssContainsBlock then
        classNamesFromCss text
        |> Seq.toArray
        |> Some
    else None

// Hard-coded since we know we are targeting netstandard2.0
// https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.osplatform.create?view=netstandard-2.0
let private validPlatforms = set [ "FREEBSD" ; "LINUX" ; "OSX" ; "WINDOWS" ]

let platformSpecific ( delimiters : string ) ( text : string ) =
    if String.IsNullOrWhiteSpace delimiters then text
    else
        let delimiters = delimiters.Trim()
        let outerDelimiter = delimiters.[ 0 ]
        let innerDelimiter = if delimiters.Length = 1 then '=' else delimiters.[ 1 ]
        let segments = text.Split( outerDelimiter );
        if Array.length segments = 1 then Array.head segments
        else
            let platformValueTuples =
                segments
                |> Array.map ( fun segment ->
                    match segment.Split( [| innerDelimiter |] , 2 ) with
                    | [| platform ; value |] ->
                        let platform = platform.Trim().ToUpperInvariant()
                        if Set.contains platform validPlatforms then
                            platform , value
                        else
                            "" , segment
                    | _ -> "" , segment
                )

            // First check for at least one valid platform string.
            // Else assume accidental syntax match and return original whole string.
            if Array.exists ( fun ( plat , _ ) -> plat <> "" ) platformValueTuples then
                platformValueTuples
                |> Array.tryFind ( fun ( plat , _ ) ->
                    // Find first for current platform
                    plat <> "" && RuntimeInformation.IsOSPlatform ( OSPlatform.Create( plat ) )
                )
                |> Option.orElseWith ( fun () ->
                    // Else find first with non specified platform
                    platformValueTuples |> Array.tryFind ( fun ( plat , _ ) -> plat = "" )
                )
                |> Option.orElseWith ( fun () ->
                    // Else use first regardless of platform specifier
                    Array.tryHead platformValueTuples
                )
                |> Option.map snd
                |> Option.defaultValue ""

            else
                text

let importClassFromCssModule (source: string) (className: string) =
    // By separating `default` and the class name with a period,
    // Fable will automatically generate JS code like this:
    // import style from "./style.module.css";
    // let myClass = style.["my-class"];
    let selector = Expr.Value("default." + className)
    let path = Expr.Value(HttpUtility.JavaScriptStringEncode source)
    fun (_args: Expr list) -> <@@ Fable.Core.JsInterop.import<string> %%selector %%path @@>

let addTypeMembersFromCss isCssModule source getProperties ( cssClasses : Property array ) ( cssType : ProvidedTypeDefinition ) =

    cssClasses
    |> Array.iter ( fun c ->
        let propName , propValue = c.Name , c.Value
        if isCssModule then
            let prop = ProvidedProperty(propName, typeof<string>, isStatic = true, getterCode = (importClassFromCssModule source propValue))
            cssType.AddMember prop
        else
            // Use literal field instead of property for better completion list performance in IDEs
            let field = ProvidedField.Literal( propName , typeof< string > , propValue ) // public static literal
            field.SetFieldAttributes( field.Attributes ||| Reflection.FieldAttributes.InitOnly ) // prevent assignment
            cssType.AddMember field
    )

    if getProperties then
        let rowType = ProvidedTypeDefinition("Property", Some(typeof<string[]>), hideObjectMethods = true)
        let rowNameProp = ProvidedProperty("Name", typeof<string>, getterCode = fun (Singleton row) -> <@@ (%%row:string[]).[0] @@>)
        rowNameProp.AddXmlDoc "Generated property name using specified naming strategy."
        let rowValueProp = ProvidedProperty("Value", typeof<string>, getterCode = fun (Singleton row) -> <@@ (%%row:string[]).[1] @@>)
        rowValueProp.AddXmlDoc "The underlying CSS class value."

        rowType.AddMember rowNameProp
        rowType.AddMember rowValueProp
        cssType.AddMember rowType

        let propsArray = cssClasses |> Array.map ( fun p -> [| p.Name ; p.Value |] )
        let usedNames = cssClasses |> Array.map ( fun p -> p.Name ) |> Set.ofArray
        let methodName =
            Seq.init 99 ( fun i -> "GetProperties" + if i = 0 then "" else "_" + string ( i + 1 ) )
            |> Seq.find ( fun s -> usedNames |> Set.contains s |> not )
        let staticMethod =
            ProvidedMethod(methodName, [], typedefof<seq<_>>.MakeGenericType(rowType), isStatic = true,
                invokeCode = fun _-> <@@ propsArray @@>)

        cssType.AddMember staticMethod
