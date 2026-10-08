"""Generate the full component map and source registry from the verified official inventory."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SLUGS = "accordion alert alert-dialog aspect-ratio attachment avatar badge breadcrumb bubble button button-group calendar card carousel chart checkbox collapsible combobox command context-menu data-table date-picker dialog direction drawer dropdown-menu empty field hover-card input input-group input-otp item kbd label marker menubar message message-scroller native-select navigation-menu pagination popover progress questionnaire radio-group resizable scroll-area select separator sheet sidebar skeleton slider spinner switch table tabs textarea toast toggle toggle-group tooltip typography".split()
CUSTOM = {
    "accordion": ("Collapsible.cs", "AccordionPanel", "単一・複数展開、無効化、ネイティブボタンでの操作"),
    "aspect-ratio": ("AspectRatioPanel.cs", "AspectRatioPanel", "内容を指定比率に保ち、利用可能な領域に収める"),
    "attachment": ("Attachments.cs", "AttachmentView / AttachmentList", "名前・サイズ・画像・状態・進捗・表示・削除。ファイル選択はOpenFileDialog"),
    "avatar": ("Avatar.cs", "Avatar", "画像がない/読込失敗時の文字フォールバック。グループと件数はRowとLabelで合成"),
    "breadcrumb": ("Breadcrumb.cs", "Breadcrumb", "階層リンク、現在位置、長いパスの省略とメニュー、移動コールバック"),
    "bubble": ("Messages.cs", "MessageBubble", "会話本文、展開/折りたたみ、リアクション、任意の内容と操作"),
    "carousel": ("CarouselPanel.cs", "CarouselPanel", "前後移動、境界、ループ、位置表示、Ctrl+左右キー。アニメーションは設けない"),
    "chart": ("Chart.cs", "Chart", "棒・線・面・円・ドーナツ・レーダー・放射、複数系列、積上げ、軸、凡例、値のヒント"),
    "collapsible": ("Collapsible.cs", "CollapsibleSection", "内容の開閉、外部からの状態設定、状態変更通知"),
    "combobox": ("ChoicePicker.cs", "ChoicePicker<TKey>", "検索、グループ、単一/複数選択、無効な候補、選択維持。単純な候補は標準ComboBox"),
    "command": ("CommandPalette.cs", "CommandPalette", "名前・グループ・ショートカットで検索、Enterで実行、Escで閉じる"),
    "data-table": ("../DataGrid.cs", "DataGrid<T>", "List<T>、DisplayName、型付き選択、検索・ソート、列表示、ページング。行操作と複数選択は標準Grid API"),
    "date-picker": ("../NaturalDates.cs", "Field.Date / Field.DateText", "DateTimePickerと2つの日付入力で範囲・時刻・プリセット。文字入力は日本語/英語の相対日付も解析"),
    "drawer": ("SurfacePopup.cs", "DrawerForm", "4方向、ネイティブのモーダル/非モーダル、ドラッグハンドル、スナップサイズ、入れ子"),
    "field": ("../InputField.cs", "InputField<T> / ModelForm<T>", "ラベル・ヘルプ・エラー、任意の入力、レスポンシブ配置、モデル読取、DataAnnotations、変更/リセット"),
    "hover-card": ("SurfacePopup.cs", "SurfacePopup.AttachHover", "開閉遅延、ホバー・フォーカス、操作可能な内容。基盤はToolStripDropDown"),
    "input-group": ("InputGroup.cs", "InputGroup", "入力の前後・上部・下部に文字・ボタン・その他の標準部品を配置"),
    "message": ("Messages.cs", "ConversationMessage", "ID、送信者、本文、アバター、左右配置、ヘッダー/フッター、任意の操作・添付"),
    "message-scroller": ("MessageScroller.cs", "MessageScroller", "読書位置、ターン開始、明示的な追従/停止、履歴追加、ストリーム更新、再開位置、IDへの移動、行の仮想化"),
    "pagination": ("Pagination.cs", "Pagination", "最初・前・次・最後、ページサイズ、件数、検索後のリセット、DataGridへの接続"),
    "popover": ("SurfacePopup.cs", "SurfacePopup.Show", "任意の入力や操作を含むポップオーバー。ネイティブDropDownの画面収容・外側クリック・Escを使用"),
    "questionnaire": ("Questionnaire.cs", "Questionnaire", "単一/複数/自由入力、必須、明示スキップ、条件付き項目、独自検証、戻る/次へ、状態保存・再開、送信"),
    "sheet": ("SurfacePopup.cs", "SheetForm", "所有フォームの端に配置、サイズ設定、ネイティブのモーダル/非モーダル・閉じる・フォーカス"),
    "slider": ("MultiSlider.cs", "MultiSlider", "単一値はTrackBar。範囲や複数値だけを複数TrackBarで追加し、値の交差を防止"),
    "toast": ("Toasts.cs", "ToastHost / ToastNotification", "自動消去、閉じる、操作、非同期の処理中/成功/エラー状態"),
}
NATIVE = {
    "alert": "Label + Panel / GroupBox。操作はButtonを添える", "alert-dialog": "MessageBox.Show。ボタン・戻り値・モーダル・所有者は標準機能",
    "badge": "Label / LinkLabel。画像、文字、件数、色は標準プロパティとテーマ", "button": "Button / LinkLabel。アイコンはImage、無効化はEnabled",
    "button-group": "FlowLayoutPanel / ToolStripで既存ボタンを並べる", "calendar": "MonthCalendar。SelectionRange、MaxSelectionCount、BoldedDates、週番号とロケール",
    "card": "GroupBox / Panel + Label + 内容 + ボタン。追加の対話機能はない", "checkbox": "CheckBox。3状態、Enabled、入力検証はField",
    "context-menu": "ContextMenuStrip。入れ子、ショートカット、チェック、アイコン、クリック通知", "dialog": "Form.ShowDialog。所有者、スクロール、AcceptButton/CancelButton、DialogResult",
    "direction": "Control.RightToLeftとForm.RightToLeftLayout", "dropdown-menu": "ToolStripDropDownButton / ContextMenuStrip",
    "empty": "GroupBox / Panel + Label + 任意の操作。状態の表示のみ", "input": "TextBox。入力制約・無効化・パスワード・キー操作とField",
    "input-otp": "MaskedTextBox。数字/英数字マスク、コピー・貼付、区切り、MaskCompleted、ReadOnly、Enabled",
    "item": "ListView / Panelで画像・タイトル・説明・操作を合成", "kbd": "Label。実際の操作はKeys、ShortcutKeys、ProcessCmdKey",
    "label": "Label / LinkLabel。AccessibleNameとTabIndexもネイティブ", "marker": "Label / ProgressBar / Panel。状態・説明・区切り・任意の操作を合成",
    "menubar": "MenuStrip。サブメニュー、チェック、ショートカット、アイコン", "native-select": "ComboBox.DropDownList / Field.Selectのキーと表示名",
    "navigation-menu": "MenuStrip / TreeView / LinkLabel。移動は標準Click/AfterSelect", "progress": "ProgressBar + Label。値・進行度・処理中",
    "radio-group": "同じPanel内のRadioButton。単一選択、無効化、Appearance.Buttonも標準", "resizable": "SplitContainer。入れ子・向き・分割バー・標準キーボード操作",
    "scroll-area": "Panel.AutoScroll / HScrollBar / VScrollBar。独自のスクロールバーを再描画しない",
    "select": "ComboBox / Field.Select。高度な検索/グループ/複数選択はChoicePicker", "separator": "Panel.BorderStyle / ToolStripSeparator / Label",
    "sidebar": "SplitContainer + TreeView / Panel。折りたたみはPanel1Collapsed、入れ子メニューはTreeNode",
    "skeleton": "Panel / Labelをプレースホルダーとして表示しVisibleで切替。装飾アニメーションは後回し",
    "spinner": "ProgressBar.Style = Marquee。円形の再描画はデザイン段階", "switch": "CheckBox。2状態の操作は標準、外観はテンプレート",
    "table": "DataGridView。表示・列/セル形式・フッター・行操作は標準API", "tabs": "TabControl。選択、向き、画像、ページの追加/削除",
    "textarea": "TextBox.Multiline / RichTextBox", "toggle": "CheckBox.Appearance = Button",
    "toggle-group": "CheckBox / RadioButtonのAppearance.ButtonをPanelでグループ化",
    "tooltip": "ToolTip。表示遅延、位置、所有者。キーボードフォーカスはEnterイベントからShow",
    "typography": "Label.Font / RichTextBox。見出し・段落・リスト・コードの書式は標準プロパティとテーマ",
}

if __name__ == "__main__":
    assert len(SLUGS) == 64 and set(CUSTOM) | set(NATIVE) == set(SLUGS) and not (set(CUSTOM) & set(NATIVE))
    rows = []
    for slug in SLUGS:
        entry = {"id": slug, "url": f"https://ui.shadcn.com/docs/components/base/{slug}"}
        if slug in CUSTOM:
            file, api, note = CUSTOM[slug]
            source = Path("src/WinformsUI/Components") / file
            source = (ROOT / source).resolve().relative_to(ROOT).as_posix()
            assert (ROOT / source).is_file(), source
            entry.update(strategy="composite", api=api, source=source, note=note)
        else:
            entry.update(strategy="native", api=NATIVE[slug], source="samples/WinformsUI.Sample/ComponentGalleryForm.cs", note="標準機能を再実装しない")
        rows.append(entry)
    (ROOT / "docs/components.json").write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding="utf-8")
    intro = """# コンポーネント対応表

