namespace Zanaptak.TypedCssClasses

type Naming =
  | Verbatim = 0
  | Underscores = 1
  | CamelCase = 2
  | PascalCase = 3

type NameCollisions =
  | BasicSuffix = 0
  | ExtendedSuffix = 1
  | Omit = 2
