# CysharpActions コードベース・テスト改善計画

## 1. 目的と評価条件

`src/CysharpActions` は、読解・制御・保守が難しい bash を C# に移しつつ、ProcessX の zx 構文で外部コマンドを簡潔に呼び出すための実行基盤である。本計画では、この方針を維持したまま次を改善する。

- コマンドの入力、判断、結果、副作用を上から順に読めること
- データを中心にし、継承階層、DI コンテナ、コマンドごとの interface 群を増やさないこと
- workflow、CLI、配布済みバイナリまで含めて、テスト結果を信頼できること
- GitHub Actions 上の破壊的操作と秘密情報の扱いを明示的にすること

評価時点は 2026-08-18、対象コミットは `0a28cb2`。以下を実行した。

```console
dotnet build CysharpActions.slnx -c Release --no-restore
# 0 warnings, 0 errors

dotnet test CysharpActions.slnx -c Release --logger:"console;verbosity=normal"
# 71 passed, 0 failed, 0 skipped

dotnet run --project src/CysharpActions/CysharpActions.csproj \
  -c Release --no-build -- --help
# 11 commands を列挙
```

テストは成功しているが、後述の通り「成功扱いだが何も実行していない」CI 限定テストを含む。そのため、`71 passed` をそのまま実効カバレッジとは見なせない。コードカバレッジ収集・閾値設定は現在ない。

## 2. 現状の評価

### 良い点

1. CLI の入口は [Program.cs](../../../src/CysharpActions/Program.cs) に集約され、workflow から見えるコマンド名と C# の処理を追いやすい。
2. `UpdateVersion`、version increment、benchmark YAML 変換など、文字列・データ変換の多くは既にテスト可能な単位になっている。
3. ProcessX で `useShell = false` を選んでおり、bash の暗黙的な展開や shell 差異を減らす方向は妥当である。
4. benchmark の入力・出力を型付きデータとして扱い、JSON source generation も利用している。これはデータ指向の方針と合っている。
5. reusable workflow の結合テストがあり、`update-packagejson` と `create-release` はローカル workflow (`./.github/workflows/...`) を呼ぶ経路を持つ。

### 現在の処理構造

```text
workflow YAML
  -> Program.cs / ActionsBatch                 CLI 引数と GitHub output
    -> Commands/*Command.cs                    判断 + I/O + process/API 実行
      -> Utils/*                               glob、置換、ログ、ProcessX 補助
      -> Runtime/ActionEnvironment.cs          process environment -> immutable data
```

規模が小さい現在は成立している。しかし `*Command` の内部で「入力検証」「純粋な変換」「ファイル操作」「git/gh/API」「ログ」が混在しており、処理全体を理解するには複数の暗黙的な static state と外部状態を同時に読む必要がある。今後コマンドが増えるほど、この混在が把握の主な障害になる。

## 3. 優先度付きの問題と提案

### P0: 秘密情報を dry-run ログへ出さない

[NuGetCommand.cs](../../../src/CysharpActions/Commands/NuGetCommand.cs) は dry-run 時に API key を含むコマンド全文を `WriteRawLog` へ渡している。GitHub 側の masking に依存すべきではなく、ローカルログや値の加工時には漏洩し得る。

**対応状況（2026-08-18）:** 対応済み。秘密値を出力直前に置換する `GitHubActions.WriteRedactedRawLog` を追加し、dry-run の表示を `-k ***` に変更した。実際の console 出力に API key が含まれないこと、複数の秘密値と空文字を安全に処理できることを回帰テストで確認する。さらに外部副作用境界のP1で、汎用の`CommandSpec.SecretArguments`と常時redactedされる表示へ統合した。

対応:

- dry-run 表示は `-k ***` とし、秘密値を含む文字列をログ API へ渡さない。
- process 呼び出しを `CommandSpec(FileName, Arguments, SensitiveArgumentIndexes)` のようなデータで表し、表示用文字列の生成時に必ず redact する。
- API key が stdout/stderr、例外、snapshot に現れないテストを追加する。

### P0: 更新対象以外を commit しない

[GitCommand.cs](../../../src/CysharpActions/Commands/GitCommand.cs) の両 commit 経路は最初に `git add -A` を実行する。このため `UpdateVersion` が返した `modifiedPaths` 以外の変更・削除も commit 対象になる。さらに signed 経路は最後に `git reset --hard origin/...` を実行する。

