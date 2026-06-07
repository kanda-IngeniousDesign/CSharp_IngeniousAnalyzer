# CSharp_IngeniousAnalyzer

A static analyzer designed to dramatically improve the code quality of your C# projects. It automatically detects issues such as insecure null checks, inefficient LINQ queries, and magic numbers, helping you maintain a safe and clean codebase.

---

## How to use
This analyzer is fully integrated with Visual Studio's "Live Code Analysis." Simply open your project, and it will automatically analyze your code as you edit it, providing real-time warnings.

* If it does not run automatically, try rebuilding the project, restarting Visual Studio, or deleting the hidden .vs folder in your project root.

## Coding Style
We use .editorconfig to enforce a unified code style and maintain high maintainability. We recommend ensuring the following settings are applied to maintain consistent code quality:

* Visual Studio: Supports .editorconfig by default.
* VS Code: Installing the [EditorConfig for VS Code](https://marketplace.visualstudio.com/items?itemName=EditorConfig.EditorConfig) extension is recommended.

## Customizing Rules
Default warning levels are set, but you can adjust them to fit your development environment or preferences. For example, to change a rule's severity from warning to info, modify your .editorconfig as follows:

### Example: Changing COL001 from 'warning' to 'info'
* Before : 
`dotnet_diagnostic.COL001.severity = warning`
* After : 
`dotnet_diagnostic.COL001.severity = info`

## Rule List

| ID | Title | Message |
| :--- | :--- | :--- |
| **NULL001** | Use 'is null' pattern matching to avoid unintended operator overload behavior. | Use type-safe 'is null' pattern instead of comparison operators (== / !=). |
| **STR001** | Use lowercase 'string.Empty' to unify coding styles. | Use lowercase 'string.Empty' instead of '{0}' for consistency. |
| **STR002** | Replace string literals with 'nameof' to prevent breaking bugs during refactoring. | Use type-safe 'nameof({0})' instead of the magic string literal '{0}'. |
| **LINQ001** | Combine LINQ 'Where' and selection methods to optimize performance. | Combine Where().{0}() chain into a single '{0}(condition)'. |
| **LINQ002** | Remove redundant collection materialization (ToList/ToArray) to optimize memory usage. | '{0}' is not reused after enumeration. Remove the call to avoid unnecessary memory allocation. |
| **LINQ003** | Add ToList/ToArray to avoid multiple enumerations of the same sequence. | '{0}' is enumerated multiple times; materializing it into a collection will improve performance. |
| **COLL001** | Specify initial capacity for List to reduce memory reallocation overhead. | Specify initial capacity in List constructor as the loop count is predictable. |

<br>

# CSharp_IngeniousAnalyzer (日本語)

C#のコード品質を劇的に高める静的アナライザーです。NULLチェックの型安全性欠如や、非効率なLINQ等を自動検知し、安全でクリーンなコードへの修正を支援します。

---

## 使い方
本アナライザーは Visual Studio の「Live Code Analysis」と完全に統合されています。プロジェクトを開くだけで、コードの編集時に自動的に解析が実行され、問題がある場合はリアルタイムで警告が表示されます。

* 自動的に解析が実行されない場合は、リビルド、VS再起動、またはプロジェクトルートにある .vs フォルダー（隠しフォルダー）の削除を試してください。

## コーディングスタイル
本プロジェクトでは、コードスタイルを統一し、保守性を維持するために .editorconfig を採用しています。エディタの自動フォーマット機能を活用し、常に一貫したコード品質を保つため、開発時には以下の設定が反映されていることを推奨します。

* Visual Studio: .editorconfig は標準でサポートされています。
* VS Code: [EditorConfig for VS Code](https://marketplace.visualstudio.com/items?itemName=EditorConfig.EditorConfig) 拡張機能のインストールを推奨します。

## ルールのカスタマイズ
本プロジェクトではデフォルトで一定の警告レベルを設定していますが、開発環境や個人の好みに合わせて調整可能です。例えば、特定の警告を warning（警告）から info（情報）に変更したい場合は、プロジェクトルートの .editorconfig を開き、該当するルールを以下のように修正してください。

### 例: COL001の警告を info に変更する
* 変更前 : 
`dotnet_diagnostic.COL001.severity = warning`

* 変更後 : 
`dotnet_diagnostic.COL001.severity = info`

## Rule List (ルール一覧)

| ID | Title (JP) | Message (JP) |
| :--- | :--- | :--- |
| **NULL001** | 「is null」パターンを使用して、演算子オーバーロードによる意図しない不具合を防止します。 | 演算子（== / !=）ではなく、型安全な 'is null' パターンを使用してください。 |
| **STR001** | 小文字の「string.Empty」に統一し、プロジェクト全体のコードスタイルを最適化します。 | 大文字の 'String.{0}' ではなく、一貫性を持たせるため小文字の 'string.Empty' を使用してください。 |
| **STR002** | 文字列を「nameof」に置き換え、将来のリファクタリング時の破壊バグを防止します。 | マジックナンバー（文字列）の '{0}' ではなく、型安全な 'nameof({0})' を使用してください。 |
| **LINQ001** | LINQの無駄な2段階評価を統合し、反復処理のパフォーマンスを向上させます。 | Where().{0}() のチェーンを、単一の '{0}(条件)' に統合して最適化してください。 |
| **LINQ002** | 無駄なコレクションの確定を削除し、メモリ使用量を最適化します。 | '{0}' は列挙後に再利用されていません。不要なコレクションの実体化を回避するため、呼び出しを削除してください。 |
| **LINQ003** | 複数回の列挙を避けるため、結果を確定させてください。 | '{0}' は複数回列挙されています。メモリを確保して結果を確定させることで、計算の重複を排除してください。 |
| **COLL001** | Listの初期キャパシティを明示し、メモリ再確保コストを削減します。 | ループ回数が予測可能なため、Listのコンストラクタに初期キャパシティを指定してください。 |