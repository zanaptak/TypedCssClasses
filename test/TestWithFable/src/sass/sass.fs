module Sass
open Fable.React
open Fable.React.Props
open Zanaptak.TypedCssClasses

type css = CssClasses<"sass.sass", logFile="sass.log", commandFile="sass,Windows=sass.cmd", resolutionFolder=__SOURCE_DIRECTORY__>
let view model dispatch =
    div [ Class css.sassclass ] [ str "class: " ; str (nameof css.sassclass) ]
