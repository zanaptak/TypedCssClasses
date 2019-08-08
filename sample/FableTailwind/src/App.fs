module App

(**
 The famous Increment/Decrement ported from Elm.
 You can find more info about Elmish architecture and samples at https://elmish.github.io/
*)

open Elmish
open Elmish.React
open Feliz

open Zanaptak.TypedCssClasses

// Naming.Verbatim required for Purgecss bundle reduction -- https://tailwindcss.com/docs/controlling-file-size
type tailwind = CssClasses<"../css/generated.css", Naming.Verbatim>

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
          tailwind.``sm:flex-row``
          tailwind.``sm:justify-around``
        ]

        prop.children [

          Html.button [
            prop.style [
              style.transitionProperty "background-color"
              style.transitionDurationMilliseconds 200
            ]
            prop.classes [
              tailwind.``custom-blue-button``
              tailwind.``w-02/05``
              tailwind.``sm:w-01/06``
              tailwind.``sm:order-last``
            ]
            prop.onClick (fun _ -> dispatch Increment)
            prop.text "+"
          ]

          Html.text [
            prop.classes [
              tailwind.``text-3xl``
              tailwind.``text-teal-600``
              tailwind.``text-center``
              tailwind.``w-01/06``
            ]
            prop.text (string model)
          ]

          Html.button [
            prop.style [
              style.transitionProperty "background-color"
              style.transitionDurationMilliseconds 200
            ]
            prop.classes [
              tailwind.``custom-blue-button``
              tailwind.``w-02/05``
              tailwind.``sm:w-01/06``
              tailwind.``sm:order-first``
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