//
// Modified for Zanaptak.TypedCssClasses by zanaptak.
//

/// Helper functions called from the generated code for working with files
module internal Zanaptak.TypedCssClasses.Internal.FSharp.Data.Runtime.IO

open System
open System.Collections.Generic
open System.Collections.Concurrent
open System.IO
open System.Text
open System.Diagnostics
open System.Threading
open Zanaptak.TypedCssClasses.Internal.FSharp.Data

let private logLock = obj()
let mutable private indentation = 0
let private logFileTypePaths = ConcurrentDictionary< string , string >()
let private processId = System.Diagnostics.Process.GetCurrentProcess().Id

let internal enableLogType typeName filePath =
  logFileTypePaths.AddOrUpdate( typeName , filePath , fun _ _ -> filePath ) |> ignore

let internal isLogEnabledType typeName =
  logFileTypePaths.ContainsKey typeName

let private appendToLogMultipleType typeName lines = lock logLock <| fun () ->
  match logFileTypePaths.TryGetValue typeName with
  | true , logFile ->
      use stream = File.Open(logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)
      use writer = new StreamWriter(stream)
      for (line:string) in lines do
          writer.WriteLine(line.Replace("\r", null).Replace("\n","\\n"))
      writer.Flush()
  | false , _ -> ()

let private appendToLogType typeName line =
    appendToLogMultipleType typeName [line]

let internal logType typeName str =
  if isLogEnabledType typeName then
    sprintf "%s [%i][%i] %s %s"
      ( DateTime.Now.ToString( "yyyy-MM-dd HH:mm:ss.fff" ) )
      processId
      Threading.Thread.CurrentThread.ManagedThreadId
      ( String(' ', indentation * 4) )
      str
    |> appendToLogType typeName

let internal logfType typeName fmt = Printf.kprintf ( logType typeName ) fmt

let internal logWithStackTraceType typeName (str:string) =
  if isLogEnabledType typeName then
    let stackTrace =
        Environment.StackTrace.Split '\n'
        |> Seq.skip 3
        |> Seq.truncate 5
        |> Seq.map (fun s -> s.TrimEnd())
        |> Seq.toList
    str::stackTrace |> appendToLogMultipleType typeName

let internal dummyDisposable = { new IDisposable with member __.Dispose() = () }

let internal logTimeType typeName category (instance:string) =
  if isLogEnabledType typeName then
    logfType typeName "%s %s" category instance
    Interlocked.Increment &indentation |> ignore
    let s = Stopwatch()
    s.Start()
    { new IDisposable with
        member __.Dispose() =
            s.Stop()
            Interlocked.Decrement &indentation |> ignore
            logfType typeName "Finished %s [%dms]" category s.ElapsedMilliseconds
    }
  else dummyDisposable

// Old log functions left as no-ops
let internal log str = ()
let internal logWithStackTrace (str:string) =  ()
let internal logTime category (instance:string) = dummyDisposable

type private FileWatcher(path) =

    let subscriptions = Dictionary<string, string -> string -> unit>()

    let getLastWrite() = File.GetLastWriteTime path
    let mutable lastWrite = getLastWrite()

    let watcher =
        new FileSystemWatcher(
            Filter = Path.GetFileName path,
            Path = Path.GetDirectoryName path,
            EnableRaisingEvents = true)

    let checkForChanges action _ =
        let curr = getLastWrite()

        if lastWrite <> curr then
            log (sprintf "Watcher detected file %s: %s" action path)
            lastWrite <- curr
            // creating a copy since the handler can be unsubscribed during the iteration
            let handlers = subscriptions.Values |> Seq.toArray
            for handler in handlers do
                handler action path

    do
        watcher.Changed.Add (checkForChanges "changed")
        watcher.Renamed.Add (checkForChanges "renamed")
        watcher.Deleted.Add (checkForChanges "deleted")

    member __.Subscribe(name, action) =
        subscriptions.Add(name, action)

    member __.Unsubscribe(fullTypeName, name) =
        if subscriptions.Remove(name) then
            logfType fullTypeName "Unsubscribed %s from %s watcher" name path
            if subscriptions.Count = 0 then
                logfType fullTypeName "Disposing %s watcher" path
                watcher.Dispose()
                true
            else
                false
        else
            false

let private watchers = Dictionary<string, FileWatcher>()

// sets up a filesystem watcher that calls the invalidate function whenever the file changes
let watchForChanges fullTypeName paths (owner, onChange) =

    let watcherPathSubs =

        let subbedWatchers = ResizeArray()

        lock watchers <| fun () ->
            paths |> List.iter( fun path ->
              match watchers.TryGetValue(path) with
              | true, watcher ->
                  logfType fullTypeName "Reusing %s watcher" path
                  watcher.Subscribe(owner, onChange)
                  subbedWatchers.Add ( watcher , path )
              | false, _ ->
                  logfType fullTypeName "Setting up %s watcher" path
                  let watcher = FileWatcher path
                  watcher.Subscribe(owner, onChange)
                  watchers.Add(path, watcher)
                  subbedWatchers.Add ( watcher , path )
            )

        subbedWatchers |> List.ofSeq

    { new IDisposable with
        member __.Dispose() =
            lock watchers <| fun () ->
                watcherPathSubs |> List.iter ( fun ( watcher , path ) ->
                  if watcher.Unsubscribe(fullTypeName, owner) then
                      watchers.Remove(path) |> ignore
                )
    }

