# TypedCssClasses

A CSS class type provider for F# web development.

Bring external stylesheet classes into your F# code as design-time discoverable compiler-verified properties.

(This is a work in progress. Features and API are subject to change, with no promise of backward compatibility during early development.)

## Examples

The following examples are using Fable.React view syntax.

### Bootstrap CSS

```fs
type Bootstrap = CssClasses<"https://stackpath.bootstrapcdn.com/bootstrap/4.3.1/css/bootstrap.min.css", Naming.PascalCase>

div [ ClassName Bootstrap.Card ] [
    div [ ClassName Bootstrap.CardBody ] [
        h5 [ ClassName Bootstrap.CardTitle ] [ str "A clever title" ]
        p [ ClassName Bootstrap.CardText ] [ str "Lorem ipsum dolor sit amet." ]
    ]
]
```

### Font Awesome CSS

```fs
type icon = CssClasses<"https://use.fontawesome.com/releases/v5.9.0/css/all.min.css", Naming.Underscores>

i [ classList [
    icon.far, true
    icon.fa_grin, true ] ] []

i [ classList [
    icon.fas, true
    icon.fa_thumbs_up, true ] ] []
```

### Tailwind CSS

```fs
type t = CssClasses<"https://unpkg.com/tailwindcss@^1.0/dist/tailwind.min.css", Naming.Verbatim>

div [ ClassName <| String.concat " " [
        t.``bg-red-200``
        t.``hover:bg-red-500``
        t.``hover:font-bold``

        t.``sm:text-2xl``
        t.``sm:bg-green-200``
        t.``sm:hover:bg-green-500``

        t.``lg:text-4xl``
        t.``lg:bg-blue-200``
        t.``lg:hover:bg-blue-500`` ]
] [ str "Resize me! Hover me!" ]
```

#### CSS Bundle Size

When using a custom-generated Tailwind CSS file in your local build process, the Tailwind documentation recommends using Purgecss to reduce your CSS bundle size. With the `Naming.Verbatim` option, that technique will work with this library as well. Just point Purgecss at your *.fs view files, and it will identify the classes you are actually using and remove unused classes from the final bundle.

## Getting started

This type provider has been primarily developed and tested with Fable, but since the provided class name properties are simply strings, they should work with any web framework.

It is tested and working in Visual Studio Code on Windows and Linux, and in Visual Studio 2019 on Windows.

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

## Parameters

### source

The source CSS to process. Can be a file path, web URL, or CSS text.

### naming

* `Naming.Verbatim`: (default) use class names verbatim from source CSS, requiring backtick-quotes for names with special characters.
* `Naming.Underscores`: replace all non-alphanumeric characters with underscores.
* `Naming.CamelCase`: convert to camel case names with all non-alphanumeric characters removed.
* `Naming.PascalCase`: convert to Pascal case names with all non-alphanumeric characters removed.

Verbatim is the default naming strategy to avoid the small chance of name collisions. For example, if the CSS contained ``class-1`` and ``class_1``, none of the non-verbatim options would be able to generate two unique property names and thus would provide only one of them arbitrarily in the generated type.

### resolutionFolder

A custom folder to use for resolving relative file paths.

## Notes

As with all type providers, the source CSS file must be available at both design time and build time.

With Fable, relative file paths currently fail during build (but they work in the IDE). Use absolute paths for now.

This does not use formal CSS parsing, just identifying classes by typical selector patterns. It's working on the major CSS frameworks but may fail on more obscure CSS syntax.

Web URLs are expected to use static CDN or otherwise unchanging content and are cached on the local filesystem with a 90-day expiration.
