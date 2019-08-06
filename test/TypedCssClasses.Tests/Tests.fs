module TypedCssClasses.Tests

open NUnit.Framework
open Zanaptak.TypedCssClasses.Utils
open System
open System.Reflection
open System.IO

let [< Test >] ``find classes bootstrap`` () =
  let assembly = Assembly.GetExecutingAssembly()

  let refClasses =
    let resource = assembly.GetManifestResourceStream( "TypedCssClasses.Tests.testdata.bootstrap-431-classes-reference.txt" )
    use reader = new StreamReader( resource )
    reader.ReadToEnd().Split( [| '\n' ; '\r' |] , StringSplitOptions.RemoveEmptyEntries )

  let cssToParse =
    let resource = assembly.GetManifestResourceStream( "TypedCssClasses.Tests.testdata.bootstrap-431-min-css.txt" )
    use reader = new StreamReader( resource )
    reader.ReadToEnd()

  let parsed = parseCss cssToParse id

  let refSet = Set.ofArray refClasses
  let parsedSet = parsed |> Seq.map ( fun p -> p.Value ) |> Set.ofSeq

  let refExtra = Set.difference refSet parsedSet
  let parsedExtra = Set.difference parsedSet refSet

  Assert.That( parsedExtra , Is.Empty )
  Assert.That( refExtra , Is.Empty )

let [< Test >] ``find classes tailwind`` () =
  let assembly = Assembly.GetExecutingAssembly()

  let refClasses =
    let resource = assembly.GetManifestResourceStream( "TypedCssClasses.Tests.testdata.tailwind-10-classes-reference.txt" )
    use reader = new StreamReader( resource )
    reader.ReadToEnd().Split( [| '\n' ; '\r' |] , StringSplitOptions.RemoveEmptyEntries )

  let cssToParse =
    let resource = assembly.GetManifestResourceStream( "TypedCssClasses.Tests.testdata.tailwind-10-min-css.txt" )
    use reader = new StreamReader( resource )
    reader.ReadToEnd()

  let parsed = parseCss cssToParse id

  let refSet = Set.ofArray refClasses
  let parsedSet = parsed |> Seq.map ( fun p -> p.Value ) |> Set.ofSeq

  let refExtra = Set.difference refSet parsedSet
  let parsedExtra = Set.difference parsedSet refSet

  Assert.That( parsedExtra , Is.Empty )
  Assert.That( refExtra , Is.Empty )


let [< Test >] ``Underscores: keep non ascii letters`` () =
  Assert.That( symbolsToUnderscores "αβⅢ" , Is.EqualTo "αβⅢ" )

let [< Test >] ``Underscores: replace connector punctuation`` () =
  Assert.That( symbolsToUnderscores "a⁀b_c" , Is.EqualTo "a_b_c" )

let [< Test >] ``Underscores: replace single quote`` () =
  Assert.That( symbolsToUnderscores "a'b" , Is.EqualTo "a_b" )

let [< Test >] ``Underscores: replace other punctuation`` () =
  Assert.That( symbolsToUnderscores "a:b‖c" , Is.EqualTo "a_b_c" )

let [< Test >] ``Underscores: replace supplementary char`` () =
  Assert.That( symbolsToUnderscores "a😀b𐐥c" , Is.EqualTo "a_b_c" )

let [< Test >] ``Underscores: prefix illegal first char`` () =
  Assert.That( symbolsToUnderscores "123" , Is.EqualTo "_123" )

let [< Test >] ``Underscores: suffix keyword`` () =
  Assert.That( symbolsToUnderscores "inline" , Is.EqualTo "inline_" )

let [< Test >] ``Underscores: preserve case`` () =
  Assert.That( symbolsToUnderscores "CARD-BODY" , Is.EqualTo "CARD_BODY" )


let [< Test >] ``PascalCase: upper non ascii letter at word boundary`` () =
  Assert.That( toPascalCase "ΑΒ-ΓΔ" , Is.EqualTo "ΑβΓδ" )

let [< Test >] ``PascalCase: connector punctuation to capitalization boundary`` () =
  Assert.That( toPascalCase "aa⁀bb_cc" , Is.EqualTo "AaBbCc" )

let [< Test >] ``PascalCase: single quote to capitalization boundary`` () =
  Assert.That( toPascalCase "aa'bb" , Is.EqualTo "AaBb" )

let [< Test >] ``PascalCase: other punctuation to capitalization boundary`` () =
  Assert.That( toPascalCase "aa:bb‖cc" , Is.EqualTo "AaBbCc" )

let [< Test >] ``PascalCase: supplementary char to capitalization boundary`` () =
  Assert.That( toPascalCase "aa😀bb𐐥cc" , Is.EqualTo "AaBbCc" )

let [< Test >] ``PascalCase: prefix illegal first char`` () =
  Assert.That( toPascalCase "123" , Is.EqualTo "_123" )

let [< Test >] ``PascalCase: all upper to mixed case`` () =
  Assert.That( toPascalCase "CARD-BODY" , Is.EqualTo "CardBody" )

let [< Test >] ``PascalCase: lower + upper preserved as word boundary between then`` () =
  Assert.That( toPascalCase "CardBody" , Is.EqualTo "CardBody" )

let [< Test >] ``PascalCase: upper + lower starts word boundary after string of uppers`` () =
  Assert.That( toPascalCase "CARDBody" , Is.EqualTo "CardBody" )


