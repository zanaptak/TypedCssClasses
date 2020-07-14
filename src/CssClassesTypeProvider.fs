namespace Zanaptak.TypedCssClasses

open Zanaptak.TypedCssClasses.Internal.FSharp.Data.Runtime
open Zanaptak.TypedCssClasses.Internal.ProviderImplementation
open Zanaptak.TypedCssClasses.Internal.ProviderImplementation.ProvidedTypes
open Zanaptak.TypedCssClasses.Internal.ProviderImplementation.ProviderHelpers
open FSharp.Core.CompilerServices
open System
open System.Reflection
open System.Runtime.InteropServices
open System.IO

module TypeProvider =

  let private addTypeMembersFromCss getProperties ( cssClasses : Utils.Property [] option ) ( cssType : ProvidedTypeDefinition ) =

    let cssClasses = cssClasses |> Option.defaultValue [||]

    cssClasses
    |> Array.iter ( fun c ->
      let propName , propValue = c.Name , c.Value
      let prop = ProvidedProperty( propName , typeof< string > , isStatic = true , getterCode = ( fun _ -> <@@ propValue @@> ) )
      prop.AddXmlDoc( Utils.escapeHtml propValue )
      cssType.AddMember prop
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

  // Hard-coded since we know we are targeting netstandard2.0
  // https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.osplatform.create?view=netstandard-2.0
  let validPlatforms = set [ "FREEBSD" ; "LINUX" ; "OSX" ; "WINDOWS" ]

  let private platformSpecific ( delimiters : string ) ( text : string ) =
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

  [< TypeProvider >]
  type CssClassesTypeProvider ( config : TypeProviderConfig ) as this =
    inherit DisposableTypeProviderForNamespaces( config )

    let ctorLog = ResizeArray< string >()
    do
      ctorLog.Add "======== CssClassesTypeProvider config ========"
      ctorLog.Add ( sprintf "TypeProviderConfig.RuntimeAssembly: %s" config.RuntimeAssembly )
      ctorLog.Add ( sprintf "TypeProviderConfig.ResolutionFolder: %s" ( if isNull config.ResolutionFolder then "<null>" else config.ResolutionFolder ) )
      ctorLog.Add ( sprintf "TypeProviderConfig.IsHostedExecution: %b" config.IsHostedExecution )
      ctorLog.Add ( sprintf "TypeProviderConfig.IsInvalidationSupported: %b" config.IsInvalidationSupported )
      ctorLog.Add ( sprintf "TypeProviderConfig.TemporaryFolder: %s" config.TemporaryFolder )
      let proc = System.Diagnostics.Process.GetCurrentProcess()
      ctorLog.Add ( sprintf "Process: [%i] %s" proc.Id proc.ProcessName )
      ctorLog.Add ( sprintf "Environment.CommandLine: %s" Environment.CommandLine )
      ctorLog.Add ( sprintf "Environment.CurrentDirectory: %s" Environment.CurrentDirectory )
      ctorLog.Add "-----------------------------------------------"

    let ns = "Zanaptak.TypedCssClasses"
    let asm = Assembly.GetExecutingAssembly()

    let parentType = ProvidedTypeDefinition( asm , ns , "CssClasses" , Some ( typeof< obj > ) )

    let buildTypes ( typeName : string ) ( args : obj[] ) =

      let osDelimiters = args.[ 9 ] :?> string
      let expandVariables = args.[ 10 ] :?> bool

      let processStringParameter ( arg : obj ) =
        arg :?> string
        |> platformSpecific osDelimiters
        |> fun s -> if expandVariables then Environment.ExpandEnvironmentVariables s else s

      let resolutionFolder = args.[ 2 ] |> processStringParameter

      let finalResolutionFolder =
        [ resolutionFolder ; config.ResolutionFolder ; Environment.CurrentDirectory ]
        |> List.find ( fun dir -> not ( String.IsNullOrWhiteSpace dir ) )

      let logFile = args.[ 5 ] |> processStringParameter

      if not ( String.IsNullOrWhiteSpace logFile ) then
        IO.enableLogType typeName ( Path.GetFullPath( Path.Combine( finalResolutionFolder , logFile ) ) )

      ctorLog |> Seq.iter ( IO.logType typeName )

      IO.logfType typeName "Type: %s" typeName

      let commandParam = args.[ 6 ] |> processStringParameter
      let commandConfig =
        if String.IsNullOrWhiteSpace commandParam then None
        else
          Some {
            commandFile = commandParam
            argumentPrefix = args.[ 7 ] |> processStringParameter
            argumentSuffix = args.[ 8 ] |> processStringParameter
          }

      IO.logfType typeName "Configured resolution folder: %s" finalResolutionFolder
      IO.logfType typeName "Configured command: %s" commandParam

      let naming = args.[ 1 ] :?> Naming
      let getProperties = args.[ 3 ] :?> bool
      let nameCollisions = args.[ 4 ] :?> NameCollisions

      let parseSampleToTypeSpec _ value =
        using ( IO.logTimeType typeName "ParseAndGenerateMembers" typeName ) <| fun _ ->

        let cssType = ProvidedTypeDefinition( asm , ns , typeName , Some ( typeof< obj > ) )
        IO.logType typeName "Parsing CSS"
        let cssClasses = Utils.parseCss value naming nameCollisions
        IO.logType typeName "Adding type members"
        addTypeMembersFromCss getProperties cssClasses cssType

        {
          GeneratedType = cssType
          RepresentationType = cssType
          CreateFromTextReader = fun _ -> failwith "Not Applicable"
          CreateFromTextReaderForSampleList = fun _ -> failwith "Not Applicable"
          WasValidInput = Option.isSome cssClasses
        }

      let source = args.[ 0 ] |> processStringParameter
      generateType "CSS" ( Sample source ) parseSampleToTypeSpec commandConfig this config "" finalResolutionFolder "" typeName None

    let parameters = [
      ProvidedStaticParameter( "source" , typeof< string >, parameterDefaultValue = "" )
      ProvidedStaticParameter( "naming" , typeof< Naming >, parameterDefaultValue = Naming.Verbatim )
      ProvidedStaticParameter( "resolutionFolder" , typeof< string >, parameterDefaultValue = "" )
      ProvidedStaticParameter( "getProperties" , typeof< bool >, parameterDefaultValue = false )
      ProvidedStaticParameter( "nameCollisions" , typeof< NameCollisions >, parameterDefaultValue = NameCollisions.BasicSuffix )
      ProvidedStaticParameter( "logFile" , typeof< string >, parameterDefaultValue = "" )
      ProvidedStaticParameter( "commandFile" , typeof< string >, parameterDefaultValue = "" )
      ProvidedStaticParameter( "argumentPrefix" , typeof< string >, parameterDefaultValue = "" )
      ProvidedStaticParameter( "argumentSuffix" , typeof< string >, parameterDefaultValue = "" )
      ProvidedStaticParameter( "osDelimiters" , typeof< string >, parameterDefaultValue = ",=" )
      ProvidedStaticParameter( "expandVariables" , typeof< bool >, parameterDefaultValue = true )
    ]

    let helpText = """
      <summary>Typed CSS classes. Provides generated properties representing CSS classes from a stylesheet.</summary>
      <param name='source'>Location of a CSS stylesheet (file path or web URL), or a string containing CSS text.</param>
      <param name='naming'>Naming strategy for class name properties.
        One of: Naming.Verbatim (default), Naming.Underscores, Naming.CamelCase, Naming.PascalCase.</param>
      <param name='resolutionFolder'>A directory that is used when resolving relative file references.</param>
      <param name='getProperties'>Adds a GetProperties() method that returns a seq of all generated property name/value pairs.</param>
      <param name='nameCollisions'>Behavior of name collisions that arise from naming strategy.
        One of: NameCollisions.BasicSuffix (default), NameCollisions.ExtendedSuffix, NameCollisions.Omit.</param>
      <param name='logFile'>File path to write logging information to.</param>
      <param name='commandFile'>Executable file to run, with source parameter as an argument, to produce the CSS output used to generate properties.</param>
      <param name='argumentPrefix'>Argument string to include before the source parameter, separated by a space, when running command.</param>
      <param name='argumentSuffix'>Argument string to include after the source parameter, separated by a space, when running command.</param>
      <param name='osDelimiters'>Characters that separate platform-specific values in the provided parameters.
        Default is ",="</param>
      <param name='expandVariables'>Expand environment variables in the provided parameters.</param>
    """

    do parentType.AddXmlDoc helpText
    do parentType.DefineStaticParameters( parameters , buildTypes )

    do this.AddNamespace( ns, [ parentType ] )

[< TypeProviderAssembly >]
do ()