**対応状況（2026-08-18）:** path allow-listによる直接対策は対応済み。unsigned / signed の両経路で、stage と差分取得を明示された pathspec のみに限定した。unsigned commit は `git commit --only` を使い、既にstageされていた対象外変更もcommitしない。signed treeも限定後の差分だけから構築し、同期は対象外のworking treeを消さない `git reset --mixed` に変更した。一時git repositoryのテストで、空白を含む対象pathだけがcommitされ、stage済みの別変更と未追跡ファイルがcommitされないことを確認する。

`update-packagejson` の `dotnet-run-path` は従来 `git add -A` に依存して生成物を暗黙にcommitしていた。この互換性を安全に置き換えるため、workflowへ `additional-commit-path`、CLIへ `additional-commit-path-string` を追加した。`file-path` はversion置換結果に関係なく常にcommit allow-listとなり、hookが同じファイルへ加えた変更も対象になる。`additional-commit-path` は `file-path` 外のhook生成物だけに使う。どちらも実差分がなければcommitには含まれず、全対象が無変更ならcommit自体をskipする。既存のworkflow testは `sandbox/VersionOutput/version.txt` を明示し、生成されたversionがcommitに含まれることも検査する。

配布バイナリ更新前との互換性のため、`additional-commit-path` が空なら新CLI optionを渡さない。この変更では通常の配布手順と同じ条件でLinux x64 / ARM64バイナリも再生成し、source、workflow、配布物を同時に更新する。既存利用者が `dotnet-run-path` で生成物をcommitしている場合は、その出力を `additional-commit-path` に列挙する。未指定時はnoticeを出し、対象外ファイルは意図的にcommitしない。

対応:

- stage 対象を明示されたcommit pathのみに限定する。ignoredな生成物も扱うため `git add -f -- <pathspec...>` を使う。
- 実行前に `GitCommitRequest(Tag, Paths, BranchMode, SigningMode)` をログへ出し、空 path や repository 外 path を拒否する。
- `reset --hard` は原則なくす。必要なら一時 clone/worktree 内でのみ許可し、その前提を executor の入力データで表す。
- signed tree 構築は rename、binary、symlink、実行 bit を正しく扱えていないため、対象ファイル形式を version metadata の text file に限定して検証するか、git blob/tree を用いる実装へ変える。

### P0: `dry-run` の契約を実態に合わせる

[UpdateVersionCommand.cs](../../../src/CysharpActions/Commands/UpdateVersionCommand.cs) は `dryRun` を受け取るが常にファイルを書き換える。git 側でも dry-run は「何もしない」ではなく、`test-release/{tag}` branch を作成して commit/API 更新する意味である。release workflow の dry-run も一時的な release を作成する。一般的な dry-run の意味と違い、誤操作を招く。

対応:

- 公開 workflow 入力との互換性を維持する間は、説明を `test-mode: performs writes on a temporary branch/release` 相当に修正する。
- C# 内部では `DryRun` bool を廃止し、`ExecutionMode.Preview`、`ExecutionMode.TestBranch`、`ExecutionMode.Apply` のような enum にする。
- 本当に副作用ゼロの `Preview` は `FileEdit[]`、`ProcessStep[]`、予定 output を返すだけにする。
- `TestBranch` が remote write を伴うことをログの先頭に明示する。

### P1: CLI と配布バイナリをテスト対象にする

通常の C# テストは project reference に対して実行され、workflow 内の開発時経路も `dotnet run` へ fallback する。一方、利用者は `actions/Linux-X64` / `Linux-ARM64` に commit されたバイナリを SHA 経由で実行する。[_update-actions-binaries.yaml](../../workflows/_update-actions-binaries.yaml) は release 後または手動でしか動かず、PR 時点で「ソース、CLI 契約、commit 済みバイナリ」が一致する保証がない。

**対応状況（2026-08-18）:** CLI black-box testと配布workflow testは対応済み。xUnitから別processとしてCLIを起動し、command一覧、`update-version` option、正常/異常exit code、`GITHUB_OUTPUT`を検査する。PR workflowはcommit済みx64バイナリをsmoke testしたうえで、現在のsourceを`RUNNER_TEMP`へLinux x64 / ARM64 publishする。x64は直接実行し、ARM64はELF machine typeを検査する。release用workflowも生成直後・commit前に同じx64実行とarchitecture検査を行う。追加後のローカルsuiteは77件すべて成功した。

