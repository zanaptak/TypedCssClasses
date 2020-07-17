module App

(**
 The famous Increment/Decrement ported from Elm.
 You can find more info about Elmish architecture and samples at https://elmish.github.io/
*)

open Elmish
open Elmish.React
open Elmish.HMR
open Feliz

// MODEL

type Model = int

type Msg =
| Increment
| Decrement

let init() : Model = 0

// UPDATE

let update (msg:Msg) (model:Model) =
    match msg with
    | Increment -> model + 1
    | Decrement -> model - 1

// VIEW (rendered with React)

open Zanaptak.TypedCssClasses
type Css =
  CssClasses<
    "../css/styles.sass"
    , Naming.PascalCase
    , commandFile = "node"
    , argumentPrefix = "../sass-process.js"
    , logFile = "TypedCssClasses.log"
  >

let view (model:Model) dispatch =
  Html.div [
    prop.classes [
      Css.Base
    ]
    prop.children [
      // Header row
      Html.div [
        prop.classes [
          Css.Inverse
        ]
        prop.text "Counter"
      ]
      // Counter row
      Html.div [
        Html.button [
          prop.onClick (fun _ -> dispatch Increment)
          prop.text "+"
        ]
        Html.text (string model)
        Html.button [
          prop.onClick (fun _ -> dispatch Decrement)
          prop.text "-"
        ]
      ]
    ]
  ]

// App
Program.mkSimple init update view
|> Program.withReactSynchronous "elmish-app"
|> Program.run