type internal UriResolutionType =
    | DesignTime
    | Runtime
    | RuntimeInFSI

let internal isWeb (uri:Uri) = uri.IsAbsoluteUri && not uri.IsUnc && uri.Scheme <> "file"

type internal UriResolver =

    { ResolutionType : UriResolutionType
      DefaultResolutionFolder : string
      ResolutionFolder : string }

    static member Create(resolutionType, defaultResolutionFolder, resolutionFolder) =
      { ResolutionType = resolutionType
        DefaultResolutionFolder = defaultResolutionFolder
        ResolutionFolder = resolutionFolder }

    /// Resolve the absolute location of a file (or web URL) according to the rules
    /// used by standard F# type providers as described here:
    /// https://github.com/fsharp/fsharpx/issues/195#issuecomment-12141785
    ///
    ///  * if it is web resource, just return it
    ///  * if it is full path, just return it
    ///  * otherwise.
    ///
    ///    At design-time:
    ///      * if the user specified resolution folder, use that
    ///      * otherwise use the default resolution folder
    ///    At run-time:
    ///      * if the user specified resolution folder, use that
    ///      * if it is running in F# interactive (config.IsHostedExecution)
    ///        use the default resolution folder
    ///      * otherwise, use 'CurrentDomain.BaseDirectory'
    /// returns an absolute uri * isWeb flag
    member x.Resolve(uri:Uri) =
      let orCurrentDirIfEmpty dir =
        if String.IsNullOrWhiteSpace dir then
          Environment.CurrentDirectory
        else dir

      if uri.IsAbsoluteUri then
        uri, isWeb uri
      else
        let root =
          match x.ResolutionType with
          | DesignTime -> x.ResolutionFolder // final resolution folder already set at TP entry point
          | RuntimeInFSI -> x.DefaultResolutionFolder |> orCurrentDirIfEmpty
          | Runtime -> AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/')
        Uri( Path.GetFullPath( Path.Combine(root, uri.OriginalString) ) , UriKind.Absolute), false

/// Opens a stream to the uri using the uriResolver resolution rules
/// It the uri is a file, uses shared read, so it works when the file locked by Excel or similar tools,
/// and sets up a filesystem watcher that calls the invalidate function whenever the file changes
let internal asyncRead fullTypeName (uriResolver:UriResolver) formatName encodingStr (uri:Uri) =
  let uri, isWeb = uriResolver.Resolve uri
  if isWeb then
    async {
        let contentTypes =
            match formatName with
            | "CSV" -> [ HttpContentTypes.Csv ]
            | "HTML" -> [ HttpContentTypes.Html ]
            | "JSON" -> [ HttpContentTypes.Json ]
            | "XML" -> [ HttpContentTypes.Xml ]
            | "CSS" -> [ HttpContentTypes.Css ]
            | _ -> []
            @ [ HttpContentTypes.Any ]
        let headers = [ HttpRequestHeaders.UserAgent ("F# Data " + formatName + " Type Provider")
                        HttpRequestHeaders.Accept (String.concat ", " contentTypes) ]
        // Download the whole web resource at once, otherwise with some servers we won't get the full file
        logfType fullTypeName "Reading from web URI: %s" uri.OriginalString
        let! text = Http.AsyncRequestString(uri.OriginalString, headers = headers, responseEncodingOverride = encodingStr)
        return new StringReader(text) :> TextReader
    }, None
  else
    let path = uri.OriginalString.Replace(Uri.UriSchemeFile + "://", "")
    logfType fullTypeName "Reading from file: %s" path
    async {
        let file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        let encoding = if encodingStr = "" then Encoding.UTF8 else HttpEncodings.getEncoding encodingStr
        return new StreamReader(file, encoding) :> TextReader
    }, Some path

let private withUri uri f =
  match Uri.TryCreate(uri, UriKind.RelativeOrAbsolute) with
  | false, _ -> failwithf "Invalid uri: %s" uri
  | true, uri -> f uri

/// Returns a TextReader for the uri using the runtime resolution rules
let asyncReadTextAtRuntime forFSI defaultResolutionFolder resolutionFolder formatName encodingStr uri =
  withUri uri <| fun uri ->
    let resolver = UriResolver.Create((if forFSI then RuntimeInFSI else Runtime),
                                      defaultResolutionFolder, resolutionFolder)
    asyncRead "NOTIMPLEMENTED" resolver formatName encodingStr uri |> fst

/// Returns a TextReader for the uri using the designtime resolution rules
let asyncReadTextAtRuntimeWithDesignTimeRules defaultResolutionFolder resolutionFolder formatName encodingStr uri =
  withUri uri <| fun uri ->
    let resolver = UriResolver.Create(DesignTime, defaultResolutionFolder, resolutionFolder)
    asyncRead "NOTIMPLEMENTED" resolver formatName encodingStr uri |> fst

