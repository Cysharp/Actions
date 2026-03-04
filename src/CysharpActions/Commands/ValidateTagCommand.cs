using Cysharp.Diagnostics;
using CysharpActions.Contexts;
using CysharpActions.Utils;
using System.Text.Json;

namespace CysharpActions.Commands;

/// <summary>
/// GitHubのリリース情報を取得するためのインターフェース。
/// </summary>
public interface IGitHubReleaseExe
{
    public Task<GitHubRelease[]> GetGitHubReleaseAsync();
}

/// <summary>
/// ghコマンドでGitHubのリリース情報を取得する実装。
/// </summary>
public class GitHubReleaseExeGh : IGitHubReleaseExe
{
    public async Task<GitHubRelease[]> GetGitHubReleaseAsync()
    {
        // release_latest=$(gh release list --exclude-drafts --exclude-pre-releases --json tagName,isLatest | jq -c -r ".[] | select(.isLatest == true) | .tagName")
        // sorted_latest=$(echo - e "${release_latest}\n${{ steps.trim.outputs.normalized_tag }}" | sort - V | tail - n 1)
        var releaseLatests = await "gh release list --exclude-drafts --exclude-pre-releases --json tagName,isLatest";
        var githubReleases = JsonSerializer.Deserialize(releaseLatests, JsonSourceGenerationContext.Default.GitHubReleaseArray);
        return githubReleases ?? Array.Empty<GitHubRelease>();
    }
}

public class ValidateTagCommand(IGitHubReleaseExe gitHubRelaeseExe)
{
    /// <summary>
    /// Normalize input tag. If the tag starts with 'v', it will be removed.
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public string Normalize(string tag) => tag.StartsWith('v') ? tag[1..] : tag;

    /// <summary>
    /// Validate input tag. If the input tag is older than the latest release tag, it will return false. Otherwise, it will return true.
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public async Task ValidateTagAsync(string tag)
    {
        Env.useShell = false;

        if (string.IsNullOrEmpty(tag))
            throw new ActionCommandException($"Tag is invalid, emptry string is not allowed.");

        // tmporary skip validation on MagicOnion. There are no implementation for validation on each Major Version.
        if (GitHubContext.Current.Repository == "Cysharp/MagicOnion")
        {
            GitHubActions.WriteLog("Temporary skip validation on MagicOnion.");
            return;
        }

        try
        {
            var githubReleases = await gitHubRelaeseExe.GetGitHubReleaseAsync();
            var releaseTag = githubReleases?.SingleOrDefault(x => x.IsLatest)?.TagName;

            // no release tag
            if (string.IsNullOrEmpty(releaseTag))
                return;
            // input tag is same or newer than latest tag
            if (releaseTag == tag)
                return;

            // 1.0.9 と 1.0.10のようにバージョンに変換できる場合、変換して適切に比較できるようにする。
            if (Version.TryParse(releaseTag, out var versionedReleaseTag) && Version.TryParse(tag, out var versionedTag))
            {
                // input tag is same or newer than latest tag
                if (versionedTag >= versionedReleaseTag)
                    return;
            }

            // バージョンに変換できない場合、セグメントごとに数値比較することで 1.0.9 と 1.0.10 を適切に比較する。
            if (CompareVersionString(tag, releaseTag) >= 0)
                return;

            // input tag is older than latest tag, reverting!!
            throw new ActionCommandException($"Tag is invalid, reverting to old version. Please bump the version.");
        }
        catch (ProcessErrorException ex)
        {
            throw new ActionCommandException($"Failed to get latest release tag. {ex.Message}", ex);
        }

        static int CompareVersionString(string a, string b)
        {
            var spanA = a.AsSpan();
            var spanB = b.AsSpan();
            while (true)
            {
                var dotA = spanA.IndexOf('.');
                var dotB = spanB.IndexOf('.');
                var segA = dotA < 0 ? spanA : spanA[..dotA];
                var segB = dotB < 0 ? spanB : spanB[..dotB];

                var cmp = CompareSegment(segA, segB);
                if (cmp != 0) return cmp;
                if (dotA < 0 && dotB < 0) return 0;
                spanA = dotA < 0 ? default : spanA[(dotA + 1)..];
                spanB = dotB < 0 ? default : spanB[(dotB + 1)..];
            }
        }

        static int CompareSegment(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
        {
            // セグメント内で '-' を探す (例: "10-beta1" → "10" と "beta1" に分割)
            var dashA = a.IndexOf('-');
            var dashB = b.IndexOf('-');

            var mainA = dashA < 0 ? a : a[..dashA];
            var mainB = dashB < 0 ? b : b[..dashB];

            // メイン部分を数値または文字列で比較
            var mainCmp = int.TryParse(mainA, out var numA) && int.TryParse(mainB, out var numB)
                ? numA.CompareTo(numB)
                : mainA.CompareTo(mainB, StringComparison.Ordinal);

            if (mainCmp != 0) return mainCmp;

            // メイン部分が同じ場合、プレリリース部分を比較
            // '-' がない場合は、ある方よりも大きい（リリース版 > プレリリース）
            if (dashA < 0 && dashB < 0) return 0;
            if (dashA < 0) return 1;  // a はリリース版、b はプレリリース
            if (dashB < 0) return -1; // a はプレリリース、b はリリース版

            var preA = a[(dashA + 1)..];
            var preB = b[(dashB + 1)..];

            return ComparePreRelease(preA, preB);
        }

        static int ComparePreRelease(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
        {
            // プレリリースラベル（alpha, beta, preview, rc など）を抽出して比較
            var (labelA, numA) = ExtractPreReleaseLabel(a);
            var (labelB, numB) = ExtractPreReleaseLabel(b);

            // ラベルの優先順位を比較
            var labelCmp = GetPreReleasePriority(labelA).CompareTo(GetPreReleasePriority(labelB));
            if (labelCmp != 0) return labelCmp;

            // ラベルが同じ場合、番号を比較
            return numA.CompareTo(numB);
        }

        static (string label, int number) ExtractPreReleaseLabel(ReadOnlySpan<char> preRelease)
        {
            // 末尾の連続した数値を抽出 (例: "beta1" → label="beta", number=1)
            var i = preRelease.Length - 1;
            while (i >= 0 && char.IsDigit(preRelease[i]))
            {
                i--;
            }

            var label = preRelease[..(i + 1)].ToString();
            var numberPart = preRelease[(i + 1)..];
            var number = numberPart.IsEmpty ? 0 : int.Parse(numberPart);

            return (label, number);
        }

        static int GetPreReleasePriority(string label)
        {
            // プレリリースラベルの優先順位 (alpha < beta < preview < rc < リリース版)
            return label.ToLowerInvariant() switch
            {
                "alpha" => 1,
                "beta" => 2,
                "preview" or "pre" => 3,
                "rc" => 4,
                "" => 5, // リリース版
                _ => 0, // 不明なラベルは最低優先度（文字列として扱う）
            };
        }
    }
}