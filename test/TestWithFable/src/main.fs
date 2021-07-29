module Main
open Fable.React
open Fable.React.Props
open Zanaptak.TypedCssClasses

type css = CssClasses<"main.css", logFile="main.log">
let view model dispatch =
    div [] [
        div [ Class css.mainclass ] [ str "class: " ; str (nameof css.mainclass) ]
        Folder.view model dispatch
        Folder.view2 model dispatch
        Import.view model dispatch
        Sass.view model dispatch
    ]

open Fable.Core.JsInterop
importSideEffects "./main.css"
importSideEffects "./folder/folder1.css"
importSideEffects "./folder/folder2.css"
importSideEffects "./import/import1.css"
importSideEffects "./sass/sass.sass"

open Elmish
open Elmish.React
Program.mkSimple ignore ( fun _ _ -> () ) view
|> Program.withReactSynchronous "elmish-app"
|> Program.run
