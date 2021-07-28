module App

open Zanaptak.TypedCssClasses

// Configure type provider to use generated Tailwind classes.
// Naming.Verbatim is required for PurgeCSS bundle reduction, see https://tailwindcss.com/docs/optimizing-for-production
type private tw =
    CssClasses<
        "content/tailwind-source.css"
        , Naming.Verbatim
        , commandFile = "node"
        , argumentPrefix = "tailwind-process.js tailwind.config.js"
        //, logFile = "TypedCssClasses.log" // uncomment to enable logging
    >

open Feliz

[<ReactComponent>]
let Counter() =
    let (count, setCount) = React.useState(0)

    Html.div [
        prop.classes [
            tw.container
            tw.``mx-auto``
        ]

        prop.children [

            // Header row
            Html.div [
                prop.classes [
                    tw.``text-3xl``
                    tw.``mb-01``
                    tw.``text-green-600``
                ]
                prop.text "Counter (tailwind)"
            ]

            // Counter row
            Html.div [
                prop.classes [
                    tw.flex
                    tw.``flex-col``
                    tw.``items-center``
                    tw.``wide:flex-row-reverse``
                    tw.``wide:justify-around``
                ]

                prop.children [

                    // Increment button
                    Html.button [
                        prop.classes [
                            tw.``custom-blue-button``
                            tw.``w-02/05``
                            tw.``wide:w-01/06``
                            tw.``transition-colors``
                            tw.``duration-200``
                        ]
                        prop.onClick (fun _ -> setCount(count + 1))
                        prop.text "+"
                    ]

                    // Display current counter value
                    Html.div [
                        prop.classes [
                            tw.``text-3xl``
                            tw.``text-red-600``
                            tw.``text-center``
                            tw.``w-01/06``
                        ]
                        prop.text count
                    ]

                    // Decrement button
                    Html.button [
                        prop.classes [
                            tw.``custom-blue-button``
                            tw.``w-02/05``
                            tw.``wide:w-01/06``
                            tw.``transition-colors``
                            tw.``duration-200``
                        ]
                        prop.onClick (fun _ -> setCount(count - 1))
                        prop.text "-"
                    ]

                ]
            ]

        ]
    ]

[<ReactComponent>]
let App() =
    Html.div [
        Counter()
    ]