let [< Test >] ``CamelCase: upper non ascii letter at word boundary`` () =
  Assert.That( toCamelCase "ΑΒ-ΓΔ" , Is.EqualTo "αβΓδ" )

let [< Test >] ``CamelCase: connector punctuation to capitalization boundary`` () =
  Assert.That( toCamelCase "aa⁀bb_cc" , Is.EqualTo "aaBbCc" )

let [< Test >] ``CamelCase: single quote to capitalization boundary`` () =
  Assert.That( toCamelCase "aa'bb" , Is.EqualTo "aaBb" )

let [< Test >] ``CamelCase: other punctuation to capitalization boundary`` () =
  Assert.That( toCamelCase "aa:bb‖cc" , Is.EqualTo "aaBbCc" )

let [< Test >] ``CamelCase: supplementary char to capitalization boundary`` () =
  Assert.That( toCamelCase "aa😀bb𐐥cc" , Is.EqualTo "aaBbCc" )

let [< Test >] ``CamelCase: prefix illegal first char`` () =
  Assert.That( toCamelCase "123" , Is.EqualTo "_123" )

let [< Test >] ``CamelCase: all upper to mixed case`` () =
  Assert.That( toCamelCase "CARD-BODY" , Is.EqualTo "cardBody" )

let [< Test >] ``CamelCase: lower + upper preserved as word boundary between then`` () =
  Assert.That( toCamelCase "CardBody" , Is.EqualTo "cardBody" )

let [< Test >] ``CamelCase: upper + lower starts word boundary after string of uppers`` () =
  Assert.That( toCamelCase "CARDBody" , Is.EqualTo "cardBody" )

let [< Test >] ``CamelCase: suffix keyword`` () =
  Assert.That( toCamelCase "INLINE" , Is.EqualTo "inline_" )


let [< Test >] ``Verbatim: remove double-backticks entry`` () =
  let parsedCss = parseCss ".abc``xyz {} .abc`xyz {} .abcxyz {}" id |> Seq.toList
  Assert.That( parsedCss , Does.Not.Contain { Name = "abc``xyz" ; Value = "abc``xyz" } )
  Assert.IsTrue( parsedCss |> Seq.exists ( fun p -> p.Name = "abcxyz" ) )
  Assert.IsTrue( parsedCss |> Seq.exists ( fun p -> p.Name = "abc`xyz" ) )
  Assert.That( parsedCss , Has.Length.EqualTo 2 )

let [< Test >] ``Underscores: matching name gets non-suffixed name`` () =
  let parsedCss = parseCss ".card-body {} .card_body {} .cardBody {} .CardBody {}" symbolsToUnderscores |> Seq.toList
  Assert.That( parsedCss , Does.Contain { Name = "card_body" ; Value = "card_body" } )
  Assert.That( parsedCss , Does.Not.Contain { Name = "card_body" ; Value = "card-body" } )
  Assert.That( parsedCss , Does.Contain { Name = "card_body_2" ; Value = "card-body" } )
  Assert.IsTrue( parsedCss |> Seq.exists ( fun p -> p.Name = "cardBody" ) )
  Assert.IsTrue( parsedCss |> Seq.exists ( fun p -> p.Name = "CardBody" ) )

let [< Test >] ``PascalCase: matching name gets non-suffixed name`` () =
  let parsedCss = parseCss ".card-body {} .card_body {} .cardBody {} .CardBody {}" toPascalCase |> Seq.toList
  Assert.That( parsedCss , Does.Contain { Name = "CardBody" ; Value = "CardBody" } )
  Assert.That( parsedCss , Does.Not.Contain { Name = "CardBody" ; Value = "cardBody" } )
  Assert.That( parsedCss , Does.Contain { Name = "CardBody_2" ; Value = "cardBody" } )
  Assert.IsTrue( parsedCss |> Seq.exists ( fun p -> p.Name = "CardBody_3" ) )
  Assert.IsTrue( parsedCss |> Seq.exists ( fun p -> p.Name = "CardBody_4" ) )
  Assert.IsFalse( parsedCss |> Seq.exists ( fun p -> p.Name = "CardBody_5" ) )

let [< Test >] ``CamelCase: matching name gets non-suffixed name`` () =
  let parsedCss = parseCss ".card-body {} .card_body {} .cardBody {} .CardBody {}" toCamelCase |> Seq.toList
  Assert.That( parsedCss , Does.Contain { Name = "cardBody" ; Value = "cardBody" } )
  Assert.That( parsedCss , Does.Not.Contain { Name = "cardBody" ; Value = "CardBody" } )
  Assert.That( parsedCss , Does.Contain { Name = "cardBody_2" ; Value = "CardBody" } )
  Assert.IsTrue( parsedCss |> Seq.exists ( fun p -> p.Name = "cardBody_3" ) )
  Assert.IsTrue( parsedCss |> Seq.exists ( fun p -> p.Name = "cardBody_4" ) )
  Assert.IsFalse( parsedCss |> Seq.exists ( fun p -> p.Name = "cardBody_5" ) )

let [< Test >] ``unicode escapes`` () =
  let parsedCss = parseCss @".a\:b\00216bc {} " id |> Seq.toList
  Assert.That( parsedCss , Does.Contain { Name = "a:bⅫc" ; Value = "a:bⅫc" } )
