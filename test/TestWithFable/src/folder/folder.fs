module Folder
open Fable.React
open Fable.React.Props
open Zanaptak.TypedCssClasses

type css = CssClasses<"folder/folder1.css", logFile="folder/folder1.log">
let view model dispatch =
    div [ Class css.folder1class ] [ str "class: " ; str (nameof css.folder1class) ]

type css2 = CssClasses<"folder2.css", logFile="folder2.log", resolutionFolder=__SOURCE_DIRECTORY__>
let view2 model dispatch =
    div [ Class css2.folder2class ] [ str "class: " ; str (nameof css2.folder2class) ]
