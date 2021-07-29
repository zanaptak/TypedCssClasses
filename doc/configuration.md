# Zanaptak.TypedCssClasses configuration instructions

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

This capability should work with any IDE that supports type providers. (Tested on Visual Studio Code with Ionide-fsharp extension on Windows and Linux, and Visual Studio 2019 on Windows.)

## External command support for CSS preprocessing

The internal logic of this type provider requires standard CSS syntax to extract class names from. For alternate syntaxes (such as Sass/SCSS), you can specify an external command to compile the source into standard CSS with the `commandFile` parameter:

```
// Assuming a "sass" command is available in PATH.
type css = CssClasses<"example.sass", commandFile="sass">
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
// Use "sass.cmd" on Windows, "sass" everywhere else.
type css = CssClasses<"example.sass", commandFile="sass,Windows=sass.cmd">
```

Supported values are (case-insensitive): `FREEBSD`, `LINUX`, `OSX`, and `WINDOWS`. (Based on [OSPlatform.Create()](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.osplatform.create?view=netstandard-2.0) as of .NET Standard 2.0.)

If this syntax conflicts with your parameter values, you can use the `osDelimiters` parameter. It takes a two-character string specifying alternatives for the comma and the equals sign in that order, or an empty string to disable OS parsing.

## Environment variables

You can use environment variables in parameters using `%VARIABLE%` syntax. Note that the Windows-style `%...%` syntax must be used, even for non-Windows systems (e.g. Linux must use `%HOME%` rather than `$HOME`). Variables not found in the environment will not be processed and the original syntax will be left in the string.

You can disable environment variable expansion by setting the `expandVariables` parameter to false.

## File watching

If the `source` parameter specifies a local file, the type provider will monitor the file and refresh the CSS classes when it changes (rerunning any specified command if applicable).

If a `commandFile` is specified, any leading lines from the output of the command that exactly specify a local file path will also be watched.

## CSS Module support for Fable

In a Fable project, CSS modules can be used

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

If true, the type will include a `GetProperties()` method that returns a sequence of the generated property name/value pairs. This can be used, for example, to generate hard-coded CSS class bindings via `.fsx` script:

```fs
// Before release of F# 5, use preview flag: dotnet fsi --langversion:preview SCRIPT_NAME.fsx
#r "nuget: Zanaptak.TypedCssClasses"
open Zanaptak.TypedCssClasses
type css = CssClasses<"https://stackpath.bootstrapcdn.com/bootstrap/4.5.0/css/bootstrap.min.css", Naming.PascalCase, getProperties=true>
printfn "module Bootstrap"
css.GetProperties() |> Seq.iter (fun p -> printfn "let [<Literal>] %s = \"%s\"" p.Name p.Value)
```

Example output:
```fs
module Bootstrap
let [<Literal>] Accordion = "accordion"
let [<Literal>] Active = "active"
let [<Literal>] Alert = "alert"
let [<Literal>] AlertDanger = "alert-danger"
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

### fableCssModule

In a Fable project, this parameter causes the provided properties to be compiled to Fable import expressions instead of class name strings, allowing processing as a [CSS Module](https://github.com/css-modules/css-modules). (This only affects the type provider property substitutions; you must also configure your JavaScript bundler for CSS Module support.)

Enabling this option will cause the `source` parameter to be used in a JavaScript `import` statement after Fable compilation, so it must be specified in a way that both the type provider and the JavaScript bundler can resolve. It is recommended to use a path relative to the source file, with current directory dot prefix, as follows:

```fs
type css = CssClasses<"./style.module.css", resolutionFolder=__SOURCE_DIRECTORY__, fableCssModule=true>
```
