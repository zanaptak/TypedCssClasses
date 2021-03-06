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

// Naming.Verbatim required for PurgeCSS bundle reduction -- https://tailwindcss.com/docs/controlling-file-size
type tailwind =
  CssClasses<
    "content/tailwind-source.css"
    , Naming.Verbatim
    , commandFile = "node"
    , argumentPrefix = "tailwind-process.js tailwind.config.js"
    //, logFile = "TypedCssClasses.log" // uncomment to enable logging
  >

let view (model:Model) dispatch =

  Html.div [
    prop.classes [
      tailwind.container
      tailwind.``mx-auto``
    ]

    prop.children [

      // Header row
      Html.div [
        prop.classes [
          tailwind.``text-3xl``
          tailwind.``mb-01``
          tailwind.``text-purple-800``
        ]
        prop.text "Counter (tailwind)"
      ]

      // Counter row
      Html.div [
        prop.classes [
          tailwind.flex
          tailwind.``flex-col``
          tailwind.``items-center``
          tailwind.``wide:flex-row-reverse``
          tailwind.``wide:justify-around``
        ]

        prop.children [

          Html.button [
            prop.classes [
              tailwind.``custom-blue-button``
              tailwind.``w-02/05``
              tailwind.``wide:w-01/06``
              tailwind.``transition-colors``
              tailwind.``duration-200``
            ]
            prop.onClick (fun _ -> dispatch Increment)
            prop.text "+"
          ]

          Html.div [
            prop.classes [
              tailwind.``text-3xl``
              tailwind.``text-teal-600``
              tailwind.``text-center``
              tailwind.``w-01/06``
            ]
            prop.text model
          ]

          Html.button [
            prop.classes [
              tailwind.``custom-blue-button``
              tailwind.``w-02/05``
              tailwind.``wide:w-01/06``
              tailwind.``transition-colors``
              tailwind.``duration-200``
            ]
            prop.onClick (fun _ -> dispatch Decrement)
            prop.text "-"
          ]

        ]
      ]

    ]
  ]

// App
Program.mkSimple init update view
|> Program.withReactSynchronous "elmish-app"
|> Program.run
