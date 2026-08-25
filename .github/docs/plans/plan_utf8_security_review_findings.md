# UTF-8 / Unicode PR Harness 敵対的レビュー Findings

## 1. 対象と前提

- 対象ブランチ: `utf8`
- 比較元: `main`
- 対象機能: `scan-pr-unicode` と `pr-harness.yaml` の `unicode-security` job
- 確認済み:
  - `dotnet test CysharpActions.slnx --no-restore`: 134 passed、5 skipped、0 failed
  - `actionlint .github/workflows/pr-harness.yaml`: pass
  - Unicode 17.0 の `Cf` と `Default_Ignorable_Code_Point` の固定テーブルに既知の差分なし

以下はテスト成功だけでは検出できない、敵対的なPR入力と運用境界を中心としたFindingである。

## 2. 対応対象のFindings

### P2: `git diff`出力と変更ファイル数に上限がない

状態: 対応済み

採用設定:

- 変更ファイル数: 最大3,000件
- `git diff --raw -z`出力: 最大16 MiB
- NUL区切りfield数: 最大9,000件（rename/copy時の3 field × 3,000ファイル）

対象:

- `src/CysharpActions/Commands/ScanPrUnicodeCommand.cs`
- `GitPrChangeSource.RunGitAsync`
- `SplitNullTerminated`

`git diff --raw -z`の標準出力を無制限の`MemoryStream`へ格納し、その後、各NUL区切りfieldを別の`byte[]`へコピーしている。10 MiB/ファイルおよび100 MiB/PRの制限はC#本文にしか適用されないため、攻撃者は大量の非C#ファイルや長いファイル名を追加し、本文制限とは独立してrunnerのメモリを消費できる。

対応案:

1. `git diff`出力に明示的な最大byte数を設け、超過時はhard errorにする。
2. 変更ファイル数にも上限を設ける。
3. 可能ならNUL区切り出力をstreaming parseし、全出力と全fieldを同時に保持しない。

完了条件:

- 非C#ファイルだけでも上限が機能する。
- 上限超過はscan成功ではなく明示的な失敗になる。
- 上限直前、直後、および非常に長いファイル名のテストがある。

実装では出力上限を読込中に判定し、超過時はGitプロセスを停止する。これにより、上限判定前に全出力を`MemoryStream`へ保持することを防ぐ。変更ファイル数は削除ファイルも含めて数え、3,000件を超えた時点でhard errorにする。

### P2: 既存の`.cs`/`.csx`シンボリックリンクから検査対象外ファイルを参照できる

対象:

- `GitPrChangeSource.VisitChangedFilesAsync`
- Git mode `120000`の判定

現在はPRの変更一覧に現れた`.cs`/`.csx`だけGit modeとworking tree上のリンクを確認する。repositoryに既存の`Link.cs -> Payload.txt`が存在し、PRが`Payload.txt`だけを変更した場合、`Payload.txt`は非C#として本文検査を受けない一方、buildでは`Link.cs`経由で読み込まれる可能性がある。

新規symlinkの追加は現在の検査で拒否されるため、成立条件は「base側に既存symlinkがあること」である。しかしCysharp/Actionsは複数のCysharp OSSへ適用されるため、各repositoryの既存状態を暗黙に信頼しない方がよい。

対応案:

- PRごとに、tracked tree全体の`.cs`/`.csx`についてGit mode `120000`が存在しないことを一括確認する。
- ファイルごとのGitプロセスには戻さず、`git ls-files --stage -z`など単一プロセスでrepository invariantを検証する。

完了条件:

- 変更されていない`.cs` symlinkの参照先だけを変更するテストが失敗する。
- 通常ファイル数に比例してGitプロセス数が増えない。

### P2: 違反ファイル名の制御文字をActionsログへ再出力できる

対象:

- `ScanPrUnicodeCommand.WriteAnnotation`
- `EscapeProperty`
- `EscapeData`

