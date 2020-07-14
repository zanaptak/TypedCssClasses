module Import
open Fable.React
open Fable.React.Props
open Zanaptak.TypedCssClasses

type css = CssClasses<"import1.css", logFile="import.log", commandFile="node", argumentPrefix="import.js", resolutionFolder=__SOURCE_DIRECTORY__>
let view model dispatch =
  div [] [
    div [ Class css.import1class ] [ str "class: " ; str (nameof css.import1class) ]
    div [ Class css.import2class ] [ str "class: " ; str (nameof css.import2class) ]
  ]
