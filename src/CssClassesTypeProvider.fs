namespace Zanaptak.TypedCssClasses

open Zanaptak.TypedCssClasses.Internal.FSharpData.Helpers
open Zanaptak.TypedCssClasses.Internal.FSharpData.IO
open Zanaptak.TypedCssClasses.Internal.FSharpData.ProvidedTypes
open Zanaptak.TypedCssClasses.Internal.ProviderImplementation.ProvidedTypes
open FSharp.Core.CompilerServices
open System
open System.Reflection
open System.Runtime.InteropServices
open System.IO

module TypeProvider =

    [< TypeProvider >]
    type CssClassesTypeProvider ( config : TypeProviderConfig ) as this =
        inherit DisposableTypeProviderForNamespaces( config )

        let ctorLog = ResizeArray< string >()
        let proc = System.Diagnostics.Process.GetCurrentProcess()
        do
            ctorLog.Add ( sprintf "Environment.CommandLine: %s" Environment.CommandLine )
            ctorLog.Add ( sprintf "Environment.CurrentDirectory: %s" Environment.CurrentDirectory )
            ctorLog.Add ( sprintf "TypeProviderConfig.RuntimeAssembly: %s" config.RuntimeAssembly )
            ctorLog.Add ( sprintf "TypeProviderConfig.ResolutionFolder: %s" ( if isNull config.ResolutionFolder then "<null>" else config.ResolutionFolder ) )
            ctorLog.Add ( sprintf "TypeProviderConfig.IsHostedExecution: %b" config.IsHostedExecution )
            ctorLog.Add ( sprintf "TypeProviderConfig.IsInvalidationSupported: %b" config.IsInvalidationSupported )
            ctorLog.Add ( sprintf "TypeProviderConfig.TemporaryFolder: %s" config.TemporaryFolder )

        let ns = "Zanaptak.TypedCssClasses"
        let asm = Assembly.GetExecutingAssembly()

        let parentType = ProvidedTypeDefinition( asm , ns , "CssClasses" , Some ( typeof< obj > ) )

        let buildTypes ( typeName : string ) ( args : obj[] ) =

            let osDelimiters = args.[ 9 ] :?> string
            let expandVariables = args.[ 10 ] :?> bool

            let processStringParameter ( arg : obj ) =
                arg :?> string
                |> Utils.platformSpecific osDelimiters
                |> fun s -> if expandVariables then Environment.ExpandEnvironmentVariables s else s

            let resolutionFolder = args.[ 2 ] |> processStringParameter

            let finalResolutionFolder =
                [ resolutionFolder ; config.ResolutionFolder ; Environment.CurrentDirectory ]
                |> List.find ( fun dir -> not ( String.IsNullOrWhiteSpace dir ) )

            let logFile = args.[ 5 ] |> processStringParameter

            if not ( String.IsNullOrWhiteSpace logFile ) then
                enableLogType typeName ( Path.GetFullPath( Path.Combine( finalResolutionFolder , logFile ) ) )

            logfType typeName this.Id "**** CssClassesTypeProvider[%i]: Host process <%s> [%i] requesting type: %s" this.Id proc.ProcessName proc.Id typeName

            ctorLog |> Seq.iter ( logType typeName this.Id )

            let commandParam = args.[ 6 ] |> processStringParameter
            let commandConfig =
                if String.IsNullOrWhiteSpace commandParam then None
                else
                    Some {
                        commandFile = commandParam
                        argumentPrefix = args.[ 7 ] |> processStringParameter
                        argumentSuffix = args.[ 8 ] |> processStringParameter
                    }

            logfType typeName this.Id "Configured resolution folder: %s" finalResolutionFolder
            logfType typeName this.Id "Configured command: %s" commandParam

            let source = args.[ 0 ] |> processStringParameter
            let naming = args.[ 1 ] :?> Naming
            let getProperties = args.[ 3 ] :?> bool
            let nameCollisions = args.[ 4 ] :?> NameCollisions
            let fableCssModule = args.[ 11 ] :?> bool

            let tryParseTextClassNames value =
                logType typeName this.Id "Parsing CSS"
                Utils.tryParseCssClassNames value

            let convertClassNamesToProperties value =
                logType typeName this.Id "Creating properties"
                Utils.getPropertiesFromClassNames naming nameCollisions value

            let createType parseResult =
                logType typeName this.Id "Creating type"
                let cssType = ProvidedTypeDefinition( asm , ns , typeName , Some ( typeof< obj > ) )
                Utils.addTypeMembersFromCss fableCssModule source getProperties parseResult cssType
                cssType

            generateType source commandConfig this config finalResolutionFolder typeName tryParseTextClassNames convertClassNamesToProperties createType

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
            ProvidedStaticParameter( "fableCssModule" , typeof< bool >, parameterDefaultValue = false )
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
            <param name='fableCssModule'>Resolve properties to Fable import expressions instead of raw class names to allow processing as a CSS Module.</param>
        """

        do parentType.AddXmlDoc helpText
        do parentType.DefineStaticParameters( parameters , buildTypes )

        do this.AddNamespace( ns, [ parentType ] )

[< TypeProviderAssembly >]
do ()

#if INTERNALS_VISIBLE
open System.Runtime.CompilerServices
[< assembly : InternalsVisibleTo( "TypedCssClasses.Tests" ) >]
do ()
#endif
