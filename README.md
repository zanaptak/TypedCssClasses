# Zanaptak.TypedCssClasses [![Nuget](https://img.shields.io/nuget/v/Zanaptak.TypedCssClasses)](https://www.nuget.org/packages/Zanaptak.TypedCssClasses/)

A CSS class type provider for F# web development.

Bring external stylesheet classes into your F# code as design-time discoverable compiler-verified properties.

## Code examples

The following code examples show how the type provider enables type-safe CSS classes. Anywhere you would have previously used a class name string, you can now use something like a `Bootstrap`-, `FA`-, or `tw`-prefixed property, with your IDE providing completion for valid classes.

The syntax in these examples is [Fable.React](https://fable.io/blog/Announcing-Fable-React-5.html) view syntax, but any web framework can be used because the provided properties just compile to the underlying class names as strings.

### Bootstrap CSS

```fs
// A "Bootstrap" type pointing at a remote Bootstrap CSS file.
type Bootstrap = CssClasses<"https://stackpath.bootstrapcdn.com/bootstrap/4.5.0/css/bootstrap.min.css", Naming.PascalCase>

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

// All CSS classes become FA.* properties, with design-time completion.
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

Preconfigured sample projects to see it in action and use as a starting point:

- [Fable Sass sample](https://github.com/zanaptak/TypedCssClasses/tree/master/sample/FableSass) - Demonstrates TypedCssClasses with Sass compilation in a Fable project.

- [Fable Tailwind sample](https://github.com/zanaptak/TypedCssClasses/tree/master/sample/FableTailwind) - Demonstrates TypedCssClasses with Tailwind CSS in a Fable project.

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

## Configuration

See the [configuration instructions](doc/configuration.md) for full details on configuration parameters to customize the behavior of the type provider.

## Notes

This type provider does not use formal CSS parsing; it identifies classes by typical selector patterns using (somewhat complex) regular expressions. It tests fine against several major CSS frameworks but is not guaranteed foolproof in case there is some obscure CSS syntax not accounted for.

Web URLs are expected to use static CDN or otherwise unchanging content and are cached on the filesystem with a 90-day expiration. If tracking of CSS changes is required, you must use local CSS files in your project.

If using Fable 2.x, update fable-compiler to version 2.3.17 or later to avoid an issue with the type provider failing to resolve relative file paths.

CSS `@import` rules are not processed internally by the type provider. If desired, they can be processed via external command; see the [TestWithFable test project](https://github.com/zanaptak/TypedCssClasses/tree/master/test/TestWithFable) for an example using [PostCSS](https://postcss.org/) with the [postcss-import](https://github.com/postcss/postcss-import) plugin.
