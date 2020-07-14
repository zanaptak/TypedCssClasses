# TypedCssClasses

A CSS class type provider for F# web development.

Bring external stylesheet classes into your F# code as design-time discoverable compiler-verified properties.

## Code examples

The following examples show how to define a type pointing at a local or remote CSS file. The type's properties can then be used, with editor completion, wherever CSS class name strings would typically be used. The syntax shown is [Fable.React](https://fable.io/blog/Announcing-Fable-React-5.html) view syntax, but any web framework can be used because the provided properties just compile to the underlying class names as strings.

### Bootstrap CSS

```fs
// A "Bootstrap" type pointing at a remote Bootstrap CSS file.
type Bootstrap = CssClasses<"https://stackpath.bootstrapcdn.com/bootstrap/4.3.1/css/bootstrap.min.css", Naming.PascalCase>

// All CSS classes become Bootstrap.* properties, with design-time completion. 
div [ ClassName Bootstrap.Card ] [
    div [ ClassName Bootstrap.CardBody ] [
        h5 [ ClassName Bootstrap.CardTitle ] [ str "A clever title" ]
        p [ ClassName Bootstrap.CardText ] [ str "Lorem ipsum dolor sit amet." ]
    ]
]
```

### Font Awesome CSS

```fs
// An "FA" type pointing at a local Font Awesome CSS file.
type FA = CssClasses<"static/font-awesome/css/all.css", Naming.Underscores> 

// All CSS classes become icon.* properties, with design-time completion. 
i [ classList [
    FA.far, true
    FA.fa_grin, true ] ] []

i [ classList [
    FA.fas, true
    FA.fa_thumbs_up, true ] ] []
```

### Tailwind CSS

```fs
// A "tw" type pointing at a remote Tailwind CSS file.
type tw = CssClasses<"https://unpkg.com/tailwindcss@^1.0/dist/tailwind.min.css", Naming.Verbatim>

// All CSS classes become tw.* properties, with design-time completion. 
div [ ClassName <| String.concat " " [
        tw.``bg-red-200``
        tw.``hover:bg-red-500``
        tw.``hover:font-bold``

        tw.``sm:text-2xl``
        tw.``sm:bg-green-200``
        tw.``sm:hover:bg-green-500``

        tw.``lg:text-4xl``
        tw.``lg:bg-blue-200``
        tw.``lg:hover:bg-blue-500`` ]
] [ str "Resize me! Hover me!" ]
```

## Samples