ファイル名に含まれるCR、LF、`%`などはescapeされるが、ESCを含むその他のC0/C1制御文字はそのままannotationへ出力される。スキャナーがまさに検出する攻撃者入力をログへ反射するため、ANSI escape sequenceによるログ表示の改変や監査性低下が起こり得る。

対応案:

- annotation/log表示用のsource名では、全C0/C1制御文字、format文字、default-ignorable文字を`\\uXXXX`または`\\UXXXXXXXX`へ可視化する。
- GitHub workflow command用escapeと、人間向けの不可視文字可視化を別処理にする。

完了条件:

- ESC、TAB、C1 control、bidi controlを含むファイル名がログへ生で出力されない。
- 改行によるworkflow command injection防止を維持する。

### P3: READMEの「head version」と実装のworking tree検査が一致しない

対象:

- `README.md`の`pr-harness`説明
- `GitPrChangeSource.VisitChangedFilesAsync`

READMEは変更されたC#ファイルの「complete head version」を検査すると説明しているが、現在の実装は`base..head`から変更ファイル名を取得し、本文はcheckout済みworking treeから読む。PRでmerge commitがcheckoutされていれば、本文はhead treeではなくmerge結果である。

対応案:

- READMEを「checkout済みworking treeの完全なファイル内容」に修正する。
- changed lineだけでなくchanged file全体を検査する点は維持する。

### P3: self `dotnet run`が.NET SDKのrunnerイメージへ依存する

対象:

- `.github/workflows/pr-harness.yaml`の`Set Cysharp/Actions binary path`

Cysharp/Actions自身では`dotnet run`を使うが、job内で.NET 9 SDKを明示的にsetupしていない。現在の`ubuntu-24.04`イメージで動作しても、runner image更新によって利用可能なSDKが変わる可能性がある。また初回restore/buildを含むため、5分timeoutに対する余裕も配布バイナリ実行時より小さい。

対応案:

- `github.repository == 'Cysharp/Actions'`の場合だけ`setup-dotnet`で`9.0.x`を準備する。
- 実測時間を確認し、必要ならtimeoutを調整する。

### P3: バイナリ探索失敗時のdebug `ls`が途中終了し得る

対象:

- `.github/workflows/pr-harness.yaml`の`Set Cysharp/Actions binary path`

GitHub Actionsのbash stepは通常`-e`で実行される。debug対象ディレクトリ自体が存在しない場合、最初の`ls -lR`でstepが終了し、後続のdebug groupや意図した`exit 1`まで到達しない。

対応案:

```bash
ls -lR "${{ github.workspace }}/../../_actions/" || true
ls -lR "${{ github.workspace }}/../../_actions/Cysharp/Actions/" || true
```

## 3. 今回は保留または許容する事項

### バイナリ展開順序

`scan-pr-unicode`を呼び出すworkflowと、同commandを含む配布バイナリの展開順序はrepository運用側で調整する。このplanでは追加対応の対象外とする。

### Cysharp/Actions自身でPR側の`dotnet run`を実行すること

検査対象PRが変更できる`./src/CysharpActions`と`.csproj`をsecurity job内でbuild/runするため、スキャナー無効化やMSBuild target実行が可能になる。このリスクは認識したうえで、今回の変更では後回しとする。

将来対応する場合は、PR working treeではなく`@main`として取得したtrusted sourceまたはtrusted binaryを実行する。

### command文字列のword splitting

step outputへ`dotnet run ... --`または実行ファイルpathを格納し、後段でcommandとして展開する方式は既存運用実績を優先して許容する。値の生成元を固定し、PR入力を混入させないことを前提とする。

## 4. 推奨対応順序

1. `git diff`出力と変更ファイル数の上限を追加する。
2. tracked tree全体でC# symlink禁止を保証する。
3. annotationへ出す攻撃者制御文字を可視化する。
4. READMEをworking tree検査へ合わせる。
5. self実行時の.NET 9 setupとdebug出力を安定化する。