PRでは`actions/`を生成・更新せず、publish成果物を一時領域だけに置く。commit済みバイナリの更新責務は従来どおりrelease/manualの`_update-actions-binaries.yaml`に限定し、このworkflow内でsmoke testに成功した生成物だけを`actions/`へcommitする。PR sourceと安定版commit済みbinaryの同一性は要求しない。

対応:

- PR で `dotnet publish` した Linux x64 バイナリを直接起動し、`--help` と副作用のない代表コマンドを smoke test する。
- PRのpublish先は`RUNNER_TEMP`とし、commit済みbinaryの更新を要求しない。`git diff -- ./actions`でPR testが配布物を変更していないことも確認する。
- commit 済み x64 バイナリ自体にも`--help`と代表コマンドのsmoke testを行う。ARM64はまずELF headerを検査し、native ARM64 runnerを採用できる場合にruntime smoke testを追加する。
- command 名、option 名、必須/任意、GitHub output 名を approval test する。これは workflow と C# の間の公開契約である。
- release用workflowではbinary生成後・commit前にCLIとarchitectureを検証し、commit対象を`actions/`だけに限定する。

### P1: CI 限定テストの偽陽性をなくす

[CreateReleaseCommandTest.cs](../../../src/CysharpActions.Tests/CreateReleaseCommandTest.cs) と [GitCommandTest.cs](../../../src/CysharpActions.Tests/GitCommandTest.cs) は、CI 以外では test body 冒頭で `return` する。今回のローカル実行でも、外部操作をしていないこれら 5 ケースが `Passed` と報告された。

**対応状況（2026-08-18）:** 対応済み。5ケースを`Category=LiveGitHub`へ分離し、xUnit v3の`SkipUnless`でGitHub Actions、`GH_REPO`、`GH_TOKEN`がない場合は理由付き`Skipped`にする。live classは一つのnon-parallel collectionに置き、同一workflowのlive jobもconcurrency groupで直列化する。ローカル集計は`Passed=72, Skipped=5`、unit filterは`Passed=72, Skipped=0`となる。

PR workflowはread-onlyのunit jobとwrite権限を持つ`live-github` jobに分離した。live jobはsame-repositoryかつnon-DependabotのPRだけで実行し、fork/Dependabotではjob自体がUI上でskipされる。TRXの`Counters.executed >= 5`かつ`UnitTestResult outcome=NotExecuted`が0件であることも検査し、filterミス、0件実行、一部または全件skipを成功にしない。管理下fixtureが無ければreturnしていたbenchmark compatibility testも明示的failureへ変更した。

対応:

- CI 必須のテストは別 test project または `Trait("Category", "LiveGitHub")` に分ける。
- 前提不足時は return ではなく xUnit の明示的 skip を使い、結果を `Skipped` と表示する。
- unit job は `LiveGitHub` を除外し、live job は同 category の件数が期待値以上であることも確認する。0 件実行を成功にしない。
- live test は parallel 実行を無効化し、run ID を含む一意な tag/branch を使い、cleanup job を `always()` で実行する。
- fork PR や Dependabot で secrets がない場合は明示的に skipped/neutral とし、main への merge queue または schedule で live suite を必須実行する。

### P1: 外部副作用の境界を一つにする

現在は各 command が ProcessX、`File.*`、Octokit、環境変数、`GitHubActions` static logger を直接呼ぶ。`ValidateTagCommand` だけが専用 interface を持つが、この方式を全 command に広げると過度な OOP になる。

**対応状況（2026-08-18）:** processとGitHub signed-commit APIの実行境界は対応済み。`Runtime/ProcessRunner.cs`へ`CommandSpec`、`ProcessResult`、`RunProcess`を置き、productionのProcessX呼び出しを一か所へ集約した。git、gh、dotnetの引数はshell文字列ではなくargument listとして組み立て、secret位置は`SecretArguments`でデータとして保持する。preview表示はこの情報から生成し、不正なsecret indexは表示前に失敗させる。

