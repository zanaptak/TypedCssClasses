module App

open Zanaptak.TypedCssClasses

// Configure type provider to use result of sass transformation.
type Css =
    CssClasses<
        "content/styles.sass"
        , Naming.PascalCase
        , commandFile = "node"
        , argumentPrefix = "sass-process.js"
        //, logFile = "TypedCssClasses.log" // uncomment to enable logging
    >

open Feliz

[<ReactComponent>]
let Counter() =
    let (count, setCount) = React.useState(0)

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
                    prop.onClick (fun _ -> setCount(count + 1))
                    prop.text "+"
                ]
                Html.div [
                    prop.classes [
                        Css.Number
                    ]
                    prop.text count
                ]
                Html.button [
                    prop.onClick (fun _ -> setCount(count - 1))
                    prop.text "-"
                ]
            ]
        ]
    ]

[<ReactComponent>]
let App() =
    Html.div [
        Counter()
    ]
