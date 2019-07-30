namespace Zanaptak.TypedCssClasses

open Zanaptak.TypedCssClasses.Internal.FSharp.Data.Runtime
open Zanaptak.TypedCssClasses.Internal.ProviderImplementation
open Zanaptak.TypedCssClasses.Internal.ProviderImplementation.ProvidedTypes
open Zanaptak.TypedCssClasses.Internal.ProviderImplementation.ProviderHelpers
open FSharp.Core.CompilerServices
open System
open System.Reflection

type Naming =
  | Verbatim = 0
  | Underscores = 1
  | CamelCase = 2
  | PascalCase = 3

module TypeProvider =

  [< TypeProvider >]
  type CssClassesTypeProvider ( config : TypeProviderConfig ) as this =
    inherit DisposableTypeProviderForNamespaces( config )

    do
      IO.log ( sprintf "TypeProviderConfig.IsHostedExecution = %b" config.IsHostedExecution )
      IO.log ( sprintf "TypeProviderConfig.IsInvalidationSupported = %b" config.IsInvalidationSupported )
      IO.log ( sprintf "TypeProviderConfig.TemporaryFolder = %s" config.TemporaryFolder )
      if config.ResolutionFolder = null then
        IO.log "TypeProviderConfig.ResolutionFolder is null"
      elif config.ResolutionFolder = "" then
        IO.log "TypeProviderConfig.ResolutionFolder is empty string"
      else
        IO.log ( sprintf "TypeProviderConfig.ResolutionFolder = %s" config.ResolutionFolder )
      IO.log ( sprintf "TypeProviderConfig.RuntimeAssembly = %s" config.RuntimeAssembly )

      IO.log ( sprintf "TypeProviderConfig.ReferencedAssemblies count = %i" ( Array.length config.ReferencedAssemblies ) )
      config.ReferencedAssemblies
      |> Array.truncate 5
      |> Array.iter ( fun s -> IO.log ( sprintf "    %s" s ) )
      if Array.length config.ReferencedAssemblies > 5 then IO.log "    ..."

      IO.log ( sprintf "Environment.CommandLine = %s" Environment.CommandLine )
      IO.log ( sprintf "Environment.CurrentDirectory = %s" Environment.CurrentDirectory )

    let ns = "Zanaptak.TypedCssClasses"
    let asm = Assembly.GetExecutingAssembly()

    let parentType = ProvidedTypeDefinition( asm , ns , "CssClasses" , Some ( typeof< obj > ) )

    let buildTypes ( typeName : string ) ( args : obj[] ) =

      let source = args.[ 0 ] :?> string
      let naming = args.[ 1 ] :?> Naming
      let resolutionFolder = args.[ 2 ] :?> string

      let getSpec _ value =

        let transformer =
          match naming with
          | Naming.Underscores -> Utils.symbolsToUnderscores
          | Naming.CamelCase -> NameUtils.niceCamelName
          | Naming.PascalCase -> NameUtils.nicePascalName
          | _ -> id

        let cssClasses = Utils.parseCss value transformer

        //using ( IO.logTime "TypeGeneration" source ) <| fun _ ->

        let cssType = ProvidedTypeDefinition( asm , ns , typeName , Some ( typeof< obj > ) )

        cssClasses
        |> Seq.iter ( fun c ->
          let propName , propValue = c.Property , c.Value
          let prop = ProvidedProperty( propName , typeof< string > , isStatic = true , getterCode = ( fun _ -> <@@ propValue @@> ) )
          prop.AddXmlDoc( Utils.escapeHtml propValue )
          cssType.AddMember prop
        )

        {
          GeneratedType = cssType
          RepresentationType = cssType
          CreateFromTextReader = fun _ -> failwith "Not Applicable"
          CreateFromTextReaderForSampleList = fun _ -> failwith "Not Applicable"
        }

      generateType' "CSS" ( Sample source ) getSpec this config "" resolutionFolder "" typeName None

    let parameters = [
      ProvidedStaticParameter( "source" , typeof< string >, parameterDefaultValue = "" )
      ProvidedStaticParameter( "naming" , typeof< Naming >, parameterDefaultValue = Naming.Verbatim )
      ProvidedStaticParameter( "resolutionFolder" , typeof< string >, parameterDefaultValue = "" )
    ]

    let helpText = """
      <summary>Typed CSS classes.</summary>
      <param name='source'>Location of a CSS stylesheet (file path or web URL), or a string containing CSS text.</param>
      <param name='naming'>Naming strategy for class name properties, specified by the Naming enum.
        Verbatim: (default) use class names verbatim from source CSS, requiring backtick-quotes for names with special characters.
        Underscores: replace all non-alphanumeric characters with underscores.
        CamelCase: convert to camel case names with all non-alphanumeric characters removed.
        PascalCase: convert to Pascal case names with all non-alphanumeric characters removed.
      </param>
      <param name='resolutionFolder'>A directory that is used when resolving relative file references.</param>
    """

    do parentType.AddXmlDoc helpText
    do parentType.DefineStaticParameters( parameters , buildTypes )

    do this.AddNamespace( ns, [ parentType ] )

[< TypeProviderAssembly >]
do ()