Octokit処理も`Runtime/GitHubCommitRunner.cs`へ移し、signed commitの入力treeとref更新結果をrecordで表した。commandごとのinterfaceやDI containerは追加せず、`RunProcess`と`RunGitHubCommit` delegateをconstructorから任意注入できる。CLIから外部実行まで`CancellationToken`を伝播し、ProcessXのnon-zero例外契約、argument list、secret redaction、runnerへ渡るコマンドデータを回帰テストする。最終suiteは`Passed=80, Skipped=5`。filesystem変換とenvironmentのimmutable data化は後続の各P1で扱う。

提案する最小境界:

```csharp
internal readonly record struct CommandSpec(
    string FileName,
    IReadOnlyList<string> Arguments,
    IReadOnlySet<int>? SecretArguments = null);

internal readonly record struct ProcessResult(int ExitCode, string Stdout, string Stderr);

internal delegate ValueTask<ProcessResult> RunProcess(
    CommandSpec command,
    CancellationToken cancellationToken);
```

- production では一つの ProcessX adapter を delegate に渡す。
- unit test では入力された `CommandSpec[]` を記録して結果データを返す。
- `IGitCommand`、`INuGetCommand`、`IReleaseService` のような interface は作らない。
- GitHub API が必要な signed commit も、入力/結果 record と一つの executor に限定する。
- すべての async CLI command から `CancellationToken` を伝播する。

これにより zx 構文の簡潔さは adapter 内に維持しつつ、command の判断部分を process 起動なしで読める。

### P1: 環境変数を明示的な入力データへ変える

旧`GitHubContext.cs`の`Current`は初回アクセス時のprocess environmentをstaticに固定していた。テストごとの差し替えが難しく、どのcommandが何を必要とするかも型から分からなかった。また`GitHubContextFilter`は`Current`を参照するだけで、空文字の必須値を検証していなかった。

**対応状況（2026-08-18）:** 対応済み。[ActionEnvironment.cs](../../../src/CysharpActions/Runtime/ActionEnvironment.cs)を追加し、CLIの`ActionsBatch`生成時に一度だけ`ReadFromProcess()`を呼ぶ。巨大なstatic snapshotは削除し、`RepositoryContext`、`WorkflowRunContext`、`GitHubCredentials`を必要なcommand methodへ値で渡す。tokenはcredentialsだけに保持し、既定の文字列表現でも必ずredactする。`GitHubActions`のverbose/output設定も、この入力データから明示的に構成する。

`Parse(IReadOnlyDictionary<string, string?>)`と使用時のvalidateを分離した。CI/debug値の不正形式、必須変数の空値、`GH_REPO`形式には変数名を含む`ActionCommandException`を返す。pure unit testはprocess environmentを書き換えず、dictionaryからのparse、parse後の入力dictionary変更からの独立性、既定値、secret非表示、commandが不足入力をprocess起動前に拒否することを検証する。通常suiteは`Passed=94, Skipped=0`、全suiteは`Passed=94, Skipped=5`。

対応:

- CLI 境界で一度だけ `ActionEnvironment.ReadFromProcess()` を呼び、immutable record を作る。
- command ごとに必要な小さい view (`RepositoryContext`、`WorkflowRunContext`) を値として渡す。
- parse と validate を分離し、bool/int の不正値には変数名を含む `ActionCommandException` を返す。
- token は通常の context record に混ぜず、必要な executor にだけ渡す。
- environment parsing は process environment を変更せず、`IReadOnlyDictionary<string,string?>` を入力にして unit test する。

### P1: command を「変換」と「適用」に分ける

特に version 更新は、ファイル名ごとの変換自体は純粋関数にできる。

```csharp
internal readonly record struct VersionFile(string Path, string Contents);
internal readonly record struct FileEdit(string Path, string Before, string After);

internal static FileEdit TransformVersion(VersionFile file, string version);
internal static void ApplyEdits(ReadOnlySpan<FileEdit> edits);
```

CLI command の流れを次の順序に固定する。

```text
parse input -> validate -> inspect/read -> plan/transform -> apply -> output
```

各段階の値を record/enum/array で表す。`Plan` が返る前には remote write をしない。これで実行内容の把握、preview、テストが同時に改善する。

### P2: ファイル構造と命名を機能中心に平坦化する

