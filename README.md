# CSharp_IngeniousAnalyzer (English)

[![NuGet Downloads](https://img.shields.io/nuget/dt/CSharp_IngeniousAnalyzer.svg)](https://www.nuget.org/packages/CSharp_IngeniousAnalyzer/)
[![NuGet Version](https://img.shields.io/nuget/v/CSharp_IngeniousAnalyzer.svg)](https://www.nuget.org/packages/CSharp_IngeniousAnalyzer/)

A static analyzer designed to dramatically improve the code quality of your C# projects. It automatically detects issues such as insecure null checks, inefficient LINQ queries, and overly complex methods, helping you maintain a safe and clean codebase.

## Feedback

Thank you very much for using this analyzer in your daily development. I created this tool based on my own professional needs, and I am committed to improving it to be more useful for your development workflows. Your feedback is very valuable to me. If you have any requests, suggestions for new rules, or encounter any issues, please feel free to reach out via the "Contact owners" link on the NuGet package page, or open an issue/discussion on our [GitHub Repository](https://github.com/kanda-IngeniousDesign/CSharp_IngeniousAnalyzer/issues). I would be honored to grow and refine this tool together with all of you.

## How to use

This analyzer is fully integrated with Visual Studio's "Live Code Analysis." Simply open your project, and it will automatically analyze your code as you edit it, providing real-time warnings.
If it does not run automatically, try rebuilding the project, restarting Visual Studio, or deleting the hidden .vs folder in your project root.

It also works the same way with `dotnet build`, VS Code (with the C# extension), and JetBrains Rider, since it's a standard Roslyn analyzer distributed as a NuGet package.

## Coding Style

We use .editorconfig to enforce a unified code style and maintain high maintainability. We recommend ensuring the following settings are applied to maintain consistent code quality:
* Visual Studio: Supports .editorconfig by default.
* VS Code: Installing the EditorConfig for VS Code extension is recommended.

## Customizing Rules

Default warning levels are set, but you can adjust them to fit your development environment or preferences. For example, to change a rule's severity from warning to info, modify your .editorconfig as follows:

**Example: Changing COLL001 from 'warning' to 'info'**
    dotnet_diagnostic.COLL001.severity = warning
    ↓
    dotnet_diagnostic.COLL001.severity = info

CPX001 and CPX002 don't have a fix that rewrites the flagged method itself (a safe, 100%-accurate mechanical transformation can't be guaranteed). Instead, each provides a Quick Fix (light bulb) that inserts a `// Ignore <RuleId>` comment immediately before the target method (or right after the opening brace of its body), letting you intentionally suppress a specific occurrence (e.g. legacy code). You can also add this comment manually.

```csharp
// Ignore CPX001
private void SomeComplexLegacyMethod() { ... }
```

ASYNC001 likewise doesn't provide a fix that appends `.ConfigureAwait(false)`. Adding it changes where the code after the `await` runs (it no longer returns to the original `SynchronizationContext`), so code that touches UI elements (WinForms/WPF) or `HttpContext.Current` (ASP.NET Framework) after the `await` would break at runtime, and the analyzer alone can't determine this with 100% accuracy. Please add it manually after confirming that the code after the `await` doesn't depend on the context. For methods that intentionally need to return to the original context, a Quick Fix inserts `// Ignore ASYNC001` at the start of the containing method's body (suppressing every await in that method, including those in lambdas and local functions). Awaits in `async void` methods/lambdas, `static Main`, and top-level statements are not flagged. If the rule is noisy in application or test projects, adjust its severity in .editorconfig.

LINQ002 will not flag (or auto-fix) a `.ToList()`/`.ToArray()` call when the corresponding `foreach` loop body calls any method on the original source collection (e.g. `Remove`, `Add`). Removing the materialization in that case would make the loop enumerate and mutate the same collection at once, causing a runtime `InvalidOperationException`; the `.ToList()`/`.ToArray()` there is very likely an intentional snapshot, not unnecessary allocation.
UNI001 detects Unicode characters that can't be told apart visually. They sneak in easily when copying from web pages, AI chat output, or diff tools, and the code usually still compiles (C# treats a no-break space as whitespace, and zero-width characters are valid inside identifiers), so they are hard to notice. The following characters are flagged anywhere in the file (string literals, identifiers, whitespace, comments, and code disabled by `#if`):

| Category | Characters |
|---|---|
| Control characters | C0 controls except tab/LF/CR (U+0000–U+001F), DELETE (U+007F), C1 controls (U+0080–U+009F) |
| Special spaces | NO-BREAK SPACE (U+00A0), U+2000–U+200A, NARROW NO-BREAK SPACE (U+202F), MEDIUM MATHEMATICAL SPACE (U+205F) |
| Zero-width / invisible format characters | SOFT HYPHEN (U+00AD), COMBINING GRAPHEME JOINER (U+034F), MONGOLIAN VOWEL SEPARATOR (U+180E), U+200B–U+200D, U+2060–U+2064, ZERO WIDTH NO-BREAK SPACE / BOM (U+FEFF, except at the start of the file), U+FFF9–U+FFFB |
| Bidirectional controls ("Trojan Source") | U+061C, U+200E, U+200F, U+202A–U+202E, U+2066–U+206F |
| Line breaks that don't look like line breaks | NEXT LINE (U+0085), LINE SEPARATOR (U+2028), PARAGRAPH SEPARATOR (U+2029) |
| Characters rendered as blank | Hangul fillers (U+115F, U+1160, U+3164, U+FFA0), Khmer inherent vowels (U+17B4, U+17B5) |
| Combining (semi-)voiced sound marks | U+3099, U+309A (`か` + U+3099 looks identical to `が`; occurs when copying file names on macOS) |
| Tag characters | U+E0001, U+E0020–U+E007F (can embed invisible text) |

The ideographic (full-width) space U+3000 is intentionally not flagged, as it is commonly used in Japanese text. Characters written as escape sequences (e.g. `"\u00A0"`) are visible in the source and are not flagged either, so write intentional uses that way.

UNI001 doesn't provide a fix that rewrites the character. For example, whether a no-break space in a string should become a normal space, be removed, or is intentional can't be determined by the analyzer, and replacing it with `\u00A0` would only hide the warning while leaving the bug in place. Please correct it manually. For intentional occurrences, a Quick Fix inserts `// Ignore UNI001` immediately before the nearest statement or member declaration (field, method, class, etc.) containing the character. The comment also suppresses everything inside that statement/member, so placing it before a method or class suppresses the whole method or class.

## Rule List

| ID | Title | Message |
|---|---|---|
| NULL001 | Safety improvement by unifying to the 'is null' pattern | Use the type-safe 'is null' pattern instead of operators (== / !=). |
| STR001 | Optimization by standardizing on string.Empty | Use lowercase 'string.Empty' instead of 'String.{0}' for consistency. |
| STR002 | Safety improvement by replacing with nameof | Use type-safe 'nameof({0})' instead of the magic string '{0}'. |
| STR003 | Code optimization by removing redundant ToString() calls | '{0}' is already a string. Remove the redundant '.ToString()' call. |
| LINQ001 | Performance improvement by integrating LINQ evaluation | Integrate the Where().{0}() chain into a single '{0}(predicate)' for optimization. |
| LINQ002 | Removal of unnecessary collection materialization | '{0}' is not reused after enumeration. Remove this call to avoid unnecessary memory allocation. |
| COLL001 | Memory reduction by specifying initial List capacity | Specify an initial capacity in the List constructor as the loop count is predictable. |
| COMM001 | Improved readability and maintainability by adding documentation comments | Function header is missing. Please add the documentation comments. |
| COMM002 | Improved accuracy by synchronizing function header parameters | Function header parameters do not match the method definition. Please synchronize '{0}'. |
| CPX001 | Improve readability by reducing method complexity | Method '{0}' has a complexity of {1} (threshold: 17). Consider refactoring or splitting the logic. |
| CPX002 | Improve maintainability by splitting long methods | Method '{0}' has {1} lines of code but only {2} method invocations. Please consider refactoring by extracting logic into smaller methods. |
| COMP001 | Standardization of inequality operator direction | Please reverse the inequality signs to improve readability. |
| EXC001 | Improved maintainability by clarifying exception handling | The catch block for '{0}' is empty. Add handling, or if this is intentional, leave a comment explaining why. |
| EXC002 | Preserve the stack trace when rethrowing exceptions | Use 'throw;' instead of 'throw {0};' to preserve the original stack trace. |
| ASYNC001 | Deadlock avoidance by adding ConfigureAwait(false) | The call to '{0}' is missing ConfigureAwait(false). Consider adding it in library code to avoid potential deadlocks. |
| UNI001 | Prevent hidden bugs by detecting invisible Unicode characters | The invisible Unicode character {0} ({1}) is mixed into the code. If it was mixed in unintentionally, remove it or replace it with an ordinary character. |

---

# CSharp_IngeniousAnalyzer (日本語)

C#のコード品質を劇的に高める静的アナライザーです。 NULLチェックの型安全性欠如や、非効率なLINQ等を自動検知し、安全でクリーンなコードへの修正を支援します。

## フィードバックについて

本アナライザーを日々ご利用いただき、誠にありがとうございます。 このツールは私自身が業務で「あったらいいな」と考えたものを形にしたものです。 至らぬ点もあるかと存じますが、より使いやすく、皆様の開発の助けとなるよう、継続的に改善を行っていきたいと考えています。 皆様からのご意見は大変貴重な財産です。 「ここをこうしてほしい」「このルールがあると嬉しい」といったご要望やフィードバックがございましたら、NuGetページ右下の「Contact owners」、または [GitHubリポジトリのIssues/Discussions](https://github.com/kanda-IngeniousDesign/CSharp_IngeniousAnalyzer/issues) よりお気軽にご連絡ください。 皆様と一緒にこのツールを育てていけたら幸いです。

## 使い方

本アナライザーは Visual Studio の「Live Code Analysis」と完全に統合されています。 プロジェクトを開くだけで、コードの編集時に自動的に解析が実行され、問題がある場合はリアルタイムで警告が表示されます。
自動的に解析が実行されない場合は、リビルド、VS再起動、またはプロジェクトルートにある .vs フォルダー（隠しフォルダー）の削除を試してください。

標準的なRoslynアナライザーとしてNuGetパッケージ経由で配布しているため、`dotnet build`・VS Code（C#拡張機能）・JetBrains Riderでも同様に動作します。

## コーディングスタイル

本プロジェクトでは、コードスタイルを統一し、保守性を維持するために .editorconfig を採用しています。
* Visual Studio: .editorconfig は標準でサポートされています。
* VS Code: EditorConfig for VS Code 拡張機能のインストールを推奨します。

## ルールのカスタマイズ

デフォルトの警告レベルは設定済みですが、開発環境に合わせて .editorconfig で調整可能です。

**例: COLL001の警告を info に変更する**
    dotnet_diagnostic.COLL001.severity = warning
    ↓
    dotnet_diagnostic.COLL001.severity = info

CPX001・CPX002は、警告対象のメソッド自体を書き換えるFixは提供していません（機械的に100%安全な変換を保証できないため）。代わりに、対象のメソッドの直前（またはメソッド本体の先頭）に `// Ignore <ルールID>` コメントを自動挿入するクイックフィックス（電球アイコン）を用意しているので、レガシーコード等の特定箇所を意図的に抑制できます。手動でコメントを追加しても構いません。

```csharp
// Ignore CPX001
private void SomeComplexLegacyMethod() { ... }
```

ASYNC001も同様に、`.ConfigureAwait(false)` を追記するFixは提供していません。追記すると `await` 以降の処理が元の `SynchronizationContext` に戻らなくなるため、`await` の後でUI要素（WinForms/WPF）や `HttpContext.Current`（ASP.NET Framework）に触れているコードは実行時に動作しなくなります。アナライザー単体ではこれを100%判定できないため、`await` 以降の処理がコンテキストに依存していないことを確認のうえ、手動で追加してください。意図的に元のコンテキストへ戻す必要があるメソッドには、メソッド本体の先頭に `// Ignore ASYNC001` を挿入するクイックフィックスを用意しています（ラムダ式・ローカル関数を含め、そのメソッド内のすべての await が抑制されます）。なお、`async void` のメソッド・ラムダ式、`static Main`、トップレベルステートメント内の await は検知対象外です。アプリケーションやテストプロジェクトでノイズになる場合は、.editorconfig でseverityを調整してください。

LINQ002は、対応する`foreach`ループ本体の中で列挙元の元コレクションに対するメソッド呼び出し（`Remove`、`Add`等）がある場合、`.ToList()`/`.ToArray()`を警告・自動修正しません。その状況で実体化を取り除くと、同じコレクションを列挙しながら変更することになり実行時に`InvalidOperationException`が発生してしまうため、その`.ToList()`/`.ToArray()`は不要なメモリ確保ではなく意図的なスナップショットである可能性が高いと判断しています。

UNI001は、目視で判別できないUnicode文字を検知します。Webページ・生成AIのチャット出力・差分ツール等からのコピー＆ペーストで混入しやすく、多くの場合そのままコンパイルが通る（C#はノーブレークスペースを空白として扱い、ゼロ幅文字は識別子の一部として有効）ため、気付くのが困難です。ファイル内のあらゆる場所（文字列リテラル・識別子・空白・コメント・`#if` で無効化されたコード）にある以下の文字を検知します。

| 分類 | 対象文字 |
|---|---|
| 制御文字 | タブ・LF・CRを除くC0制御文字（U+0000〜U+001F）、DELETE（U+007F）、C1制御文字（U+0080〜U+009F） |
| 特殊なスペース | ノーブレークスペース（U+00A0）、U+2000〜U+200A、NARROW NO-BREAK SPACE（U+202F）、MEDIUM MATHEMATICAL SPACE（U+205F） |
| ゼロ幅文字・不可視の書式文字 | ソフトハイフン（U+00AD）、COMBINING GRAPHEME JOINER（U+034F）、MONGOLIAN VOWEL SEPARATOR（U+180E）、U+200B〜U+200D、U+2060〜U+2064、ZERO WIDTH NO-BREAK SPACE / BOM（U+FEFF。ファイル先頭を除く）、U+FFF9〜U+FFFB |
| 双方向制御文字（Trojan Source） | U+061C、U+200E、U+200F、U+202A〜U+202E、U+2066〜U+206F |
| 改行に見えない改行文字 | NEXT LINE（U+0085）、LINE SEPARATOR（U+2028）、PARAGRAPH SEPARATOR（U+2029） |
| 空白として描画される文字 | ハングルの埋め字（U+115F、U+1160、U+3164、U+FFA0）、クメール文字の固有母音（U+17B4、U+17B5） |
| 結合用の濁点・半濁点 | U+3099、U+309A（「か」＋U+3099 は「が」と同じ見た目になる。Macのファイル名のコピー等で混入する） |
| タグ文字 | U+E0001、U+E0020〜U+E007F（不可視の文字列を埋め込める） |

全角スペース（U+3000）は日本語の文字列・コメントで意図的に使われることが多いため、対象外としています。また、エスケープシーケンス（`"\u00A0"` 等）で記述された文字はソース上で目視できるため検知しません。意図的に使う場合はエスケープシーケンスで記述してください。

UNI001は、文字を書き換えるFixは提供していません。例えば文字列内のノーブレークスペースは、半角スペースにすべきか・削除すべきか・意図的なのかをアナライザーが判断できず、`\u00A0` への置き換えは不具合を残したまま警告だけを消してしまうためです。手動で修正してください。意図的な箇所には、その文字を含む直近の文またはメンバー宣言（フィールド・メソッド・クラス等）の直前に `// Ignore UNI001` を挿入するクイックフィックスを用意しています。このコメントはその文・メンバーの内側すべてに効くため、メソッドやクラスの直前に書けば、そのメソッド・クラス全体を抑制できます。

## Rule List (ルール一覧)

| ID | Title (JP) | Message (JP) |
|---|---|---|
| NULL001 | 'is null'パターンへの統一による安全性向上 | 演算子（== / !=）ではなく、型安全な 'is null' パターンを使用してください。 |
| STR001 | string.Emptyへの統一による最適化 | 'String.{0}' ではなく、一貫性を持たせるため小文字の 'string.Empty' を使用してください。 |
| STR002 | nameofへの置き換えによる安全性向上 | 文字列リテラル '{0}' ではなく、型安全な 'nameof({0})' を使用してください。 |
| STR003 | 冗長なToString()呼び出しの削除によるコード最適化 | '{0}' は既にstring型です。冗長な '.ToString()' 呼び出しを削除してください。 |
| LINQ001 | LINQ評価の統合によるパフォーマンス向上 | Where().{0}() のチェーンを、単一の '{0}(predicate)' に統合して最適化してください。 |
| LINQ002 | 不要なコレクションの実体化の削除 | '{0}' は列挙後に再利用されていません。メモリ確保を回避するため、呼び出しを削除してください。 |
| COLL001 | List初期キャパシティ指定によるメモリ削減 | ループ回数が予測可能なため、Listのコンストラクタに初期キャパシティを指定してください。 |
| COMM001 | ドキュメントコメント追加による可読性・保守性の向上 | 関数ヘッダーが記述されていません。ドキュメントコメントを追加してください。 |
| COMM002 | パラメータ同期による関数ヘッダーの整合性向上 | 関数ヘッダーのパラメータがメソッド定義と一致していません。'{0}' を同期してください。 |
| CPX001 | メソッドの複雑度削減による可読性の向上 | メソッド '{0}' の複雑度が {1} です（閾値: 17）。分割やリファクタリングを検討してください。 |
| CPX002 | メソッド分割による保守性の向上 | メソッド '{0}' は {1} 行と長大です。関数呼び出しが {2} 回と少ないため、処理の分割（メソッド抽出）を検討してください。 |
| COMP001 | 不等号演算子の向きの統一 | 可読性向上のため、不等号を反転させてください。 |
| EXC001 | 例外処理の明確化による保守性向上 | '{0}' のcatchブロックが空です。処理を追加するか、意図的な場合はその理由をコメントで記述してください。 |
| EXC002 | 再スロー時のスタックトレース保持 | 'throw {0};' ではなく 'throw;' を使用して、元のスタックトレースを保持してください。 |
| ASYNC001 | ConfigureAwait(false)の追加によるデッドロック回避 | '{0}' の呼び出しに ConfigureAwait(false) がありません。ライブラリコードではデッドロック回避のため追加を検討してください。 |
| UNI001 | 不可視Unicode文字の検知による不具合防止 | 目視で判別できないUnicode文字 {0}（{1}）が混入しています。意図しない混入であれば、削除するか通常の文字に置き換えてください。 |