# 外部UI検証

```powershell
dotnet run --project tests/Tansu.Forms.UiTests
```

WinFormsサンプルの `--automation-sample` 画面を別プロセスで起動し、FlaUI.UIA3から操作します。
テスト対象のDLLや内部メソッドは呼びません。Valueパターンで値を設定し、Invokeパターンでボタンを実行します。

- モデルのプロパティ名から生成したAutomationIdと、日本語の表示名。
- 必須・型変換エラー、フォームの未保存状態、保存ボタンの実行可否。
- 非同期処理の開始・完了・取消・失敗と、入力/ボタンの無効化と復帰。
- 保存成功後の基準値とリセット。

結果は `artifacts/ui-verification/results.txt` に記録します。終了コードは成功時0、失敗時1です。
Windowsの対話セッションで実行してください。対象画面は画面外に表示し、終了時にはテストが起動したプロセスだけを閉じます。
通常は現在の構成（Debug/Release）のサンプルexeを自動選択します。別のexeを検証する場合は、その絶対パスを引数として渡せます。
