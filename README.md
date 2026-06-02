# CSharp_IngeniousAnalyzer
A static analyzer designed to dramatically improve the code quality of your C# projects. It automatically detects issues such as insecure null checks, inefficient LINQ queries, and magic numbers, helping you maintain a safe and clean codebase.
<br>
<br>

# How to use
This analyzer is fully integrated with Visual Studio's "Live Code Analysis." Simply open your project, and it will automatically analyze your code as you edit it, providing real-time warnings.
<br>
* If it does not run automatically, try rebuilding the project, restarting Visual Studio, or deleting the hidden `.vs` folder in your project root.
<br>
<br>

# Coding Style
We use `.editorconfig` to enforce a unified code style and maintain high maintainability. We recommend ensuring the following settings are applied to maintain consistent code quality:
<br>
* **Visual Studio**: Supports `.editorconfig` by default.
* **VS Code**: Installing the [EditorConfig for VS Code](https://marketplace.visualstudio.com/items?itemName=EditorConfig.EditorConfig) extension is recommended.
<br>
<br>

# Customizing Rules
Default warning levels are set, but you can adjust them to fit your development environment or preferences. For example, to change a rule's severity from `warning` to `info`, modify your `.editorconfig` as follows:
<br>

### Example: Changing COL001 from 'warning' to 'info'
### Before
`dotnet_diagnostic.COL001.severity = warning`
### After
`dotnet_diagnostic.COL001.severity = info`
<br>
<br>

# 

<br>
<br>

# CSharp-CSharp-IngeniousAnalyzer (日本語)
C#のコード品質を劇的に高める静的アナライザーです。NULLチェックの型安全性欠如や、非効率なLINQ等を自動検知し、安全でクリーンなコードへの修正を支援します。
<br>
<br>

# 使い方
本アナライザーは Visual Studio の「Live Code Analysis」と完全に統合されています。プロジェクトを開くだけで、コードの編集時に自動的に解析が実行され、問題がある場合はリアルタイムで警告が表示されます。
<br>
* 自動的に解析が実行されない場合は、リビルド、VS再起動、またはプロジェクトルートにある `.vs` フォルダー（隠しフォルダー）の削除を試してください。
<br>
<br>

# コーディングスタイル
本プロジェクトでは、コードスタイルを統一し、保守性を維持するために `.editorconfig` を採用しています。エディタの自動フォーマット機能を活用し、常に一貫したコード品質を保つため、開発時には以下の設定が反映されていることを推奨します。
<br>
* **Visual Studio**: `.editorconfig` は標準でサポートされています。
* **VS Code**: [EditorConfig for VS Code](https://marketplace.visualstudio.com/items?itemName=EditorConfig.EditorConfig) 拡張機能のインストールを推奨します。
<br>
<br>

# ルールのカスタマイズ
本プロジェクトではデフォルトで一定の警告レベルを設定していますが、開発環境や個人の好みに合わせて調整可能です。例えば、特定の警告を `warning`（警告）から `info`（情報）に変更したい場合は、プロジェクトルートの `.editorconfig` を開き、該当するルールを以下のように修正してください。
<br>

### 例: COL001の 警告 を info に変更する
### 変更前
`dotnet_diagnostic.COL001.severity = warning`
### 変更後
`dotnet_diagnostic.COL001.severity = info`