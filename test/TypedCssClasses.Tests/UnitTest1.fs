module Tests

open NUnit.Framework
open Zanaptak.TypedCssClasses.Utils

[<Test>]
let Test1 () =
  Assert.AreEqual( "a_b" , symbolsToUnderscores "a-b" )