参照: [公式コンポーネント一覧](https://ui.shadcn.com/docs/components)（2026-10-08に64項目を確認）。
操作・データ・状態・検証の機能を対応付けています。CSSの外観、Web固有のDOM/SSR、React/Tailwind依存の実装方法は移植対象ではありません。
標準部品で同じ操作ができるものは新しいコントロールを作りません。標準部品にない動作は小さい合成部品として実装します。
見た目はNativeを既定値とし、JSONテンプレートのフォント・色・余白・ロール・チャート色で一括差し替えできます。

サンプルを `dotnet run --project samples/WinformsUI.Sample -- --gallery` で起動してください。
対話機能の検証はサンプル内の `Verification.cs` / `ExtendedVerification.cs`、描画画像と実行結果は `artifacts/verification/` にあります。
標準部品の全プロパティを別APIとして包み直すことはしません。対応表に記載した標準APIを直接使ってください。

| 元の部品 | 方針 | API / ソース | 対応する機能 |
| --- | --- | --- | --- |
"""
    table = [f"| [{row['id']}]({row['url']}) | {'標準' if row['strategy']=='native' else '合成'} | [{row['api']}](../{row['source']}) | {row['note']} |" for row in rows]
    (ROOT / "docs/COMPONENTS.md").write_text(intro + "\n".join(table) + "\n", encoding="utf-8")
    source_files = sorted(path.relative_to(ROOT).as_posix() for path in (ROOT / "src/WinformsUI").rglob("*.cs") if "obj" not in path.parts and "bin" not in path.parts)
    registry = {"name": "WinformsUI", "targetFramework": "net10.0-windows", "sourceFiles": source_files, "components": rows}
    (ROOT / "components.json").write_text(json.dumps(registry, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Mapped all {len(rows)} components: {len(CUSTOM)} composites, {len(NATIVE)} native alternatives; {len(source_files)} source files.")
