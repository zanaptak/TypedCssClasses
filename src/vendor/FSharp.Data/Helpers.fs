//
// Modified for Zanaptak.TypedCssClasses by zanaptak.
//

// Copyright 2011-2015, Tomas Petricek (http://tomasp.net), Gustavo Guerra (http://functionalflow.co.uk), and other contributors
// Licensed under the Apache License, Version 2.0, see LICENSE.md in this project
//
// Helpers for writing type providers

namespace Zanaptak.TypedCssClasses.Internal.ProviderImplementation

open System
open System.Collections.Generic
open System.Reflection
open System.Text
open FSharp.Core.CompilerServices
open FSharp.Quotations
open Zanaptak.TypedCssClasses.Internal.FSharp.Data.Runtime
open Zanaptak.TypedCssClasses.Internal.FSharp.Data.Runtime.IO
open Zanaptak.TypedCssClasses.Internal.FSharp.Data.Runtime.StructuralTypes
open Zanaptak.TypedCssClasses.Internal.FSharp.Data.Runtime.StructuralInference
open Zanaptak.TypedCssClasses.Internal.ProviderImplementation
open Zanaptak.TypedCssClasses.Internal.ProviderImplementation.ProvidedTypes

// ----------------------------------------------------------------------------------------------

[<AutoOpen>]
module internal PrimitiveInferedPropertyExtensions =

    type PrimitiveInferedProperty with

      member x.TypeWithMeasure =
          match x.UnitOfMeasure with
          | None -> x.RuntimeType
          | Some unit ->
              if supportsUnitsOfMeasure x.RuntimeType
              then ProvidedMeasureBuilder.AnnotateType(x.RuntimeType, [unit])
              else failwithf "Units of measure not supported by type %s" x.RuntimeType.Name


// ----------------------------------------------------------------------------------------------

[<AutoOpen>]
module internal ActivePatterns =

    /// Helper active pattern that can be used when constructing InvokeCode
    /// (to avoid writing pattern matching or incomplete matches):
    ///
    ///    p.InvokeCode <- fun (Singleton self) -> <@ 1 + 2 @>
    ///
    let (|Singleton|) = function [l] -> l | _ -> failwith "Parameter mismatch"

    /// Takes a map and succeeds if it is empty
    let (|EmptyMap|_|) result (map:Map<_,_>) = if map.IsEmpty then Some result else None

    /// Takes a map and succeeds if it contains exactly one value
    let (|SingletonMap|_|) (map:Map<_,_>) =
        if map.Count <> 1 then None else
            let (KeyValue(k, v)) = Seq.head map
            Some(k, v)

// ----------------------------------------------------------------------------------------------

module internal ReflectionHelpers =

    open FSharp.Quotations
    open UncheckedQuotations

    let makeDelegate (exprfunc:Expr -> Expr) argType =
        let var = Var("t", argType)
        let convBody = exprfunc (Expr.Var var)
        Expr.NewDelegateUnchecked(typedefof<Func<_,_>>.MakeGenericType(argType, convBody.Type), [var], convBody)

// ----------------------------------------------------------------------------------------------

type DisposableTypeProviderForNamespaces(config, ?assemblyReplacementMap) as x =
    inherit TypeProviderForNamespaces(config, ?assemblyReplacementMap=assemblyReplacementMap)

    let disposeActions = ResizeArray()

    static let mutable idCount = 0

    let id = idCount
    let filesToWatch = Dictionary< string , string list >()

    do idCount <- idCount + 1

    let dispose typeNameOpt =
        lock disposeActions <| fun () ->
            for i = disposeActions.Count-1 downto 0 do
                let disposeAction = disposeActions.[i]
                let discard = disposeAction typeNameOpt
                if discard then
                    disposeActions.RemoveAt(i)

    do
        log (sprintf "Creating TypeProviderForNamespaces %O [%d]" x id)
        x.Disposing.Add <| fun _ ->
            using (IO.logTime "DisposingEvent" (sprintf "%O [%d]" x id)) <| fun _ ->
                dispose None

    member __.Id = id

    member __.SetFileToWatch(fullTypeName, path) =
        lock filesToWatch <| fun () ->
            filesToWatch.[fullTypeName] <- path

    member __.GetFileToWatch(fullTypeName) =
        lock filesToWatch <| fun () ->
            match filesToWatch.TryGetValue(fullTypeName) with
            | true, path -> Some path
            | _ -> None

    member __.AddDisposeAction action =
        lock disposeActions <| fun () -> disposeActions.Add action

    member __.InvalidateOneType typeName =
        using (IO.logTimeType typeName "InvalidateOneType" (sprintf "%s in %O [%d]" typeName x id)) <| fun _ ->
            dispose (Some typeName)
            IO.logfType typeName "Calling invalidate for %O [%d]" x id
        base.Invalidate()

    override x.Finalize() =
        log (sprintf "Finalize %O [%d]" x id)

