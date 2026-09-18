; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MDG001 | DomValueModel | Error | Type with [DV] members must be partial
MDG002 | DomValueModel | Error | [DV] member has a type the value model cannot describe
MDG003 | DomValueModel | Error | [DV] is only meaningful on an instance member
MDG004 | DomValueModel | Error | Two [DV] members share a name
MDG005 | DomValueModel | Error | [DV] is only meaningful inside a DocumentObject
MDG006 | DomValueModel | Warning | RefOnly has no meaning on a value member
MDG007 | DomValueModel | Warning | [DV] member's name appears nowhere in this type's Serialize