`Commands/Contexts/Utils` は技術分類なので、一つの処理を追うと三つの directory を往復する。`Utils` は依存関係も曖昧にする。以下程度の平坦さを推奨する。

```text
src/CysharpActions/
  Program.cs                       composition root のみ
  Cli/
    ActionsCli.cs                  command 定義、引数 -> request
    CliFilters.cs
  Runtime/
    ActionEnvironment.cs           process env -> immutable data
    GitHubOutput.cs
    ProcessRunner.cs               ProcessX adapter + secret redaction
  Operations/
    IncrementVersion.cs            request/result + pure logic
    UpdateVersion.cs               request/edit + orchestration
    ValidateTag.cs
    ValidateFiles.cs
    GitCommit.cs
    CreateRelease.cs
    PushNuGet.cs
    BenchmarkConfigMatrix.cs
    BenchmarkLoaderMatrix.cs
  Serialization/
    JsonContexts.cs
```

各 operation が 200～300 行を超える場合だけ、同名 subdirectory に `Models` / `Parser` / `Executor` を分ける。最初からレイヤー別 project や大量の directory は作らない。

併せて次を修正する。

- `FileExsistsCommand` -> `ValidateFiles`、`RegrexReplace` -> `RegexReplace`。
- 状態を変更しない小さな command class は static function にする。
- 公開 API でない型は `internal` にし、テストは `InternalsVisibleTo` を使う。
- tuple の `commited`、文字列の `isBranchCreated` を `GitCommitResult(bool Committed, string Sha, string? BranchName, bool BranchCreated)` にする。
- `CreateDummy` の不要な `async Task` を同期 command にする。
- `GlobFiles.Exists` で全例外を false に変換しない。無効 pattern、権限エラー、missing file を区別する。
- GitHub output は multiline delimiter 形式と改行/制御文字の検証に対応し、秘密値をログへ再出力しない。

### P2: tag/version の規則を一つにする

`VersioningCommand`、`ValidateTagCommand`、workflow に version の正規化・比較規則が分散している。`ValidateTagCommand` は input の先頭 `v` だけを外側で除去する一方、GitHub から取得した latest tag は同じ正規化を通らない。また独自 prerelease 比較は SemVer の識別子規則を完全には表していない。

対応:

- `VersionTag.Parse` / `Compare` / `Increment` を一か所に置く。
- 許容形式を README と test data で固定する。SemVer が契約なら SemVer parser を使用する。
- latest release と input の双方に同じ normalization を適用する。
- repository 名による MagicOnion の一時 bypass は policy data (`ValidationPolicy`) として CLI 境界から渡し、期限・理由・削除条件を文書化する。

### P2: branch 削除ポリシーを正しく表現する

`DeleteBranchAsync` は branch HEAD commit の author が `github-actions[bot]` かを見て「bot が作成した branch」と判定する。しかし HEAD author は branch 作成者を証明しない。

対応:

- 削除対象を `test-release/` prefix と run が出力した branch 名の allow-list で制限する。
- default branch、protected branch、prefix、期待 SHA を削除 request のデータとして検証する。
- author 判定は補助条件に留め、「作成者」とは呼ばない。

## 4. テスト戦略

### Layer 1: pure unit tests（常時、最速）

対象:

- version parse/increment/compare
- YAML -> matrix data
- file contents -> `FileEdit`
- glob pattern の分解
- environment dictionary -> context
- `CommandSpec` の組み立てと secret redaction

原則として filesystem、process、GitHub API、process environment を使わない。table-driven test を優先する。

### Layer 2: local contract tests（常時）

対象:

- temp directory 上の file edit、glob、GitHub output file
- temp の実 git repository 上の stage/commit/branch 操作
- fake executable/script に対する ProcessX の argument 境界、空白、quote、改行、non-zero exit、cancel
- CLI executable の exit code、stdout/stderr、output file

git は mock せず一時 repository で実行する。remote GitHub が不要なため高速で、`git add -A` のような回帰も検出できる。

### Layer 3: packaged binary tests（PR ごと）

- publish した Linux x64 binary の `--help`
- `increment-version`、matrix 変換、file validation の代表ケース
- temporary `GITHUB_OUTPUT` に対する出力値
- commit 済み配布 binary の起動確認
- x64/ARM64 asset の存在、実行権限、version manifest