// ----------------------------------------------------------------------------------------------

module internal ProviderHelpers =

    open System.IO
    open System.Diagnostics
    open System.Text.RegularExpressions
    open Zanaptak.TypedCssClasses.Internal.FSharp.Data.Runtime.Caching

    let unitsOfMeasureProvider =
        { new StructuralInference.IUnitsOfMeasureProvider with
            member x.SI(str) = ProvidedMeasureBuilder.SI str
            member x.Product(measure1, measure2) = ProvidedMeasureBuilder.Product(measure1, measure2)
            member x.Inverse(denominator): Type = ProvidedMeasureBuilder.Inverse(denominator) }

    let asyncMap (resultType:Type) (valueAsync:Expr<Async<'T>>) (body:Expr<'T>->Expr) =
        let (?) = QuotationBuilder.(?)
        let convFunc = ReflectionHelpers.makeDelegate (Expr.Cast >> body) typeof<'T>
        let f = Var("f", convFunc.Type)
        let body = typeof<TextRuntime>?AsyncMap (typeof<'T>, resultType) (valueAsync, Expr.Var f)
        Expr.Let(f, convFunc, body)

    let some (typ:Type) arg =
        let unionType = typedefof<option<_>>.MakeGenericType typ
        let meth = unionType.GetMethod("Some")
        Expr.Call(meth, [arg])

    let private cacheDuration = TimeSpan.FromDays 90.
    let private webUrisCache = createInternetFileCache "Zanaptak.TypedCssClasses" cacheDuration
    let private invalidPathChars = Set.ofArray ( Path.GetInvalidPathChars() )
    let private invalidFileChars = Set.ofArray ( Path.GetInvalidFileNameChars() )

    let private isValidFilenameSyntax str =
      if String.IsNullOrWhiteSpace str
        || Seq.exists invalidPathChars.Contains str
        || String.IsNullOrWhiteSpace( Path.GetFileName str )
        || Seq.exists invalidFileChars.Contains ( Path.GetFileName str )
      then false
      else true

    let private pathFromFileUri ( uri : Uri ) =
      Regex.Replace( uri.OriginalString , @"^file://" , "" , RegexOptions.None , TimeSpan.FromSeconds 5. )

    let private tryGetUri fullTypeName str =
      try
        match Uri.TryCreate(str, UriKind.RelativeOrAbsolute) with
        | false, _ -> None
        | true, uri ->
            if isWeb uri then Some uri
            else
              let path = if uri.IsAbsoluteUri then pathFromFileUri uri else uri.OriginalString
              if isValidFilenameSyntax path then Some uri else None
      with
      | ex ->
          IO.logfType fullTypeName "tryGetUri error, assuming non file: %A" ex
          None

    // part of the information needed by generateType
    type TypeProviderSpec =
        { //the generated type
          GeneratedType : ProvidedTypeDefinition
          //the representation type (what's returned from the constructors, may or may not be the same as Type)
          RepresentationType : Type
          // the constructor from a text reader to the representation
          CreateFromTextReader : Expr<TextReader> -> Expr
          // the constructor from a text reader to an array of the representation
          CreateFromTextReaderForSampleList : Expr<TextReader> -> Expr
          WasValidInput : bool
        }

    type private ParseTextResult =
        { Spec : TypeProviderSpec
          IsUri : bool
          IsResource : bool }

    let readResource(tp: DisposableTypeProviderForNamespaces, resourceName:string) =
        match resourceName.Split(',') with
        | [| asmName; name |] ->
            let bindingCtxt = tp.TargetContext
            match bindingCtxt.TryBindSimpleAssemblyNameToTarget(asmName.Trim()) with
            | Choice1Of2 asm ->
                use sr = new StreamReader(asm.GetManifestResourceStream(name.Trim()))
                Some(sr.ReadToEnd())
            | _ -> None
        | _ -> None

    /// Reads a sample parameter for a type provider, detecting if it is a uri and fetching it if needed
    /// Samples from the web are cached for 30 minutes
    /// Samples from the filesystem are read using shared read, so it works when the file is locked by Excel or similar tools,
    ///
    /// Parameters:
    /// * valueToBeParsedOrItsUri - the text which can be a sample or an uri for a sample
    /// * parseFunc - receives the file/url extension (or ""  if not applicable) and the text value
    /// * formatName - the description of what is being parsed (for the error message)
    /// * tp - the type provider
    /// * cfg - the type provider config
    /// * resource - when specified, we first try to treat read the sample from an embedded resource
    ///     (the value specified assembly and resource name e.g. "MyCompany.MyAssembly, some_resource.json")
    /// * resolutionFolder - if the type provider allows to override the resolutionFolder pass it here
    let private parseTextAtDesignTime valueToBeParsedOrItsUri parseFunc formatName (tp:DisposableTypeProviderForNamespaces)
                                      (cfg:TypeProviderConfig) encodingStr resolutionFolder resource fullTypeName maxNumberOfRows =

        using (IO.logTimeType fullTypeName "LoadTextFromSource" fullTypeName) <| fun _ ->

        let tryGetResource() =
            if resource = "" then None else readResource(tp, resource)

        match tryGetResource() with
        | Some res -> { Spec = parseFunc "" res
                        IsUri = false
                        IsResource = true }
        | _ ->

        match tryGetUri fullTypeName valueToBeParsedOrItsUri with
        | None ->

            try
                let parseResult =
                  { Spec = parseFunc "" valueToBeParsedOrItsUri
                    IsUri = false
                    IsResource = false }
                parseResult
            with e ->
                failwithf "The provided source is neither a file, nor a well-formed %s: %s" formatName e.Message

        | Some uri ->

            let resolver =
                { ResolutionType = DesignTime
                  DefaultResolutionFolder = cfg.ResolutionFolder
                  ResolutionFolder = resolutionFolder }

            let readText() =
                let reader, toWatch = asyncRead fullTypeName resolver formatName encodingStr uri
                // Non need to register file watchers in fsc.exe and fsi.exe
                if cfg.IsInvalidationSupported  then
                    toWatch |> Option.iter (fun path -> tp.SetFileToWatch(fullTypeName, [ path ]))
                use reader = reader |> Async.RunSynchronously
                match maxNumberOfRows with
                | None -> reader.ReadToEnd()
                | Some max ->
                    let sb = StringBuilder()
                    let max = ref max
                    while !max > 0 do
                        let line = reader.ReadLine()
                        if line = null then
                            max := 0
                        else
                            line |> sb.AppendLine |> ignore
                            decr max
                    sb.ToString()

            try

                let sample =
                    if isWeb uri then
                        let text =
                            match webUrisCache.TryRetrieve(uri.OriginalString) with
                            | Some text ->
                                IO.logfType fullTypeName "Web URI retrieved from cache: %s" uri.OriginalString
                                text
                            | None ->
                                let text = readText()
                                webUrisCache.Set(uri.OriginalString, text)
                                text
                        text
                    else
                        readText()

                let parseResult =
                  { Spec = parseFunc (Path.GetExtension uri.OriginalString) sample
                    IsUri = true
                    IsResource = false }

                parseResult

            with e ->
                if not uri.IsAbsoluteUri then
                    // even if it's a valid uri, it could be sample text
                    IO.logfType fullTypeName "File read failed, attempting processing as text"
                    try
                        let parseResult =
                          { Spec = parseFunc "" valueToBeParsedOrItsUri
                            IsUri = false
                            IsResource = false }
                        if not parseResult.Spec.WasValidInput then
                          // backup text parse failed to match any CSS, assume it was a failed file uri
                          failwith ""
                        parseResult
                    with _ ->
                        // if not, return the first exception
                        failwithf "Cannot read %s from '%s': %s" formatName valueToBeParsedOrItsUri e.Message
                else
                    failwithf "Cannot read %s from '%s': %s" formatName valueToBeParsedOrItsUri e.Message

    type RunCommandConfig = {
      commandFile : string
      argumentPrefix : string
      argumentSuffix : string
    }

    /// Runs the command supplied to the TP and uses the output of the command as the source sample.
    ///
    /// Parameters:
    /// * valueToBeParsedOrItsUri - the text which can be a sample or an uri for a sample
    /// * commandConfig - configuration for command to run and its arguments
    /// * parseFunc - receives the file/url extension (or ""  if not applicable) and the text value
    /// * formatName - the description of what is being parsed (for the error message)
    /// * tp - the type provider
    /// * cfg - the type provider config
    /// * resource - when specified, we first try to treat read the sample from an embedded resource
    ///     (the value specified assembly and resource name e.g. "MyCompany.MyAssembly, some_resource.json")
    /// * resolutionFolder - if the type provider allows to override the resolutionFolder pass it here
    let private runCommandAtDesignTime valueToBeParsedOrItsUri ( commandConfig : RunCommandConfig ) parseFunc formatName (tp:DisposableTypeProviderForNamespaces)
                                      (cfg:TypeProviderConfig) encodingStr resolutionFolder resource fullTypeName maxNumberOfRows =

      using (IO.logTimeType fullTypeName "RunCommand" fullTypeName) <| fun _ ->

      let uriResolver =
        { ResolutionType = DesignTime
          DefaultResolutionFolder = cfg.ResolutionFolder
          ResolutionFolder = resolutionFolder }

      let checkPathExistsLocally path =
        match tryGetUri fullTypeName path with
        | None -> None
        | Some uri ->
            try
              match uriResolver.Resolve uri with
              | uri , false ->
                  // non-web uri, check if exists
                  let resolvedPath = pathFromFileUri uri
                  if File.Exists resolvedPath then Some resolvedPath else None
              | _ -> None // web uri
            with
            | ex ->
                IO.logfType fullTypeName "resolver error, assuming non file: %A" ex
                None // in case of any IO error on resolve or existence check

      let initialFileWatchList =
        match checkPathExistsLocally valueToBeParsedOrItsUri with
        | None -> []
        | Some path -> [ path ]

      let getTextFromProcess () =

        let stdoutSb = StringBuilder()
        let stderrSb = StringBuilder()

        let pinfo = ProcessStartInfo( commandConfig.commandFile )
        pinfo.UseShellExecute <- false
        pinfo.RedirectStandardInput <- false
        pinfo.RedirectStandardOutput <- true
        pinfo.RedirectStandardError <- true
        pinfo.CreateNoWindow <- true
        pinfo.Arguments <-
          [ commandConfig.argumentPrefix ; valueToBeParsedOrItsUri ; commandConfig.argumentSuffix ]
          |> List.filter ( fun arg -> arg <> "" )
          |> String.concat " "
        pinfo.WorkingDirectory <- resolutionFolder
        IO.logfType fullTypeName "Process filename: %s" pinfo.FileName
        IO.logfType fullTypeName "Arguments: %s" pinfo.Arguments
        IO.logfType fullTypeName "Working directory: %s" pinfo.WorkingDirectory

        use p = new Process()
        p.StartInfo <- pinfo
        p.OutputDataReceived.Add( fun eventArgs ->
          if not ( isNull eventArgs.Data ) then
            if stdoutSb.Length = 0 then IO.logType fullTypeName "StandardOutput data started"
            stdoutSb.AppendLine( eventArgs.Data ) |> ignore
        )
        p.ErrorDataReceived.Add( fun eventArgs ->
          if not ( isNull eventArgs.Data ) then
            if stderrSb.Length = 0 then IO.logType fullTypeName "StandardError data started"
            stderrSb.AppendLine( eventArgs.Data ) |> ignore
        )

        p.Start() |> ignore
        p.BeginOutputReadLine()
        p.BeginErrorReadLine()
        IO.logType fullTypeName "Process started"

        let css =
          if p.WaitForExit( 60000 ) then
            IO.logType fullTypeName "Process completed"

            p.WaitForExit() // flush remaining output to the data receive events

            if p.ExitCode <> 0 then
              let stderr = stderrSb.ToString()
              failwithf "Command failed with exit code %i and stderr: %s" p.ExitCode stderr

            use outReader = new StringReader( stdoutSb.ToString() )

            // Check initial lines for local files to monitor for changes
            let rec checkLine fileWatchList =
              if outReader.Peek() < 0 then
                ( fileWatchList , "" )
              else
                let line = outReader.ReadLine()
                if line.Length > 1000 then
                  ( fileWatchList , line )
                else
                  match checkPathExistsLocally line with
                  | None ->
                      ( fileWatchList , line )
                  | Some path ->
                      IO.logfType fullTypeName "Add file from command output to watch list: %s" path
                      checkLine ( path :: fileWatchList )

            let fileWatchList , firstNonFileLine = checkLine initialFileWatchList
            let css =
              if outReader.Peek() < 0 then firstNonFileLine
              else firstNonFileLine + "\n" + outReader.ReadToEnd()

            if css.Length > 200 then
              IO.logfType fullTypeName "Output (200 of %i chars): %s" css.Length ( css.Substring( 0 , 200 ) )
            else
              IO.logfType fullTypeName "Output: %s" css

            if ( cfg.IsInvalidationSupported && not ( List.isEmpty fileWatchList ) ) then
              tp.SetFileToWatch( fullTypeName , List.distinct fileWatchList )

            css

          else
            IO.logType fullTypeName "Timed out waiting for process to end"
            try p.Kill() with ex -> IO.logfType fullTypeName "Error killing process: %A" ex
            failwithf "Command timed out: %s %s" pinfo.FileName pinfo.Arguments

        css

      try
        let sample = getTextFromProcess ()
        let parseResult =
          { Spec = parseFunc "" sample
            IsUri = false
            IsResource = false }
        parseResult

      with e ->
        failwithf "Failed executing command '%s' for '%s': %s" commandConfig.commandFile valueToBeParsedOrItsUri e.Message

    let private providedTypesCache = createInMemoryCache (TimeSpan.FromSeconds 30.0)
    let private activeDisposeActions = HashSet<_>()

    // Cache generated types for a short time, since VS invokes the TP multiple tiems
    // Also cache temporarily during partial invalidation since the invalidation of one TP always causes invalidation of all TPs
    let internal getOrCreateProvidedType (cfg: TypeProviderConfig) (tp:DisposableTypeProviderForNamespaces) (fullTypeName:string) f =

        let fullKey = (fullTypeName, cfg.RuntimeAssembly, cfg.ResolutionFolder, cfg.SystemRuntimeAssemblyVersion)

        let setupDisposeAction providedType filesToWatch =

            if activeDisposeActions.Add(fullTypeName, tp.Id) then

                IO.logType fullTypeName "Setting up dispose action"

                let watcher =
                    match filesToWatch with
                    | Some files when not ( List.isEmpty files ) ->
                        let name = sprintf "%s [%d]" fullTypeName tp.Id
                        // Hold a weak reference to the type provider instance.  If the TP instance is leaked
                        // and not held strongly by anyone else, then don't hold it strongly here.
                        let tpref = WeakReference<_>(tp)
                        let invalidateAction action path =
                            match tpref.TryGetTarget() with
                            | true, tp ->
                              IO.logfType fullTypeName "Invalidating %s - file %s: %s" name action path
                              tp.InvalidateOneType(fullTypeName)
                            | _ ->
                              IO.logfType fullTypeName "No invalidation target for %s - file %s: %s" name action path
                              ()
                        Some (watchForChanges fullTypeName files (name, invalidateAction))
                    | _ -> None

                // On disposal of one of the types, remove that type from the cache, and add all others to the cache
                tp.AddDisposeAction <| fun typeNameBeingDisposedOpt ->

                    // might be called more than once for each watcher, but the Dispose action is a NOP the second time
                    watcher |> Option.iter (fun watcher -> watcher.Dispose())

                    match typeNameBeingDisposedOpt with
                    | Some typeNameBeingDisposed when fullTypeName = typeNameBeingDisposed ->
                        providedTypesCache.Remove(fullTypeName)
                        IO.logfType fullTypeName "Dropping dispose action for %s [%d]" fullTypeName tp.Id
                        // for the case where a file used by two TPs, when the file changes
                        // there will be two invalidations: A and B
                        // when the dispose action is called with A, A is removed from the cache
                        // so we need to remove the dispose action so it will won't be added when disposed is called with B
                        true
                    | Some typeNameBeingDisposed ->
                        IO.logfType fullTypeName "Caching %s [%d] during dispose of other type %s" fullTypeName tp.Id typeNameBeingDisposed
                        providedTypesCache.Set(fullTypeName, (providedType, fullKey, filesToWatch))
                        // for the case where a file used by two TPs, when the file changes
                        // there will be two invalidations: A and B
                        // when the dispose action is called with A, B is added to the cache
                        // so we need to keep the dispose action around so it will be called with B and the cache is removed
                        false
                    | _ ->
                        IO.logfType fullTypeName "Caching %s [%d] during type provider dispose" fullTypeName tp.Id
                        providedTypesCache.Set(fullTypeName, (providedType, fullKey, filesToWatch))
                        // for the case where a file used by two TPs, when the file changes
                        // there will be two invalidations: A and B
                        // when the dispose action is called with A, B is added to the cache
                        // so we need to keep the dispose action around so it will be called with B and the cache is removed
                        false

        match providedTypesCache.TryRetrieve(fullTypeName, true) with
        | Some (providedType, fullKey2, watchedFile) when fullKey = fullKey2 ->
            IO.logType fullTypeName "Retrieve from cache"
            setupDisposeAction providedType watchedFile
            providedType
        | _ ->
            let providedType = f()
            IO.logType fullTypeName "Create new type and add to cache"
            let filesToWatch = tp.GetFileToWatch(fullTypeName)
            providedTypesCache.Set(fullTypeName, (providedType, fullKey, filesToWatch))
            setupDisposeAction providedType filesToWatch
            providedType

    type Source =
    | Sample of string
    | SampleList of string
    | Schema of string

    /// Creates all the constructors for a type provider: (Async)Parse, (Async)Load, (Async)GetSample(s), and default constructor
    /// * source - the sample/sample list/schema from which the types will be generated
    /// * getSpec - receives the file/url extension (or ""  if not applicable) and the text value of the sample or schema
    /// * tp - the type provider
    /// * cfg - the type provider config
    /// * encodingStr - the encoding to be used when reading the sample or schema
    /// * resolutionFolder -> if the type provider allows to override the resolutionFolder pass it here
    /// * resource - when specified, we first try to treat read the sample from an embedded resource
    ///     (the value specifies assembly and resource name e.g. "MyCompany.MyAssembly, some_resource.json")
    /// * fullTypeName - the full name of the type provider, this will be used as the caching key
    /// * maxNumberOfRows - the max number of rows to read from the sample or schema
    let generateType_orig formatName source getSpec
                     (tp:DisposableTypeProviderForNamespaces) (cfg:TypeProviderConfig)
                     encodingStr resolutionFolder resource fullTypeName maxNumberOfRows  =

        getOrCreateProvidedType cfg tp fullTypeName <| fun () ->

        let isRunningInFSI = cfg.IsHostedExecution
        let defaultResolutionFolder = cfg.ResolutionFolder

        let valueToBeParsedOrItsUri =
            match source with
            | Sample value -> value
            | SampleList value -> value
            | Schema value -> value

        let parseResult =
            parseTextAtDesignTime valueToBeParsedOrItsUri getSpec formatName tp cfg encodingStr resolutionFolder resource fullTypeName maxNumberOfRows

        let spec = parseResult.Spec

        let resultType = spec.RepresentationType
        let resultTypeAsync = typedefof<Async<_>>.MakeGenericType(resultType)

        //using (IO.logTime "CommonTypeGeneration" valueToBeParsedOrItsUri) <| fun _ ->

        [ // Generate static Parse method
          let args = [ ProvidedParameter("text", typeof<string>) ]
          let m = ProvidedMethod("Parse", args, resultType, isStatic = true,
                                    invokeCode  = fun (Singleton text) ->
                                        <@ new StringReader(%%text) :> TextReader @>
                                        |> spec.CreateFromTextReader )
          m.AddXmlDoc <| sprintf "Parses the specified %s string" formatName
          yield m :> MemberInfo

          // Generate static Load stream method
          let args = [ ProvidedParameter("stream", typeof<Stream>) ]
          let m = ProvidedMethod("Load", args, resultType, isStatic = true,
                                    invokeCode = fun (Singleton stream) ->
                                        <@ new StreamReader(%%stream:Stream) :> TextReader @>
                                        |> spec.CreateFromTextReader)
          m.AddXmlDoc <| sprintf "Loads %s from the specified stream" formatName
          yield m :> _

          // Generate static Load reader method
          let args = [ ProvidedParameter("reader", typeof<TextReader>) ]
          let m = ProvidedMethod("Load", args, resultType, isStatic = true,
                                    invokeCode = fun (Singleton reader) ->
                                        let reader = reader |> Expr.Cast
                                        reader |> spec.CreateFromTextReader)
          m.AddXmlDoc <| sprintf "Loads %s from the specified reader" formatName
          yield m :> _

          // Generate static Load uri method
          let args = [ ProvidedParameter("uri", typeof<string>) ]
          let m = ProvidedMethod("Load", args, resultType, isStatic = true,
                                    invokeCode = fun (Singleton uri) ->
                                         <@ Async.RunSynchronously(asyncReadTextAtRuntime isRunningInFSI defaultResolutionFolder resolutionFolder formatName encodingStr %%uri) @>
                                         |> spec.CreateFromTextReader)
          m.AddXmlDoc <| sprintf "Loads %s from the specified uri" formatName
          yield m :> _

          // Generate static AsyncLoad uri method
          let args = [ ProvidedParameter("uri", typeof<string>) ]
          let m = ProvidedMethod("AsyncLoad", args, resultTypeAsync, isStatic = true,
                                     invokeCode = fun (Singleton uri) ->
                                         let readerAsync = <@ asyncReadTextAtRuntime isRunningInFSI defaultResolutionFolder resolutionFolder formatName encodingStr %%uri @>
                                         asyncMap resultType readerAsync spec.CreateFromTextReader)
          m.AddXmlDoc <| sprintf "Loads %s from the specified uri" formatName
          yield m :> _

          if not parseResult.IsResource then

              match source with
              | SampleList _ ->

                  // the [][] case needs more work, and it's a weird scenario anyway, so we won't support it
                  if not resultType.IsArray then

                      let resultTypeArray = resultType.MakeArrayType()
                      let resultTypeArrayAsync = typedefof<Async<_>>.MakeGenericType(resultTypeArray)

                      // Generate static GetSamples method
                      let m = ProvidedMethod("GetSamples", [], resultTypeArray, isStatic = true,
                                                invokeCode = fun _ ->
                                                  if parseResult.IsUri
                                                  then <@ Async.RunSynchronously(asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder formatName encodingStr valueToBeParsedOrItsUri) @>
                                                  else <@ new StringReader(valueToBeParsedOrItsUri) :> TextReader @>
                                                  |> spec.CreateFromTextReaderForSampleList)
                      yield m :> _

                      if parseResult.IsUri  then
                          // Generate static AsyncGetSamples method
                          let m = ProvidedMethod("AsyncGetSamples", [], resultTypeArrayAsync, isStatic = true,
                                                    invokeCode = fun _ ->
                                                      let readerAsync = <@ asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder formatName encodingStr valueToBeParsedOrItsUri @>
                                                      spec.CreateFromTextReaderForSampleList
                                                      |> asyncMap resultTypeArray readerAsync)
                          yield m :> _

              | Sample _ ->

                  let name = if resultType.IsArray then "GetSamples" else "GetSample"
                  let getSampleCode _ =
                      if parseResult.IsUri
                      then <@ Async.RunSynchronously(asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder formatName encodingStr valueToBeParsedOrItsUri) @>
                      else <@ new StringReader(valueToBeParsedOrItsUri) :> TextReader @>
                      |> spec.CreateFromTextReader

                  // Generate static GetSample method
                  yield ProvidedMethod(name, [], resultType, isStatic = true, invokeCode = getSampleCode) :> _

                  if spec.GeneratedType :> Type = spec.RepresentationType then
                      // Generate default constructor
                      yield ProvidedConstructor([], invokeCode = getSampleCode) :> _

                  if parseResult.IsUri then
                      // Generate static AsyncGetSample method
                      let m = ProvidedMethod("Async" + name, [], resultTypeAsync, isStatic = true,
                                                invokeCode = fun _ ->
                                                  let readerAsync = <@ asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder formatName encodingStr valueToBeParsedOrItsUri @>
                                                  asyncMap resultType readerAsync spec.CreateFromTextReader)
                      yield m :> _

              | Schema _ ->
                  let getSchemaCode _ =
                      if parseResult.IsUri
                      then <@ Async.RunSynchronously(asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder formatName encodingStr valueToBeParsedOrItsUri) @>
                      else <@ new StringReader(valueToBeParsedOrItsUri) :> TextReader @>
                      |> spec.CreateFromTextReaderForSampleList // hack: this will actually parse the schema

                  // Generate static GetSchema method
                  yield ProvidedMethod("GetSchema", [], typeof<System.Xml.Schema.XmlSchemaSet>, isStatic = true,
                    invokeCode = getSchemaCode) :> _


        ] |> spec.GeneratedType.AddMembers

        spec.GeneratedType

    // Modified version of original generateType function to process CSS and create type
    let generateType formatName source getSpec ( commandConfig : RunCommandConfig option )
                     (tp:DisposableTypeProviderForNamespaces) (cfg:TypeProviderConfig)
                     encodingStr resolutionFolder resource fullTypeName maxNumberOfRows  =

        using ( IO.logTimeType fullTypeName "GenerateType" fullTypeName ) <| fun _ ->

        let createProvidedTypeFromData () =

          let valueToBeParsedOrItsUri =
              match source with
              | Sample value -> value
              | SampleList value -> value
              | Schema value -> value

          let parseResult =
            match commandConfig with
            | Some commandConfig ->
                runCommandAtDesignTime valueToBeParsedOrItsUri commandConfig getSpec formatName tp cfg encodingStr resolutionFolder resource fullTypeName maxNumberOfRows
            | None ->
                parseTextAtDesignTime valueToBeParsedOrItsUri getSpec formatName tp cfg encodingStr resolutionFolder resource fullTypeName maxNumberOfRows

          let spec = parseResult.Spec

          let resultType = spec.RepresentationType
          let resultTypeAsync = typedefof<Async<_>>.MakeGenericType(resultType)

          spec.GeneratedType

        try
          getOrCreateProvidedType cfg tp fullTypeName createProvidedTypeFromData
        with ex ->
          IO.logfType fullTypeName "Error: %A" ex
          reraise ()


#if INTERNALS_VISIBLE
open System.Runtime.CompilerServices
[<assembly:InternalsVisibleTo("TypedCssClasses.Tests")>]
do()
#endif
