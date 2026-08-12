# CSharp_IngeniousAnalyzer (English)

[![NuGet Downloads](https://img.shields.io/nuget/dt/CSharp_IngeniousAnalyzer.svg)](https://www.nuget.org/packages/CSharp_IngeniousAnalyzer/)
[![NuGet Version](https://img.shields.io/nuget/v/CSharp_IngeniousAnalyzer.svg)](https://www.nuget.org/packages/CSharp_IngeniousAnalyzer/)

A static analyzer designed to dramatically improve the code quality of your C# projects. It automatically detects issues such as insecure null checks, inefficient LINQ queries, and magic numbers, helping you maintain a safe and clean codebase.

## Feedback

Thank you very much for using this analyzer in your daily development. I created this tool based on my own professional needs, and I am committed to improving it to be more useful for your development workflows. Your feedback is very valuable to me. If you have any requests, suggestions for new rules, or encounter any issues, please feel free to reach out via the "Contact owners" link on the NuGet package page, or open an issue/discussion on our [GitHub Repository](https://github.com/kanda-IngeniousDesign/CSharp_IngeniousAnalyzer/issues). I would be honored to grow and refine this tool together with all of you.

## How to use

This analyzer is fully integrated with Visual Studio's "Live Code Analysis." Simply open your project, and it will automatically analyze your code as you edit it, providing real-time warnings.
If it does not run automatically, try rebuilding the project, restarting Visual Studio, or deleting the hidden .vs folder in your project root.

## Coding Style

We use .editorconfig to enforce a unified code style and maintain high maintainability. We recommend ensuring the following settings are applied to maintain consistent code quality:
* Visual Studio: Supports .editorconfig by default.
* VS Code: Installing the EditorConfig for VS Code extension is recommended.

## Customizing Rules

Default warning levels are set, but you can adjust them to fit your development environment or preferences. For example, to change a rule's severity from warning to info, modify your .editorconfig as follows:

**Example: Changing COL001 from 'warning' to 'info'**
    dotnet_diagnostic.COL001.severity = warning
    ↓
    dotnet_diagnostic.COL001.severity = info

## Rule List

| ID | Title | Message |
|---|---|---|
| NULL001 | Prevention of operator overloading issues | Use the type-safe 'is null' pattern instead of operators (== / !=). |
| STR001 | Optimization by standardizing on string.Empty | Use lowercase 'string.Empty' instead of 'String.{0}' for consistency. |
| STR002 | Safety improvement by replacing with nameof | Use type-safe 'nameof({0})' instead of the magic string '{0}'. |
| LINQ001 | Performance improvement by integrating LINQ evaluation | Integrate the Where().{0}() chain into a single '{0}(predicate)' for optimization. |
| LINQ002 | Removal of unnecessary collection materialization | '{0}' is not reused after enumeration. Remove this call to avoid unnecessary memory allocation. |
| COLL001 | Memory reduction by specifying initial List capacity | Specify an initial capacity in the List constructor as the loop count is predictable. |
| COMM001 | Missing function header | Function header is missing. Please add the documentation comments. |
| COMM002 | Function header parameter mismatch | Function header parameters do not match the method definition. Please synchronize '{0}'. |
| CPX001 | Improve readability by reducing method complexity | Method '{0}' has a complexity of {1} (threshold: 17). Consider refactoring or splitting the logic. |
| CPX002 | Improve maintainability by splitting long methods | Method '{0}' is {1} lines long. With only {2} invocations, consider refactoring or splitting the logic. |
| COMP001 | Standardization of inequality operator direction | Please reverse the inequality signs to improve readability. |

---

# CSharp_IngeniousAnalyzer (日本語)

C#のコード品質を劇的に高める静的アナライザーです。 NULLチェックの型安全性欠如や、非効率なLINQ等を自動検知し、安全でクリーンなコードへの修正を支援します。

## フィードバックについて

本アナライザーを日々ご利用いただき、誠にありがとうございます。 このツールは私自身が業務で「あったらいいな」と考えたものを形にしたものです。 至らぬ点もあるかと存じますが、より使いやすく、皆様の開発の助けとなるよう、継続的に改善を行っていきたいと考えています。 皆様からのご意見は大変貴重な財産です。 「ここをこうしてほしい」「このルールがあると嬉しい」といったご要望やフィードバックがございましたら、NuGetページ右下の「Contact owners」、または [GitHubリポジトリのIssues/Discussions](https://github.com/kanda-IngeniousDesign/CSharp_IngeniousAnalyzer/issues) よりお気軽にご連絡ください。 皆様と一緒にこのツールを育てていけたら幸いです。

## 使い方

本アナライザーは Visual Studio の「Live Code Analysis」と完全に統合されています。 プロジェクトを開くだけで、コードの編集時に自動的に解析が実行され、問題がある場合はリアルタイムで警告が表示されます。
自動的に解析が実行されない場合は、リビルド、VS再起動、またはプロジェクトルートにある .vs フォルダー（隠しフォルダー）の削除を試してください。

## コーディングスタイル

本プロジェクトでは、コードスタイルを統一し、保守性を維持するために .editorconfig を採用しています。
* Visual Studio: .editorconfig は標準でサポートされています。
* VS Code: EditorConfig for VS Code 拡張機能のインストールを推奨します。

## ルールのカスタマイズ

デフォルトの警告レベルは設定済みですが、開発環境に合わせて .editorconfig で調整可能です。

**例: COL001の警告を info に変更する**
    dotnet_diagnostic.COL001.severity = warning
    ↓
    dotnet_diagnostic.COL001.severity = info

## Rule List (ルール一覧)

| ID | Title (JP) | Message (JP) |
|---|---|---|
| NULL001 | 演算子オーバーロードによる不具合の防止 | 演算子（== / !=）ではなく、型安全な 'is null' パターンを使用してください。 |
| STR001 | string.Emptyへの統一による最適化 | 'String.{0}' ではなく、一貫性を持たせるため小文字の 'string.Empty' を使用してください。 |
| STR002 | nameofへの置き換えによる安全性向上 | 文字列リテラル '{0}' ではなく、型安全な 'nameof({0})' を使用してください。 |
| LINQ001 | LINQ評価の統合によるパフォーマンス向上 | Where().{0}() のチェーンを、単一の '{0}(predicate)' に統合して最適化してください。 |
| LINQ002 | 不要なコレクションの実体化の削除 | '{0}' は列挙後に再利用されていません。メモリ確保を回避するため、呼び出しを削除してください。 |
| COLL001 | List初期キャパシティ指定によるメモリ削減 | ループ回数が予測可能なため、Listのコンストラクタに初期キャパシティを指定してください。 |
| COMM001 | 関数ヘッダーの欠落 | 関数ヘッダーが記述されていません。ドキュメントコメントを追加してください。 |
| COMM002 | 関数ヘッダーのパラメータ不一致 | 関数ヘッダーのパラメータがメソッド定義と一致していません。'{0}' を同期してください。 |
| CPX001 | メソッドの複雑度削減による可読性の向上 | メソッド '{0}' の複雑度が {1} です（閾値: 17）。分割やリファクタリングを検討してください。 |
| CPX002 | メソッド分割による保守性の向上 | メソッド '{0}' は {1} 行と長大です。関数呼び出しが {2} 回と少ないため、処理の分割を検討してください。 |
| COMP001 | 不等号演算子の向きの統一 | 可読性向上のため、不等号を反転させてください。 |