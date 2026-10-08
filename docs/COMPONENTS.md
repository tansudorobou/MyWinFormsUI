# コンポーネント対応表

参照: [公式コンポーネント一覧](https://ui.shadcn.com/docs/components)（2026-10-08に64項目を確認）。
操作・データ・状態・検証の機能を対応付けています。CSSの外観、Web固有のDOM/SSR、React/Tailwind依存の実装方法は移植対象ではありません。
標準部品で同じ操作ができるものは新しいコントロールを作りません。標準部品にない動作は小さい合成部品として実装します。
見た目はNativeを既定値とし、JSONテンプレートのフォント・色・余白・角丸・ロール・チャート色で一括差し替えできます。Normal（緑）とDarkも同梱しています。
製造工程用の追加部品 `GanttChart` / `GanttTask` は [GanttChart.cs](../src/WinformsUI/Components/GanttChart.cs) とREADMEのガント節を参照してください。

サンプルを `dotnet run --project samples/WinformsUI.Sample -- --gallery` で起動してください。
対話機能の検証はサンプル内の `Verification.cs` / `ExtendedVerification.cs`、描画画像と実行結果は `artifacts/verification/` にあります。
標準部品の全プロパティを別APIとして包み直すことはしません。対応表に記載した標準APIを直接使ってください。

| 元の部品 | 方針 | API / ソース | 対応する機能 |
| --- | --- | --- | --- |
| [accordion](https://ui.shadcn.com/docs/components/base/accordion) | 合成 | [AccordionPanel](../src/WinformsUI/Components/Collapsible.cs) | 単一・複数展開、無効化、ネイティブボタンでの操作 |
| [alert](https://ui.shadcn.com/docs/components/base/alert) | 標準 | [Label + Panel / GroupBox。操作はButtonを添える](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [alert-dialog](https://ui.shadcn.com/docs/components/base/alert-dialog) | 標準 | [MessageBox.Show。ボタン・戻り値・モーダル・所有者は標準機能](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [aspect-ratio](https://ui.shadcn.com/docs/components/base/aspect-ratio) | 合成 | [AspectRatioPanel](../src/WinformsUI/Components/AspectRatioPanel.cs) | 内容を指定比率に保ち、利用可能な領域に収める |
| [attachment](https://ui.shadcn.com/docs/components/base/attachment) | 合成 | [AttachmentView / AttachmentList](../src/WinformsUI/Components/Attachments.cs) | 名前・サイズ・画像・状態・進捗・表示・削除。ファイル選択はOpenFileDialog |
| [avatar](https://ui.shadcn.com/docs/components/base/avatar) | 合成 | [Avatar](../src/WinformsUI/Components/Avatar.cs) | 画像がない/読込失敗時の文字フォールバック。グループと件数はRowとLabelで合成 |
| [badge](https://ui.shadcn.com/docs/components/base/badge) | 標準 | [Label / LinkLabel。画像、文字、件数、色は標準プロパティとテーマ](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [breadcrumb](https://ui.shadcn.com/docs/components/base/breadcrumb) | 合成 | [Breadcrumb](../src/WinformsUI/Components/Breadcrumb.cs) | 階層リンク、現在位置、長いパスの省略とメニュー、移動コールバック |
| [bubble](https://ui.shadcn.com/docs/components/base/bubble) | 合成 | [MessageBubble](../src/WinformsUI/Components/Messages.cs) | 会話本文、展開/折りたたみ、リアクション、任意の内容と操作 |
| [button](https://ui.shadcn.com/docs/components/base/button) | 標準 | [Button / LinkLabel。アイコンはImage、無効化はEnabled](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [button-group](https://ui.shadcn.com/docs/components/base/button-group) | 標準 | [FlowLayoutPanel / ToolStripで既存ボタンを並べる](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [calendar](https://ui.shadcn.com/docs/components/base/calendar) | 標準 | [MonthCalendar。SelectionRange、MaxSelectionCount、BoldedDates、週番号とロケール](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [card](https://ui.shadcn.com/docs/components/base/card) | 標準 | [GroupBox / Panel + Label + 内容 + ボタン。追加の対話機能はない](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [carousel](https://ui.shadcn.com/docs/components/base/carousel) | 合成 | [CarouselPanel](../src/WinformsUI/Components/CarouselPanel.cs) | 前後移動、境界、ループ、位置表示、Ctrl+左右キー。アニメーションは設けない |
| [chart](https://ui.shadcn.com/docs/components/base/chart) | 合成 | [Chart](../src/WinformsUI/Components/Chart.cs) | 棒・線・面・円・ドーナツ・レーダー・放射、複数系列、積上げ、軸、凡例、値のヒント |
| [checkbox](https://ui.shadcn.com/docs/components/base/checkbox) | 標準 | [CheckBox。3状態、Enabled、入力検証はField](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [collapsible](https://ui.shadcn.com/docs/components/base/collapsible) | 合成 | [CollapsibleSection](../src/WinformsUI/Components/Collapsible.cs) | 内容の開閉、外部からの状態設定、状態変更通知 |
| [combobox](https://ui.shadcn.com/docs/components/base/combobox) | 合成 | [ChoicePicker<TKey>](../src/WinformsUI/Components/ChoicePicker.cs) | 検索、グループ、単一/複数選択、無効な候補、選択維持。単純な候補は標準ComboBox |
| [command](https://ui.shadcn.com/docs/components/base/command) | 合成 | [CommandPalette](../src/WinformsUI/Components/CommandPalette.cs) | 名前・グループ・ショートカットで検索、Enterで実行、Escで閉じる |
| [context-menu](https://ui.shadcn.com/docs/components/base/context-menu) | 標準 | [ContextMenuStrip。入れ子、ショートカット、チェック、アイコン、クリック通知](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [data-table](https://ui.shadcn.com/docs/components/base/data-table) | 合成 | [DataGrid<T>](../src/WinformsUI/DataGrid.cs) | List<T>、DisplayName、型付き選択、検索・ソート、列表示、ページング。行操作と複数選択は標準Grid API |
| [date-picker](https://ui.shadcn.com/docs/components/base/date-picker) | 合成 | [Field.Date / Field.DateText](../src/WinformsUI/NaturalDates.cs) | DateTimePickerと2つの日付入力で範囲・時刻・プリセット。文字入力は日本語/英語の相対日付も解析 |
| [dialog](https://ui.shadcn.com/docs/components/base/dialog) | 標準 | [Form.ShowDialog。所有者、スクロール、AcceptButton/CancelButton、DialogResult](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [direction](https://ui.shadcn.com/docs/components/base/direction) | 標準 | [Control.RightToLeftとForm.RightToLeftLayout](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [drawer](https://ui.shadcn.com/docs/components/base/drawer) | 合成 | [DrawerForm](../src/WinformsUI/Components/SurfacePopup.cs) | 4方向、ネイティブのモーダル/非モーダル、ドラッグハンドル、スナップサイズ、入れ子 |
| [dropdown-menu](https://ui.shadcn.com/docs/components/base/dropdown-menu) | 標準 | [ToolStripDropDownButton / ContextMenuStrip](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [empty](https://ui.shadcn.com/docs/components/base/empty) | 標準 | [GroupBox / Panel + Label + 任意の操作。状態の表示のみ](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [field](https://ui.shadcn.com/docs/components/base/field) | 合成 | [InputField<T> / ModelForm<T>](../src/WinformsUI/InputField.cs) | ラベル・ヘルプ・エラー、任意の入力、レスポンシブ配置、モデル読取、DataAnnotations、変更/リセット |
| [hover-card](https://ui.shadcn.com/docs/components/base/hover-card) | 合成 | [SurfacePopup.AttachHover](../src/WinformsUI/Components/SurfacePopup.cs) | 開閉遅延、ホバー・フォーカス、操作可能な内容。基盤はToolStripDropDown |
| [input](https://ui.shadcn.com/docs/components/base/input) | 標準 | [TextBox。入力制約・無効化・パスワード・キー操作とField](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [input-group](https://ui.shadcn.com/docs/components/base/input-group) | 合成 | [InputGroup](../src/WinformsUI/Components/InputGroup.cs) | 入力の前後・上部・下部に文字・ボタン・その他の標準部品を配置 |
| [input-otp](https://ui.shadcn.com/docs/components/base/input-otp) | 標準 | [MaskedTextBox。数字/英数字マスク、コピー・貼付、区切り、MaskCompleted、ReadOnly、Enabled](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [item](https://ui.shadcn.com/docs/components/base/item) | 標準 | [ListView / Panelで画像・タイトル・説明・操作を合成](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [kbd](https://ui.shadcn.com/docs/components/base/kbd) | 標準 | [Label。実際の操作はKeys、ShortcutKeys、ProcessCmdKey](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [label](https://ui.shadcn.com/docs/components/base/label) | 標準 | [Label / LinkLabel。AccessibleNameとTabIndexもネイティブ](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [marker](https://ui.shadcn.com/docs/components/base/marker) | 標準 | [Label / ProgressBar / Panel。状態・説明・区切り・任意の操作を合成](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [menubar](https://ui.shadcn.com/docs/components/base/menubar) | 標準 | [MenuStrip。サブメニュー、チェック、ショートカット、アイコン](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [message](https://ui.shadcn.com/docs/components/base/message) | 合成 | [ConversationMessage](../src/WinformsUI/Components/Messages.cs) | ID、送信者、本文、アバター、左右配置、ヘッダー/フッター、任意の操作・添付 |
| [message-scroller](https://ui.shadcn.com/docs/components/base/message-scroller) | 合成 | [MessageScroller](../src/WinformsUI/Components/MessageScroller.cs) | 読書位置、ターン開始、明示的な追従/停止、履歴追加、ストリーム更新、再開位置、IDへの移動、行の仮想化 |
| [native-select](https://ui.shadcn.com/docs/components/base/native-select) | 標準 | [ComboBox.DropDownList / Field.Selectのキーと表示名](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [navigation-menu](https://ui.shadcn.com/docs/components/base/navigation-menu) | 標準 | [MenuStrip / TreeView / LinkLabel。移動は標準Click/AfterSelect](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [pagination](https://ui.shadcn.com/docs/components/base/pagination) | 合成 | [Pagination](../src/WinformsUI/Components/Pagination.cs) | 最初・前・次・最後、ページサイズ、件数、検索後のリセット、DataGridへの接続 |
| [popover](https://ui.shadcn.com/docs/components/base/popover) | 合成 | [SurfacePopup.Show](../src/WinformsUI/Components/SurfacePopup.cs) | 任意の入力や操作を含むポップオーバー。ネイティブDropDownの画面収容・外側クリック・Escを使用 |
| [progress](https://ui.shadcn.com/docs/components/base/progress) | 標準 | [ProgressBar + Label。値・進行度・処理中](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [questionnaire](https://ui.shadcn.com/docs/components/base/questionnaire) | 合成 | [Questionnaire](../src/WinformsUI/Components/Questionnaire.cs) | 単一/複数/自由入力、必須、明示スキップ、条件付き項目、独自検証、戻る/次へ、状態保存・再開、送信 |
| [radio-group](https://ui.shadcn.com/docs/components/base/radio-group) | 標準 | [同じPanel内のRadioButton。単一選択、無効化、Appearance.Buttonも標準](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [resizable](https://ui.shadcn.com/docs/components/base/resizable) | 標準 | [SplitContainer。入れ子・向き・分割バー・標準キーボード操作](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [scroll-area](https://ui.shadcn.com/docs/components/base/scroll-area) | 標準 | [Panel.AutoScroll / HScrollBar / VScrollBar。独自のスクロールバーを再描画しない](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [select](https://ui.shadcn.com/docs/components/base/select) | 標準 | [ComboBox / Field.Select。高度な検索/グループ/複数選択はChoicePicker](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [separator](https://ui.shadcn.com/docs/components/base/separator) | 標準 | [Panel.BorderStyle / ToolStripSeparator / Label](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [sheet](https://ui.shadcn.com/docs/components/base/sheet) | 合成 | [SheetForm](../src/WinformsUI/Components/SurfacePopup.cs) | 所有フォームの端に配置、サイズ設定、ネイティブのモーダル/非モーダル・閉じる・フォーカス |
| [sidebar](https://ui.shadcn.com/docs/components/base/sidebar) | 標準 | [SplitContainer + TreeView / Panel。折りたたみはPanel1Collapsed、入れ子メニューはTreeNode](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [skeleton](https://ui.shadcn.com/docs/components/base/skeleton) | 標準 | [Panel / Labelをプレースホルダーとして表示しVisibleで切替。装飾アニメーションは後回し](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [slider](https://ui.shadcn.com/docs/components/base/slider) | 合成 | [MultiSlider](../src/WinformsUI/Components/MultiSlider.cs) | 単一値はTrackBar。範囲や複数値だけを複数TrackBarで追加し、値の交差を防止 |
| [spinner](https://ui.shadcn.com/docs/components/base/spinner) | 標準 | [ProgressBar.Style = Marquee。円形の再描画はデザイン段階](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [switch](https://ui.shadcn.com/docs/components/base/switch) | 標準 | [CheckBox。2状態の操作は標準、外観はテンプレート](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [table](https://ui.shadcn.com/docs/components/base/table) | 標準 | [DataGridView。表示・列/セル形式・フッター・行操作は標準API](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [tabs](https://ui.shadcn.com/docs/components/base/tabs) | 標準 | [TabControl。選択、向き、画像、ページの追加/削除](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [textarea](https://ui.shadcn.com/docs/components/base/textarea) | 標準 | [TextBox.Multiline / RichTextBox](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [toast](https://ui.shadcn.com/docs/components/base/toast) | 合成 | [ToastHost / ToastNotification](../src/WinformsUI/Components/Toasts.cs) | 自動消去、閉じる、操作、非同期の処理中/成功/エラー状態 |
| [toggle](https://ui.shadcn.com/docs/components/base/toggle) | 標準 | [CheckBox.Appearance = Button](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [toggle-group](https://ui.shadcn.com/docs/components/base/toggle-group) | 標準 | [CheckBox / RadioButtonのAppearance.ButtonをPanelでグループ化](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [tooltip](https://ui.shadcn.com/docs/components/base/tooltip) | 標準 | [ToolTip。表示遅延、位置、所有者。キーボードフォーカスはEnterイベントからShow](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
| [typography](https://ui.shadcn.com/docs/components/base/typography) | 標準 | [Label.Font / RichTextBox。見出し・段落・リスト・コードの書式は標準プロパティとテーマ](../samples/WinformsUI.Sample/ComponentGalleryForm.cs) | 標準機能を再実装しない |
