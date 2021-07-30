# Changelog - Zanaptak.TypedCssClasses

[![GitHub](https://img.shields.io/badge/-github-gray?logo=github)](https://github.com/zanaptak/TypedCssClasses) [![NuGet](https://img.shields.io/nuget/v/Zanaptak.TypedCssClasses?logo=nuget)](https://www.nuget.org/packages/Zanaptak.TypedCssClasses)

## 1.0.0 (2021-07-30)

- Add [`fableCssModule`](https://github.com/zanaptak/TypedCssClasses/blob/main/doc/configuration.md#fablecssmodule) parameter for CSS Module support in Fable projects ([#11](https://github.com/zanaptak/TypedCssClasses/pull/11)) @alfonsogarciacaro
- Fix stale cache data when changing `naming` and `nameCollisions` parameters ([#12](https://github.com/zanaptak/TypedCssClasses/issues/12))

## 0.4.0 (2020-07-26)

- Add caching of parsed results from files and process execution ([#7](https://github.com/zanaptak/TypedCssClasses/issues/7), [#8](https://github.com/zanaptak/TypedCssClasses/pull/8))
    - Change from properties to fields for better completion list performance
    - See [note on performance](https://github.com/zanaptak/TypedCssClasses/pull/8#issue-456779399) with large class counts

## 0.3.1 (2020-07-16)

- Add process id to log messages

## 0.3.0 (2020-07-14)

- Add configurable [external command](https://github.com/zanaptak/TypedCssClasses/blob/main/doc/configuration.md#external-command-support-for-css-preprocessing) capability for CSS preprocessing ([#4](https://github.com/zanaptak/TypedCssClasses/issues/4))
- Support environment variables in parameters
- Support OS-specific values in parameters
- Add [`logFile`](https://github.com/zanaptak/TypedCssClasses/blob/main/doc/configuration.md#logfile) option
- Enable Source Link
- __Breaking change__: Target .NET Standard 2.0 only, per Type Provider SDK guidance (may affect very old build chains)
- __Breaking change__: The environment variable and OS-specific parameter support may affect existing parameter values if they use the same syntax. See the [`osDelimiters`](https://github.com/zanaptak/TypedCssClasses/blob/main/doc/configuration.md#osdelimiters) and [`expandVariables`](https://github.com/zanaptak/TypedCssClasses/blob/main/doc/configuration.md#expandvariables) options to address this.

## 0.2.0 (2019-09-27)

- Add [`nameCollisions`](https://github.com/zanaptak/TypedCssClasses/blob/main/doc/configuration.md#namecollisions) parameter for handling duplicate property names ([#1](https://github.com/zanaptak/TypedCssClasses/issues/1))

## 0.1.0 (2019-08-21)

- Packaging update (no change in functionality):
    - Remove System.ValueTuple dependency

## 0.0.3 (2019-08-10)

- Add [`getProperties`](https://github.com/zanaptak/TypedCssClasses/blob/main/doc/configuration.md#getproperties) option providing seq of generated properties, useful for code generation
- Add .NET Framework target

## 0.0.2 (2019-08-06)

- Recognize classes inside `@supports` rules
- Provide duplicate property names with `_2`, `_3`, etc. suffixes
- Preserve non-ASCII letters with `Naming.Underscores`
- Add `_` prefix to names with invalid identifier start characters (except with `Naming.Verbatim`)
- Add `_` suffix to names that are F# keywords (except with `Naming.Verbatim`)

## 0.0.1 (2019-07-29)

- Initial release