### Layer 4: live GitHub integration（merge queue / schedule / manual）

- signed commit と fast-forward conflict
- test branch の create/update/delete
- draft release の create/upload/delete
- `gh` authentication error と permission error

live test は unit test assembly に混ぜない。外部状態を変更するため直列化し、resource 名を run ごとに一意化し、cleanup の成否も test result として残す。

### workflow tests

[_test-increment-version.yaml](../../workflows/_test-increment-version.yaml) は reusable workflow を `Cysharp/Actions/...@main` で呼ぶため、PR 上でも変更中の workflow/C# ではなく main を検証する。PR の回帰検知にはならない。

- repository 内の reusable workflow は可能な限り `./.github/workflows/...` を使う。
- main 自体を検証する canary は別名の schedule job として残す。
- workflow test の shell assertion は、共通の C# CLI または小さな test script に寄せ、失敗メッセージを統一する。
- required checks を unit / packaged / workflow / live に分け、どの層が未実行か UI 上で分かる名前にする。

### coverage の扱い

全体の数値を先に追うと、外部 process orchestration の行数に引っ張られる。最初は pure component を対象に branch coverage を可視化し、次を gate とする。

- pure transformation / parser: line 90%、branch 85% を目標
- Runtime adapter: 数値 gate ではなく contract case の完備を確認
- live integration: coverage 対象外、scenario 件数と最終成功時刻を記録

## 5. 段階的な実施順序

### Phase 0: 安全性修正（最初の PR）

1. NuGet API key のログ出力を redact する。
2. git stage を更新対象 path に限定する。
3. `dry-run` が remote write を行うことを workflow description とログへ明記する。
4. CI 条件の早期 return を明示的 skip に変える。

完了条件: secret 非出力テスト、temp git repo で unrelated file が commit されないテストが通る。

### Phase 1: 読みやすいデータ境界（1～2 PR）

1. request/result record と `ExecutionMode` を導入する。
2. environment を明示的 record にして CLI から渡す。
3. `CommandSpec` と一つの ProcessX adapter を導入する。
4. tuple/string bool を型付き result に置換する。

完了条件: operation の public/internal method signature から必要入力と副作用結果が分かり、テストが static environment を変更しない。

### Phase 2: pure transform と executor の分離（2～3 PR）

1. UpdateVersion を `TransformVersion` + `ApplyEdits` に分ける。
2. version 規則を一か所へ統合する。
3. GitCommit/CreateRelease/NuGet を plan + execute の順にする。
4. `Commands/Contexts/Utils` を `Cli/Runtime/Operations` へ段階的に移す。

完了条件: 各 operation が `parse -> validate -> inspect -> plan -> apply -> output` の順で読める。

### Phase 3: 実配布物を含むテスト（1～2 PR）

1. CLI black-box test を追加する。
2. PR publish binary smoke test を追加する。
3. release/manual workflowで生成直後・commit前のbinary smoke testを追加する。PRではbinaryを更新しない。
4. live GitHub tests を独立 job/project にする。
5. `_test-increment-version.yaml` の PR test を local reusable workflow 参照へ変える。

完了条件: unit、CLI、packaged binary、live integration の実行/skip 状態を別々に確認できる。

## 6. 採用しない方針

この規模では次は導入しない。

- command ごとの interface、repository/service class
- DI container と複雑な lifetime 管理
- Clean Architecture の project 分割
- visitor、abstract factory、継承ベースの command hierarchy
- 単純なデータ変換に対する fluent builder

必要なのは OOP の層ではなく、入力・計画・結果を表す小さい immutable data と、純粋関数、少数の副作用 executor である。

## 7. 最終的な判断

C# + ProcessX へ寄せた根本方針は維持すべきである。現状の難しさは C# 化そのものではなく、command class が判断と副作用を同時に抱え、static environment と実配布バイナリがテスト境界の外にあることから生じている。

最も効果が高い順は、(1) secret/stage/dry-run の安全性、(2) request/result/plan のデータ化、(3) process/environment 境界の一本化、(4) CLI と配布バイナリを含むテストである。この順なら大規模な再設計をせず、既存 workflow の公開契約を保ちながら段階的に改善できる。