Check out the [Fable Tailwind sample](https://github.com/zanaptak/TypedCssClasses/tree/master/sample/FableTailwind) to see it in action. Demonstrates the use of a local Tailwind CSS setup that is customized, purged, and minified.

## Getting started

Add the [NuGet package](https://www.nuget.org/packages/Zanaptak.TypedCssClasses) to your project:
```
dotnet add package Zanaptak.TypedCssClasses
```

If the project was already open in an IDE, you might want to close and restart it to make sure the type provider loads into the process correctly.

Write some code:
```fs
open Zanaptak.TypedCssClasses

// Define a type for a CSS source.
// Can be file path, web URL, or CSS text.
type css = CssClasses<"https://stackpath.bootstrapcdn.com/bootstrap/4.3.1/css/bootstrap.min.css">

// Enjoy your typed properties!
let x = css.``display-1``
```

Works with Visual Studio Code (with Ionide-fsharp extension) on Windows and Linux, and Visual Studio 2019 on Windows. (Other environments/IDEs have not been tested by me.)

## External command support for CSS preprocessing

The internal logic of this type provider requires standard CSS syntax to extract class names from. For alternate syntaxes (such as Sass/SCSS), you can specify an external command to compile the source into standard CSS with the `commandFile` parameter:

```
// Assuming a "sass" command is available in PATH.
type css =
  CssClasses<
    "example.sass"
    , commandFile="sass"
  >
```

The command can be an executable file in your PATH, a file path relative to the resolution folder (see [`resolutionFolder`](#resolutionfolder) parameter), or an absolute path. The working directory of the process will be the resolution folder of the type provider. On Windows, the .exe extension can be omitted, but other extensions such as .cmd/.bat must be specified.

The `source` parameter will be passed as an argument to the command. The standard output returned by the command will be used as the CSS data for extracting class names. A non-zero exit code returned by the command indicates an error and the standard error text will be reported as an exception.

In addition to the CSS output, the command can optionally return leading lines to indicate additional files to watch beyond the `source` parameter (see [File watching](#file-watching) section below), such as Sass partials. Any initial full line that exactly matches a local path will be interpreted as a file to watch; the type provider will only start processing CSS on the first line that doesn't match a local path. You would likely accomplish this via custom scripts; see the sample projects for examples.

Two additional parameters are available to further customize the command: `argumentPrefix` and `argumentSuffix`. If specified, these strings will be placed before and after the `source` argument, separated by a space. That is, the full command will be the result of the following parameters:

```
commandFile [argumentPrefix ][source][ argumentSuffix]
```

Source and arguments are concatenated as-is (after OS-specific and environment variable processing described below); you are responsible for any quoting or escaping if necessary.

## Multiple development environments

To support development in different environments, you can include operating system-specific alternatives in parameters using comma-separated OS=value pairs after an initially-specified default, in the form of `defaultvalue,OS1=value1,OS2=value2`:

```
// Global npm-installed sass on Windows uses "sass.cmd". Use that on Windows, plain "sass" everywhere else.
type css =
  CssClasses<
    "example.sass"
    , commandFile="sass,Windows=sass.cmd"
  >
```

Supported values are (case-insensitive): `FREEBSD`, `LINUX`, `OSX`, and `WINDOWS`. (Based on [OSPlatform.Create()](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.osplatform.create?view=netstandard-2.0) as of .NET Standard 2.0.)

If this syntax conflicts with your parameter values, you can use the `osDelimiters` parameter. It takes a two-character string specifying alternatives for the comma and the equals sign in that order, or an empty string to disable OS parsing.

## Environment variables

You can use environment variables in parameters using `%VARIABLE%` syntax. Note that the Windows-style `%...%` syntax must be used, even for non-Windows systems (e.g. Linux must use `%HOME%` rather than `$HOME`). Variables not found in the environment will not be processed and the original syntax will be left in the string.

You can disable environment variable expansion by setting the `expandVariables` parameter to false.

## File watching

If the `source` parameter specifies a local file, the type provider will monitor the file and refresh the CSS classes when it changes (rerunning any specified command if applicable).

If a `commandFile` is specified, any leading lines from the output of the command that exactly specify a local file path will also be watched.

## Parameters

### source

The source CSS to process. Can be a file path, web URL, or CSS text.

### naming

* `Naming.Verbatim`: (default) use class names verbatim from source CSS, requiring backtick-quotes for names with special characters.
* `Naming.Underscores`: replace all non-alphanumeric characters with underscores.
* `Naming.CamelCase`: convert to camel case names with all non-alphanumeric characters removed.
* `Naming.PascalCase`: convert to Pascal case names with all non-alphanumeric characters removed.

Note that non-verbatim naming options can produce name collisions. See the [`nameCollisions`](#nameCollisions) parameter for details.

### resolutionFolder

A custom folder to use for resolving relative file paths. The default is the project root folder.

To have nested code files referencing CSS files in the same directory without having to specify the entire path from project root, you can use the built-in F# `__SOURCE_DIRECTORY__` value:

```fs
type css = CssClasses<"file-in-same-dir.css", resolutionFolder=__SOURCE_DIRECTORY__>
```

### getProperties

If true, the type will include a `GetProperties()` method that returns a sequence of the generated property name/value pairs. You could use this, for example, to produce a simple hard-coded standalone set of bindings by running the following `.fsx` script:

```fs
// NOTE: update this reference to a valid assembly location on your machine
#r "YOUR_USER_HOME_DIRECTORY/.nuget/packages/zanaptak.typedcssclasses/0.1.0/lib/netstandard2.0/Zanaptak.TypedCssClasses.dll"
open Zanaptak.TypedCssClasses
type css = CssClasses<"https://cdnjs.cloudflare.com/ajax/libs/materialize/1.0.0/css/materialize.min.css", Naming.CamelCase, getProperties=true>
css.GetProperties()
|> Seq.sortBy (fun p -> p.Name)
|> Seq.iter (fun p -> printfn "let %s = \"%s\"" p.Name p.Value)
```

Example output:
```
let accent1 = "accent-1"
let accent2 = "accent-2"
let accent3 = "accent-3"
let accent4 = "accent-4"
let activator = "activator"
...
```

### nameCollisions

If a naming option produces collisions, such as `card-text` and `card_text` CSS classes both mapping to a `CardText` property in Pascal case, then the duplicate names will be handled according to this option.

- `NameCollisions.BasicSuffix`: (default) The base property name will refer to the closest text match, and additional properties will receive `_2`, `_3`, etc. suffixes as needed. Note that if this option is used during ongoing CSS development, it can cause existing properties to silently change to refer to different classes if collisions are introduced that affect the base name and number determination.
- `NameCollisions.ExtendedSuffix`: All property names involved in a collision will receive an extended numbered suffix such as `__1_of_2`,  `__2_of_2`. This option is safer for ongoing development since any introduced collision will change all involved names and produce immediate compiler errors where the previous names were used.
- `NameCollisions.Omit`: All colliding properties will be omitted from the generated type. This option is safer for ongoing development since any introduced collision will remove the original property and produce immediate compiler errors wherever it was used.

### logFile

Path to a log file the type provider should write to.

### commandFile

An executable file to run that will process the `source` file before extracting CSS class names. See [External command support](#external-command-support-for-css-preprocessing).

### argumentPrefix

An argument string to include before the `source` parameter, separated by a space, when running a command. Only applicable when `commandFile` is specified.

### argumentSuffix

An argument string to include after the `source` parameter, separated by a space, when running a command. Only applicable when `commandFile` is specified.

### osDelimiters

A two-character string specifying the delimiter characters used to indicate operating system-specific parameter values. Default is `,=` as in `defaultvalue,OS1=value1,OS2=value2`. If set to empty string, disables parsing for OS values.

Applies to parameters: `source`, `resolutionFolder`, `logFile`, `commandFile`, `argumentPrefix`, `argumentSuffix`

### expandVariables

Boolean to indicate whether evironment variables in the form of `%VARIABLE%` in parameter values should be expanded. Default true.

Applies to parameters: `source`, `resolutionFolder`, `logFile`, `commandFile`, `argumentPrefix`, `argumentSuffix`

## Notes

This type provider does not use formal CSS parsing, just identifying classes by typical selector patterns. It works with several major CSS frameworks but could fail on some as-yet-untested obscure CSS syntax.

Web URLs are expected to use static CDN or otherwise unchanging content and are cached on the local filesystem with a 90-day expiration.

If using Fable, update fable-compiler to version 2.3.17 or later to avoid an issue with the type provider failing to resolve relative file paths.
