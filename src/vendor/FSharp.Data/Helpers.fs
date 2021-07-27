//
// Adapted from FSharp.Data for Zanaptak.TypedCssClasses by zanaptak.
//

// Copyright 2011-2015, Tomas Petricek (http://tomasp.net), Gustavo Guerra (http://functionalflow.co.uk), and other contributors
// Licensed under the Apache License, Version 2.0, see LICENSE.md in this project
//
// Helpers for writing type providers

namespace Zanaptak.TypedCssClasses.Internal.FSharpData

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.Web
open FSharp.Core.CompilerServices
open Zanaptak.TypedCssClasses.Internal.FSharpData.Caching
open Zanaptak.TypedCssClasses.Internal.FSharpData.IO
open Zanaptak.TypedCssClasses.Internal.ProviderImplementation.ProvidedTypes

module ProvidedTypes =

    type Property = { Name : string ; Value : string }

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
                using (logTime "DisposingEvent" (sprintf "%O [%d]" x id)) <| fun _ ->
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
            dispose (Some typeName)
            logfType typeName id "Calling invalidate for %O [%d]" x id
            base.Invalidate()

        override x.Finalize() =
            log (sprintf "Finalize %O [%d]" x id)

module internal Helpers =
    open ProvidedTypes

    /// Helper active pattern that can be used when constructing InvokeCode
    /// (to avoid writing pattern matching or incomplete matches):
    ///
    ///    p.InvokeCode <- fun (Singleton self) -> <@ 1 + 2 @>
    ///
    let (|Singleton|) = function [l] -> l | _ -> failwith "Parameter mismatch"

    let private cacheDuration = TimeSpan.FromDays 90.
    let private webUrisCache = createInternetFileCache "Zanaptak.TypedCssClasses" cacheDuration
    let private invalidPathChars = Set.ofArray ( Path.GetInvalidPathChars() )
    let private invalidFileChars = Set.ofArray ( Path.GetInvalidFileNameChars() )

    let private isValidFilenameSyntax str =
        if
            String.IsNullOrWhiteSpace str
            || Seq.exists invalidPathChars.Contains str
            || String.IsNullOrWhiteSpace( Path.GetFileName str )
            || Seq.exists invalidFileChars.Contains ( Path.GetFileName str )
        then false
        else true

    let private tryGetUri fullTypeName tpInstance str =
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
            logfType fullTypeName tpInstance "tryGetUri error, assuming non file: %O" ex
            None

    /// If relative file URI, resolves to absolute.
    /// Returns absolute URI and flag indicating whether it is a web URI.
    let private resolveUri resolutionFolder ( uri : Uri ) =
        if uri.IsAbsoluteUri then
            uri, isWeb uri
        else
            Uri( Path.GetFullPath( Path.Combine( resolutionFolder , uri.OriginalString ) ) , UriKind.Absolute ) , false

    /// Check if file exists and return absolute path if so
    let private tryFilePathExists fullTypeName tpInstance resolutionFolder path =
        match tryGetUri fullTypeName tpInstance path with
        | None -> None
        | Some uri ->
            try
                match resolveUri resolutionFolder uri with
                | uri , false ->
                    // non-web uri, check if exists
                    let resolvedPath = pathFromFileUri uri
                    if File.Exists resolvedPath then Some resolvedPath else None
                | _ -> None // web uri
            with
            | ex ->
                logfType fullTypeName tpInstance "resolver error, assuming non file: %A" ex
                None // in case of any IO error on resolve or existence check

    type RunCommandConfig = {
        commandFile : string
        argumentPrefix : string
        argumentSuffix : string
    }

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
    let private parseTextAtDesignTime
        valueToBeParsedOrItsUri
        (tp:DisposableTypeProviderForNamespaces)
        (cfg:TypeProviderConfig)
        resolutionFolder
        fullTypeName
        tryParseText =

        using (logTimeType fullTypeName tp.Id "LoadTextFromSource" fullTypeName) <| fun _ ->

        let formatName = "CSS"
        let parseDefaultEmpty x = tryParseText x |> Option.defaultValue [||]

        match tryGetUri fullTypeName tp.Id valueToBeParsedOrItsUri with
        | None -> parseDefaultEmpty valueToBeParsedOrItsUri , []
        | Some uri ->

            let resolver = {
                ResolutionType = DesignTime
                DefaultResolutionFolder = cfg.ResolutionFolder
                ResolutionFolder = resolutionFolder
            }

            let readText() =
                let reader, toWatch = asyncRead fullTypeName tp.Id resolver formatName "" uri
                use reader = reader |> Async.RunSynchronously
                reader.ReadToEnd() , toWatch |> Option.map ( fun s -> [ s ] ) |> Option.defaultValue []

            try

                let sample , fileWatchList =
                    if isWeb uri then
                        match webUrisCache.TryRetrieve(uri.OriginalString) with
                        | Some text ->
                            logfType fullTypeName tp.Id "Web URI retrieved from cache: %s" uri.OriginalString
                            text , []
                        | None ->
                            let text , fileWatchList = readText()
                            webUrisCache.Set(uri.OriginalString, text)
                            text , fileWatchList
                    else
                        readText()

                parseDefaultEmpty sample , fileWatchList

            with _ ->
                // File read failed, try as text instead, if this also fails we'll reraise the original file exception
                let attemptedTextParseResult =
                    if not uri.IsAbsoluteUri then
                        logfType fullTypeName tp.Id "File read failed, attempting processing as text"
                        try
                            // Returns None if text parse failed to match any CSS; in that case we'll assume it was a failed file uri
                            tryParseText valueToBeParsedOrItsUri
                        with _ -> None
                    else None

                match attemptedTextParseResult with
                | Some parseResult -> parseResult , []
                | None -> reraise ()

    /// Runs the command supplied to the TP and uses the output of the command as the source sample.
    ///
    /// Parameters:
    /// * source - file path to use as main argument for command
    /// * commandConfig - configuration for command to run and its arguments
    /// * parseFunc - receives the file/url extension (or ""  if not applicable) and the text value
    /// * tp - the type provider
    /// * cfg - the type provider config
    /// * resolutionFolder - if the type provider allows to override the resolutionFolder pass it here
    let private runCommandAtDesignTime
        source
        ( commandConfig : RunCommandConfig )
        (tp:DisposableTypeProviderForNamespaces)
        (cfg:TypeProviderConfig)
        resolutionFolder
        fullTypeName =

        using (logTimeType fullTypeName tp.Id "RunCommand" fullTypeName) <| fun _ ->

        let initialFileWatchList =
            match tryFilePathExists fullTypeName tp.Id resolutionFolder source with
            | None -> []
            | Some path -> [ path ]

        let stdoutSb = StringBuilder()
        let stderrSb = StringBuilder()

        let pinfo = ProcessStartInfo( commandConfig.commandFile )
        pinfo.UseShellExecute <- false
        pinfo.RedirectStandardInput <- false
        pinfo.RedirectStandardOutput <- true
        pinfo.RedirectStandardError <- true
        pinfo.CreateNoWindow <- true
        pinfo.Arguments <-
            [ commandConfig.argumentPrefix ; source ; commandConfig.argumentSuffix ]
            |> List.filter ( fun arg -> arg <> "" )
            |> String.concat " "
        pinfo.WorkingDirectory <- resolutionFolder
        logfType fullTypeName tp.Id "Process filename: %s" pinfo.FileName
        logfType fullTypeName tp.Id "Arguments: %s" pinfo.Arguments
        logfType fullTypeName tp.Id "Working directory: %s" pinfo.WorkingDirectory

        use p = new Process()
        p.StartInfo <- pinfo
        p.OutputDataReceived.Add( fun eventArgs ->
            if not ( isNull eventArgs.Data ) then
                if stdoutSb.Length = 0 then logType fullTypeName tp.Id "StandardOutput data started"
                stdoutSb.AppendLine( eventArgs.Data ) |> ignore
        )
        p.ErrorDataReceived.Add( fun eventArgs ->
            if not ( isNull eventArgs.Data ) then
                if stderrSb.Length = 0 then logType fullTypeName tp.Id "StandardError data started"
                stderrSb.AppendLine( eventArgs.Data ) |> ignore
        )

        p.Start() |> ignore
        p.BeginOutputReadLine()
        p.BeginErrorReadLine()
        logType fullTypeName tp.Id "Process started"

        if p.WaitForExit( 60000 ) then
            logType fullTypeName tp.Id "Process completed"

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
                        match tryFilePathExists fullTypeName tp.Id resolutionFolder line with
                        | None ->
                            ( fileWatchList , line )
                        | Some path ->
                            logfType fullTypeName tp.Id "Add file from command output to watch list: %s" path
                            checkLine ( path :: fileWatchList )

            let fileWatchList , firstNonFileLine = checkLine initialFileWatchList
            let css =
                if outReader.Peek() < 0 then firstNonFileLine
                else firstNonFileLine + "\n" + outReader.ReadToEnd()

            let maxLen = 50
            if css.Length > maxLen then
                logfType fullTypeName tp.Id "Output (%i of %i chars): %s" maxLen css.Length ( css.Substring( 0 , maxLen ) )
            else
                logfType fullTypeName tp.Id "Output: %s" css

            css , fileWatchList

        else
            logType fullTypeName tp.Id "Timed out waiting for process to end"
            try p.Kill() with ex -> logfType fullTypeName tp.Id "Error killing process: %O" ex
            failwithf "Command timed out: %s %s" pinfo.FileName pinfo.Arguments

    let private parseResultCache : ICache< Property array * string list > = createInMemoryCache (TimeSpan.FromHours 2.)
    let private providedTypesCache = createInMemoryCache (TimeSpan.FromSeconds 30.0)
    let private activeDisposeActions = HashSet<_>()

    // Cache generated types for a short time, since VS invokes the TP multiple tiems
    // Also cache temporarily during partial invalidation since the invalidation of one TP always causes invalidation of all TPs
    let internal getOrCreateProvidedType (cfg: TypeProviderConfig) (tp:DisposableTypeProviderForNamespaces) (fullTypeName:string) createTypeFn cacheKey =

        let fullKey = (fullTypeName, cfg.RuntimeAssembly, cfg.ResolutionFolder, cfg.SystemRuntimeAssemblyVersion)
        let tpInstance = tp.Id

        let setupDisposeAction filesToWatch =

            if activeDisposeActions.Add(fullTypeName, tp.Id) then

                logfType fullTypeName tpInstance "Setting up dispose action"

                let watcherDisposer =
                    match filesToWatch with
                    | Some files when not ( List.isEmpty files ) ->
                        let name = sprintf "%s [%d]" fullTypeName tp.Id
                        // Hold a weak reference to the type provider instance.  If the TP instance is leaked
                        // and not held strongly by anyone else, then don't hold it strongly here.
                        let tpref = WeakReference<_>(tp)
                        let invalidateAction action path =
                            match tpref.TryGetTarget() with
                            | true, tp ->
                                logfType fullTypeName tpInstance "File change: %s - Invalidate type: %s" path fullTypeName
                                tp.InvalidateOneType(fullTypeName)
                            | _ ->
                                logfType fullTypeName tpInstance "File change: %s - No TP reference to invalidate" path
                                ()
                        Some (watchForChanges fullTypeName tpInstance files (name, invalidateAction))
                    | _ -> None

                // On disposal of one of the types, remove that type from the cache, and add all others to the cache
                tp.AddDisposeAction <| fun typeNameBeingDisposedOpt ->

                    // All dispose actions for all types run when any type invalidated (and then new instance of main TP created).
                    // If multiple types invalidated, all actions run for each invalidation (unless action removes itself).
                    // Dispose action of invalidated type clears watchers and caches for that type and removes the dispose action so it doesn't rerun.
                    // Dispose action of non-invalidated type only clears watchers, and leaves action in place in case invalidation of that type is still in queue.
                    // Change from FSharp.Data: Don't cache types on invalidation. Next creation should be cheap due to the internal parse result cache.

                    using (logTimeType fullTypeName tpInstance "DisposeAction" fullTypeName) <| fun _ ->
                    logfType fullTypeName tpInstance "Invalidated type: %s" ( typeNameBeingDisposedOpt |> Option.defaultValue "" )

                    // might be called more than once for each watcher, but the Dispose action is a NOP the second time
                    watcherDisposer |> Option.iter (fun watcher -> watcher.Dispose())

                    match typeNameBeingDisposedOpt with
                    | Some typeNameBeingDisposed when fullTypeName = typeNameBeingDisposed ->
                        logfType fullTypeName tpInstance "Removing parse result from cache, key=%s" cacheKey
                        parseResultCache.Remove cacheKey
                        logfType fullTypeName tpInstance "Removing type from cache and dropping dispose action"
                        providedTypesCache.Remove(fullTypeName)
                        // for the case where a file used by two TPs, when the file changes
                        // there will be two invalidations: A and B
                        // when the dispose action is called with A, A is removed from the cache
                        // so we need to remove the dispose action so it will won't be added when disposed is called with B
                        true
                    | _ ->
                        logfType fullTypeName tpInstance "Keeping dispose action during invalidation of other type"
                        // for the case where a file used by two TPs, when the file changes
                        // there will be two invalidations: A and B
                        // when the dispose action is called with A, B is added to the cache
                        // so we need to keep the dispose action around so it will be called with B and the cache is removed
                        false

        match providedTypesCache.TryRetrieve(fullTypeName, true) with
        | Some (providedType, fullKey2, watchedFile) when fullKey = fullKey2 ->
            logType fullTypeName tpInstance "Retrieve type from cache"
            setupDisposeAction watchedFile
            providedType
        | _ ->
            let providedType = createTypeFn()
            let filesToWatch = tp.GetFileToWatch(fullTypeName)
            logType fullTypeName tpInstance "Add type to cache"
            providedTypesCache.Set(fullTypeName, (providedType, fullKey, filesToWatch))
            setupDisposeAction filesToWatch
            providedType

    // Modified version of original generateType function to process CSS and create type
    let generateType
        source
        ( commandConfig : RunCommandConfig option )
        (tp:DisposableTypeProviderForNamespaces)
        (cfg:TypeProviderConfig)
        resolutionFolder
        fullTypeName
        tryParseText
        ( createType : Property array -> ProvidedTypeDefinition ) =

        using ( logTimeType fullTypeName tp.Id "GenerateType" fullTypeName ) <| fun _ ->

        let cacheKey =
            match commandConfig with
            | Some commandConfig ->
                let keyParts =
                    [
                        commandConfig.commandFile
                        ; ( [ commandConfig.argumentPrefix ; source ; commandConfig.argumentSuffix ] |> List.filter ( fun arg -> arg <> "" ) |> String.concat " " )
                        ; resolutionFolder
                    ]
                    |> List.map ( fun s -> HttpUtility.JavaScriptStringEncode( s , true ) )
                sprintf "Process:%s,Arguments:%s,Directory:%s" keyParts.[ 0 ] keyParts.[ 1 ] keyParts.[ 2 ]
            | None ->
                match tryGetUri fullTypeName tp.Id source with
                | Some uri ->
                    let uri , isWeb = resolveUri resolutionFolder uri
                    if isWeb then
                        "URI:" + HttpUtility.JavaScriptStringEncode( uri.OriginalString , true )
                    else
                        "File:" + HttpUtility.JavaScriptStringEncode( pathFromFileUri uri , true )
                | None ->
                    "Text:" + HttpUtility.JavaScriptStringEncode( source , true )

        let createProvidedTypeFromData () =

            let parseResult , fileWatchList =
                match parseResultCache.TryRetrieve( cacheKey , true ) with
                | Some propertiesAndFiles ->
                    logfType fullTypeName tp.Id "Retrieve parse result from cache, key=%s" cacheKey
                    propertiesAndFiles
                | None ->

                    let parsedProperties , files =
                        match commandConfig with
                        | Some commandConfig ->
                            let text , files = runCommandAtDesignTime source commandConfig tp cfg resolutionFolder fullTypeName
                            tryParseText text |> Option.defaultValue [||] , files
                        | None -> parseTextAtDesignTime source tp cfg resolutionFolder fullTypeName tryParseText

                    logfType fullTypeName tp.Id "Add parse result to cache, key=%s" cacheKey
                    parseResultCache.Set( cacheKey , ( parsedProperties , files )  )

                    parsedProperties , files

            logfType fullTypeName tp.Id "Parsed CSS class count: %i" parseResult.Length

            if ( cfg.IsInvalidationSupported && not ( List.isEmpty fileWatchList ) ) then
                tp.SetFileToWatch( fullTypeName , List.distinct fileWatchList )

            createType parseResult

        try
            getOrCreateProvidedType cfg tp fullTypeName createProvidedTypeFromData cacheKey
        with ex ->
            logfType fullTypeName tp.Id "Error: %O" ex
            reraise ()
