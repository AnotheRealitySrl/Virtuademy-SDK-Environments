#if !VIRTUADEMY_APPLICATION
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using UnityEditor;
using UnityEditor.Search;

using UnityEngine;
using UnityEngine.UIElements;

namespace Virtuademy.SDK.Environments.Editor
{
    // The pane of one place (a world, or a tenant's tenant level): its banners, its thumbnail, its localization file and, for a world, its experiences.
    internal sealed partial class EnvironmentDetailsWindow
    {
        private const string unsupported_banner = "This tenant lets the SDK read thumbnails and localization files, not change them yet. Change them in its Back Office.";
        private const string unsupported_tooltip = "This tenant cannot receive files from the SDK yet: change them in its Back Office.";
        private const string not_allowed_banner = "You can see this place but not change it: only the world's Owner, or the person who published the environment here, can.";
        private const string not_allowed_tooltip = "Only the world's Owner, or the person who published the environment here, can change it.";
        private const string not_allowed_tenant_banner = "You can see this place but not change it: only a TenantManager can change the tenant-level environment.";
        private const string not_allowed_tenant_tooltip = "Only a TenantManager can change the tenant-level environment.";
        private const string tenant_banner = "The tenant-level environment has one thumbnail and one localization file for all the worlds that use it.";
        private const string no_capacity_message = "This world has no session capacity set, so a Core experience cannot be created here. Ask its Owner to set it in the Back Office.";
        private const string experiences_not_offered = "This tenant does not show experiences to the SDK yet. You can see and change them in its Back Office.";
        private const string no_longer_published = "This scene is no longer published in this world. Refresh to update the list.";
        private const string log_in_message = "Log in to see where this scene is published here.";

        // The replace in one place that is picked and waiting for its confirmation, or running, or finished.
        private PlaceReplaceFlow placeFlow;

        // Files being saved to the project (by place and kind) and the reason one could not be.
        private readonly HashSet<string> saving = new();
        private readonly Dictionary<string, string> saveProblems = new();

        // One cancellation for what the user starts here (saves, replaces, creating Core experiences): a new scene, Refresh and closing the window raise it.
        private CancellationTokenSource flowCancellation;

        private CancellationToken FlowToken => (flowCancellation ??= new CancellationTokenSource()).Token;

        private static string SaveKey(DetailsConnection connection, FilesTarget target, FileKind kind) =>
            $"{connection.Key}|{(target.WorldId?.ToString(CultureInfo.InvariantCulture) ?? "tenant")}|{kind}";

        private static string KindName(FileKind kind) => kind == FileKind.Thumbnail ? "thumbnail" : "localization file";

        #region The pane

        private void BuildPane(VisualElement pane)
        {
            pane.AddToClassList("vd-pane");

            if (allSelected)
            {
                BuildAllPlacesPane(pane);
                return;
            }

            if (TryGetSelected(out DetailsConnection connection, out FilesTarget target))
            {
                BuildPlacePane(pane, connection, target);
                return;
            }

            if (connections.Any(c => c.Load == ConnectionLoad.Loading))
            {
                BuildPaneSkeleton(pane);
                return;
            }

            pane.Add(Banner(BannerKind.Info, "Select a place in the list. Log in to a connection to see where this scene is published there."));
        }

        private void BuildPaneSkeleton(VisualElement pane)
        {
            VisualElement skeleton = new();
            skeleton.style.paddingLeft = 12;
            skeleton.style.paddingRight = 12;
            skeleton.style.paddingTop = 12;
            skeleton.Add(Skeleton(40, 14));
            VisualElement row = Row();
            row.style.alignItems = Align.FlexStart;
            row.style.marginTop = 12;
            VisualElement picture = Skeleton(0, 108);
            picture.style.width = 192;
            picture.style.flexShrink = 0;
            row.Add(picture);
            VisualElement lines = new() { style = { flexGrow = 1, marginLeft = 10 } };
            lines.Add(Skeleton(60));
            lines.Add(Skeleton(40));
            row.Add(lines);
            skeleton.Add(row);
            Label reading = Text($"Reading the places where {scene?.Name} is published…", "muted");
            reading.style.marginTop = 12;
            skeleton.Add(reading);
            pane.Add(skeleton);
        }

        private void BuildPlacePane(VisualElement pane, DetailsConnection connection, FilesTarget target)
        {
            pane.Add(BuildPlaceTitle(connection, target));

            string refusal = EnvironmentFilesService.RefusalFor(target);
            if (connection.Support == WriteSupport.NotSupported)
                pane.Add(Banner(BannerKind.Info, unsupported_banner));
            if (refusal != null)
                pane.Add(WithTooltip(Banner(BannerKind.Warning, target.IsTenant ? not_allowed_tenant_banner : not_allowed_banner), refusal));
            if (target.IsTenant)
                pane.Add(Banner(BannerKind.Info, tenant_banner));

            pane.Add(BuildFileSection(connection, target, FileKind.Thumbnail));
            pane.Add(BuildFileSection(connection, target, FileKind.Localization));
            pane.Add(BuildExperiencesSection(connection, target));
        }

        private static VisualElement WithTooltip(VisualElement element, string tooltip)
        {
            element.tooltip = tooltip ?? string.Empty;
            return element;
        }

        private VisualElement BuildPlaceTitle(DetailsConnection connection, FilesTarget target)
        {
            VisualElement title = new();
            title.AddToClassList("vd-pane-title");
            title.Add(Text(target.IsTenant ? "Tenant level" : target.WorldLabel, "vd-pane-title-name"));
            if (!target.IsTenant)
                title.Add(Text($"#{target.WorldId.Value.ToString(CultureInfo.InvariantCulture)}", "muted"));
            title.Add(Text($"· {ConnectionName(connection)}", "muted", "vd-pane-title-where"));
            if (connection.HasBackoffice)
                title.Add(BackofficeButton(connection, target.WorldId));
            return title;
        }

        private static Button BackofficeButton(DetailsConnection connection, int? worldId) =>
            Btn("Back Office ↗", () =>
            {
                string link = connection.BackofficeLinkFor(worldId);
                if (link != null)
                    Application.OpenURL(link);
            }, "Opens this tenant's Back Office in the browser" + (worldId != null ? ", on this world." : "."));

        #endregion

        #region Thumbnail and localization file

        private VisualElement BuildFileSection(DetailsConnection connection, FilesTarget target, FileKind kind)
        {
            FileStatus status = target.StatusOf(kind);
            string note = !target.IsTenant ? null
                : kind == FileKind.Thumbnail ? "shared by every world using the tenant-level environment" : "shared, as above";
            VisualElement section = Section(kind == FileKind.Thumbnail ? "Thumbnail" : "Localization file", note, out VisualElement bodyElement);

            if (kind == FileKind.Thumbnail)
                BuildThumbnailBody(bodyElement, connection, target, status);
            else
                BuildLocalizationBody(bodyElement, connection, target, status);

            if (placeFlow != null && placeFlow.Kind == kind && placeFlow.Place.Equals(new PlaceKey(connection.Key, target.WorldId)))
                bodyElement.Add(BuildReplaceStrip(connection, target, placeFlow));

            return section;
        }

        private void BuildThumbnailBody(VisualElement bodyElement, DetailsConnection connection, FilesTarget target, FileStatus status)
        {
            RequestCache(connection, status);
            Texture2D texture = status.Present ? PreviewOf(status.CachePath) : null;

            VisualElement row = new();
            row.AddToClassList("vd-thumb-row");
            if (status.Present && texture == null && status.Error == null)
            {
                VisualElement waiting = Skeleton(0, 108);
                waiting.style.width = 192;
                waiting.style.flexShrink = 0;
                row.Add(waiting);
            }
            else
            {
                row.Add(Thumb(texture, 192, 108));
            }

            VisualElement side = new();
            side.AddToClassList("vd-thumb-side");
            if (!status.Present)
            {
                side.Add(Text("No thumbnail. The Back Office and the app show a blank card.", "vd-small", "vd-warn"));
            }
            else
            {
                string info = ImageInfo(status, texture);
                if (info != null)
                    side.Add(Text(info, "muted"));
                if (status.Error != null && status.Sha256 != null)
                {
                    Label preview = Text("The preview could not be loaded.", "vd-small", "vd-err");
                    preview.tooltip = status.Error;
                    side.Add(preview);
                }

                side.Add(BuildProjectLine(connection, target, FileKind.Thumbnail, status));
            }

            side.Add(BuildReplaceButton(connection, target, FileKind.Thumbnail));
            row.Add(side);
            bodyElement.Add(row);
        }

        /// <summary><c>1920 × 1080 · JPEG</c>, from what the SDK read of the file; null when it did not.</summary>
        private static string ImageInfo(FileStatus status, Texture2D texture)
        {
            int width = status.Width > 0 ? status.Width : texture != null ? texture.width : 0;
            int height = status.Height > 0 ? status.Height : texture != null ? texture.height : 0;
            string format = status.Format != null ? status.Format.ToUpperInvariant()
                : status.CachePath != null ? (Path.GetExtension(status.CachePath).Equals(".png", StringComparison.OrdinalIgnoreCase) ? "PNG" : "JPEG") : null;
            if (width == 0 || height == 0)
                return format;

            return format == null ? $"{width} × {height}" : $"{width} × {height} · {format}";
        }

        private void BuildLocalizationBody(VisualElement bodyElement, DetailsConnection connection, FilesTarget target, FileStatus status)
        {
            RequestCache(connection, status);

            if (!status.Present)
            {
                Label none = Text("No localization file. Texts that are keys show as the key.", "vd-small", "vd-warn");
                none.style.marginBottom = 4;
                bodyElement.Add(none);
            }
            else
            {
                LocalizationCsvCheck content = ContentOf(status.CachePath);
                VisualElement kv = new();
                kv.AddToClassList("vd-kv");
                kv.Add(Text("Content", "muted", "vd-kv-key"));
                kv.Add(Text(content != null ? ContentText(content)
                    : status.Error != null ? "not available" : "reading the file…", content != null ? "vd-small" : "muted"));
                bodyElement.Add(kv);
                if (status.Error != null && status.Sha256 != null)
                {
                    Label preview = Text("The content could not be loaded.", "vd-small", "vd-err");
                    preview.tooltip = status.Error;
                    bodyElement.Add(preview);
                }

                bodyElement.Add(BuildProjectLine(connection, target, FileKind.Localization, status));
            }

            VisualElement actions = BuildReplaceButton(connection, target, FileKind.Localization);
            actions.style.marginTop = 5;
            bodyElement.Add(actions);
        }

        private static string ContentText(LocalizationCsvCheck content) =>
            $"{content.TermCount} keys · languages {(content.Languages.Count == 0 ? "none" : string.Join(", ", content.Languages))}";

        /// <summary>Where the file stands against the project: the same file is there (with Show), it is not, or the tenant did not say which file it holds.</summary>
        private VisualElement BuildProjectLine(DetailsConnection connection, FilesTarget target, FileKind kind, FileStatus status)
        {
            VisualElement line = new();
            line.AddToClassList("vd-proj-line");
            string key = SaveKey(connection, target, kind);

            if (saving.Contains(key))
            {
                line.Add(Spinner());
                line.Add(Text("Saving to the project…", "muted"));
                return line;
            }

            if (status.Sha256 == null)
            {
                // The platform did not say the hash and the SDK could not download the file to hash it: nothing is known about it.
                Label unreadable = Text("This file could not be read.", "vd-small", "vd-err");
                unreadable.tooltip = status.Error ?? "The platform did not say which file this place holds, and the SDK could not download it.";
                line.Add(unreadable);
                line.Add(Btn("Retry", () => _ = LoadConnectionAsync(connection), "Reads this connection again."));
            }
            else if (status.ProjectAsset != null)
            {
                line.Add(Text("✓ Same file in the project:", "vd-small", "vd-ok"));
                Label asset = Text(ProjectName(status.ProjectAsset), "muted");
                asset.tooltip = status.ProjectAsset;
                line.Add(asset);
                line.Add(Btn("Show", () => ShowAsset(status.ProjectAsset), "Selects the file in the Project window."));
            }
            else
            {
                line.Add(Text("Not in the project.", "vd-small", "vd-warn"));
                line.Add(SaveButton(connection, target, kind));
            }

            if (saveProblems.TryGetValue(key, out string problem))
            {
                Label error = Text($"× {problem}", "vd-small", "vd-err");
                error.style.width = Length.Percent(100);
                error.style.whiteSpace = WhiteSpace.Normal;
                line.Add(error);
            }

            return line;
        }

        private Button SaveButton(DetailsConnection connection, FilesTarget target, FileKind kind) =>
            Btn("Save to project", () => SaveFileToProject(connection, target, kind),
                $"Keeps the {KindName(kind)} this place holds in {EnvironmentFilesService.PublishFolder}, unless the project already holds the same file.");

        private static void ShowAsset(string assetPath)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
                return;

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private async void SaveFileToProject(DetailsConnection connection, FilesTarget target, FileKind kind)
        {
            string key = SaveKey(connection, target, kind);
            if (!saving.Add(key))
                return;

            saveProblems.Remove(key);
            Render();

            SavedFile saved;
            try
            {
                saved = await EnvironmentFilesService.SaveOnlineFileAsync(target, kind, null, false, FlowToken);
            }
            catch (OperationCanceledException)
            {
                saving.Remove(key);
                return;
            }
            catch (Exception ex)
            {
                saved = new SavedFile { Target = target, Kind = kind, Status = SaveStatus.Failed, Error = ex.Message };
            }

            saving.Remove(key);
            if (saved.Status == SaveStatus.Failed)
                saveProblems[key] = Sentence(saved.Error) + ".";
            Render();
        }

        #endregion

        #region Replace in one place

        private VisualElement BuildReplaceButton(DetailsConnection connection, FilesTarget target, FileKind kind)
        {
            string label = target.IsTenant ? "Replace…" : "Replace in this world only…";
            string why = connection.Support == WriteSupport.NotSupported ? unsupported_tooltip
                : EnvironmentFilesService.RefusalFor(target) != null ? (target.IsTenant ? not_allowed_tenant_tooltip : not_allowed_tooltip)
                : null;
            bool busy = placeFlow != null && placeFlow.Busy;
            Button button = Btn(label, () => PickFor(connection, target, kind), why ?? (kind == FileKind.Thumbnail
                ? "Pick an image asset of the project (PNG or JPEG)."
                : "Pick a .csv asset of the project."), enabled: why == null && !busy);
            return Row(button);
        }

        /// <summary>Unity's object picker, on the project's images or its .csv files; what is picked starts a replace in this place only.</summary>
        private void PickFor(DetailsConnection connection, FilesTarget target, FileKind kind)
        {
            Action<UnityEngine.Object, bool> selected = (picked, canceled) =>
            {
                if (canceled || picked == null)
                    return;

                EditorApplication.delayCall += () => StartPlaceReplace(connection, target, kind, picked);
            };

            if (kind == FileKind.Thumbnail)
                SearchService.ShowObjectPicker(selected, _ => { }, "a:assets", nameof(Texture2D), typeof(Texture2D));
            else
                SearchService.ShowObjectPicker(selected, _ => { }, "a:assets ext:csv", nameof(TextAsset), typeof(TextAsset));
        }

        private async void StartPlaceReplace(DetailsConnection connection, FilesTarget target, FileKind kind, UnityEngine.Object picked)
        {
            if (!IsCurrent(connection))
                return;

            ReleaseNewPreviews();
            PlaceReplaceFlow flow = new() { Place = new PlaceKey(connection.Key, target.WorldId), Kind = kind };
            UnityEngine.Object accepted = SceneDefaults.Accept(kind, new[] { picked }, out string refusal);
            if (accepted == null)
            {
                flow.Problem = refusal;
                placeFlow = flow;
                Render();
                return;
            }

            flow.Asset = AssetDatabase.GetAssetPath(accepted);
            flow.Checking = true;
            placeFlow = flow;
            Render();

            try
            {
                // A moment for "Checking..." to be drawn: the checks below run on the main thread (a large image is decoded).
                await Task.Delay(40);
                if (placeFlow != flow)
                    return;

                // The tenant's languages first: a localization file is checked against them. Everything else is local.
                List<string> languages = kind == FileKind.Localization ? await LanguagesOfAsync(connection) : null;
                if (placeFlow != flow)
                    return;

                flow.Prepared = kind == FileKind.Thumbnail
                    ? EnvironmentFilesService.PrepareThumbnail(flow.Asset, write: false)
                    : EnvironmentFilesService.PrepareLocalization(flow.Asset, languages);
            }
            catch (Exception ex)
            {
                flow.Problem = $"The file could not be checked: {Sentence(ex.Message)}.";
            }

            flow.Checking = false;
            if (placeFlow == flow)
                Render();
        }

        /// <summary>The strip inside the section: what is about to be replaced and with what, with Cancel and Replace; or the problems of the file; or the outcome.</summary>
        private VisualElement BuildReplaceStrip(DetailsConnection connection, FilesTarget target, PlaceReplaceFlow flow)
        {
            VisualElement holder = new();
            holder.style.marginTop = 8;
            string fileName = flow.Asset == null ? string.Empty : Path.GetFileName(flow.Asset);

            if (flow.Problem != null)
            {
                holder.Add(Banner(BannerKind.Error, flow.Problem, Btn("Cancel", () => { placeFlow = null; Render(); })));
                return holder;
            }

            if (flow.Checking || flow.Prepared == null)
            {
                VisualElement checking = Row(Spinner(), Text($"Checking {fileName}…", "muted"));
                holder.Add(checking);
                return holder;
            }

            if (flow.Outcome != null)
            {
                BuildOutcome(holder, connection, target, flow);
                return holder;
            }

            if (!flow.Prepared.Check.Valid)
            {
                holder.Add(BuildBadFile(fileName, flow.Asset, flow.Prepared.Check, () => { placeFlow = null; Render(); }));
                return holder;
            }

            VisualElement strip = new();
            strip.AddToClassList("vd-strip");

            VisualElement text = new();
            text.AddToClassList("vd-strip-text");
            string what = flow.Kind == FileKind.Thumbnail ? "thumbnail" : "localization file";
            string where = target.IsTenant ? "the tenant level" : $"{target.WorldLabel} #{target.WorldId.Value.ToString(CultureInfo.InvariantCulture)} only";
            text.Add(Rich(($"Replace the {what} of ", false), (where, true), (", with ", false), (fileName, true)));
            string conversion = ConversionNote(flow.Prepared);
            if (conversion != null)
                text.Add(Text(conversion, "muted", "vd-wrap"));
            foreach (FileIssue warning in flow.Prepared.Check.Warnings)
                text.Add(Text(warning.ToString(), "vd-small", "vd-warn"));
            strip.Add(text);

            FileStatus now = target.StatusOf(flow.Kind);
            if (flow.Kind == FileKind.Thumbnail)
            {
                strip.Add(Thumb(now.Present ? PreviewOf(now.CachePath) : null, 64, 36));
                strip.Add(Text("→", "vd-strip-arrow"));
                strip.Add(Thumb(PreviewOfBytes("new|" + flow.Prepared.Sha256, flow.Prepared.Bytes), 64, 36));
            }
            else
            {
                LocalizationCsvCheck before = now.Present ? ContentOf(now.CachePath) : null;
                Label difference = Text(Difference(before, (LocalizationCsvCheck)flow.Prepared.Check, now.Present), "vd-small");
                strip.Add(difference);
            }

            if (flow.Busy)
            {
                strip.Add(Spinner());
            }
            else
            {
                strip.Add(Btn("Cancel", () => { placeFlow = null; Render(); }));
                strip.Add(Btn("Replace", () => ApplyPlaceReplace(connection, target, flow), "Sends the file to this place only. The file the place holds now is saved to the project first, unless it is already there.", primary: true));
            }

            holder.Add(strip);
            return holder;
        }

        /// <summary>"The PNG will be converted to PublishFiles/hero.jpg (2400 × 1350): that file goes online." for an image that is not sent as it is; null otherwise.</summary>
        private static string ConversionNote(PreparedFile file)
        {
            if (file == null || !file.Converted)
                return null;

            string format = file.Check is ThumbnailCheck check && check.Format != null ? check.Format.ToUpperInvariant() : "image";
            return $"The {format} will be converted to {ProjectName(file.ConvertedAsset)} ({file.Width} × {file.Height}): that file goes online.";
        }

        /// <summary><c>86 → 92 keys (+6) · + de</c>: what a new localization file changes against the one the place holds.</summary>
        private static string Difference(LocalizationCsvCheck before, LocalizationCsvCheck after, bool placeHasFile)
        {
            if (after == null)
                return string.Empty;

            if (before == null)
                return placeHasFile ? $"{after.TermCount} keys" : $"none → {after.TermCount} keys";

            string keys = before.TermCount == after.TermCount
                ? $"{after.TermCount} keys"
                : $"{before.TermCount} → {after.TermCount} keys ({(after.TermCount > before.TermCount ? "+" : "−")}{Math.Abs(after.TermCount - before.TermCount)})";

            List<string> added = after.Languages.Where(l => !before.Languages.Contains(l, StringComparer.OrdinalIgnoreCase)).ToList();
            List<string> removed = before.Languages.Where(l => !after.Languages.Contains(l, StringComparer.OrdinalIgnoreCase)).ToList();
            if (added.Count > 0)
                keys += $" · + {string.Join(", ", added)}";
            if (removed.Count > 0)
                keys += $" · − {string.Join(", ", removed)}";
            return keys;
        }

        /// <summary>A bad file: "&lt;file&gt; has &lt;n&gt; problems, nothing was sent:", one line for each with its row, and Open file.</summary>
        private VisualElement BuildBadFile(string fileName, string assetPath, FileCheckResult check, System.Action cancel)
        {
            VisualElement holder = new();
            int count = check.Errors.Count;
            string header = $"{fileName} has {count}{(check.ErrorsTruncated ? "+" : string.Empty)} problem{(count == 1 && !check.ErrorsTruncated ? string.Empty : "s")}, nothing was sent:";
            holder.Add(Banner(BannerKind.Error, header,
                Btn("Open file", () => OpenAsset(assetPath), "Opens the file in its editor."),
                cancel == null ? null : Btn("Cancel", cancel)));

            VisualElement problems = new();
            problems.AddToClassList("vd-problems");
            foreach (FileIssue issue in check.Errors)
                problems.Add(Text("· " + issue, "vd-small"));
            if (check.ErrorsTruncated)
                problems.Add(Text("· and more: fix these first.", "vd-small", "muted"));
            holder.Add(problems);
            return holder;
        }

        private static void OpenAsset(string assetPath)
        {
            UnityEngine.Object asset = string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
                return;

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            AssetDatabase.OpenAsset(asset);
        }

        private async void ApplyPlaceReplace(DetailsConnection connection, FilesTarget target, PlaceReplaceFlow flow)
        {
            if (flow.Busy || flow.Prepared == null)
                return;

            flow.Busy = true;
            flow.Outcome = null;
            Render();

            FileOutcome outcome;
            try
            {
                outcome = await EnvironmentFilesService.ReplaceOneAsync(target, flow.Prepared, false, FlowToken);
            }
            catch (OperationCanceledException)
            {
                flow.Busy = false;
                return;
            }
            catch (Exception ex)
            {
                outcome = new FileOutcome { Target = target, Kind = flow.Kind, Result = OutcomeKind.Failed, Message = ex.Message };
            }

            flow.Busy = false;
            flow.Outcome = outcome;
            Render();
        }

        /// <summary>What a replace in one place ended with: Updated, or why it did not: refused, unsupported, or an error with Retry.</summary>
        private void BuildOutcome(VisualElement holder, DetailsConnection connection, FilesTarget target, PlaceReplaceFlow flow)
        {
            FileOutcome outcome = flow.Outcome;
            System.Action close = () => { placeFlow = null; Render(); };

            switch (outcome.Result)
            {
                case OutcomeKind.Updated:
                case OutcomeKind.Unchanged:
                    VisualElement done = Row();
                    done.Add(Text(outcome.Result == OutcomeKind.Updated ? "✓ Updated" : "✓ Unchanged: same file", "vd-small", "vd-ok"));
                    if (outcome.SavedPrevious != null)
                    {
                        Label saved = Text($"The previous file was saved to {ProjectName(outcome.SavedPrevious)}.", "muted");
                        saved.style.marginLeft = 8;
                        done.Add(saved);
                    }

                    done.Add(new VisualElement { style = { flexGrow = 1 } });
                    done.Add(Btn("Close", close));
                    holder.Add(done);
                    if (outcome.Warning != null)
                        holder.Add(Text(outcome.Warning, "vd-small", "vd-warn"));
                    break;

                case OutcomeKind.Refused:
                    holder.Add(WithTooltip(Banner(BannerKind.Warning, target.IsTenant ? not_allowed_tenant_banner : not_allowed_banner, Btn("Close", close)), outcome.Message));
                    break;

                case OutcomeKind.Unsupported:
                    holder.Add(WithTooltip(Banner(BannerKind.Info, unsupported_banner, Btn("Close", close)), outcome.Message));
                    break;

                default:
                    holder.Add(WithTooltip(Banner(BannerKind.Error, $"The {KindName(flow.Kind)} was not replaced: {Sentence(outcome.Message)}.",
                        Btn("Retry", () => ApplyPlaceReplace(connection, target, flow), "Sends the file again."), Btn("Cancel", close)), outcome.Message));
                    break;
            }
        }

        #endregion

        #region Experiences

        private VisualElement BuildExperiencesSection(DetailsConnection connection, FilesTarget target)
        {
            if (target.IsTenant)
            {
                VisualElement tenantSection = Section("Experiences", null, out VisualElement tenantBody);
                tenantBody.Add(Text("Experiences belong to a world: select one of the worlds in the list.", "muted", "vd-wrap"));
                return tenantSection;
            }

            int worldId = target.WorldId.Value;
            connection.Experiences.TryGetValue(worldId, out WorldExperiences experiences);

            string total = experiences != null && experiences.Known ? experiences.Data.Counts.Total.ToString(CultureInfo.InvariantCulture) : null;
            VisualElement section = Section("Experiences", total, out VisualElement bodyElement);

            if (experiences == null || experiences.Loading)
            {
                bodyElement.Add(Row(Spinner(), Text("Reading the experiences…", "muted")));
                return section;
            }

            switch (experiences.Status)
            {
                case EnvironmentExperiencesStatus.Ok:
                    break;

                case EnvironmentExperiencesStatus.NotAllowed:
                    bodyElement.Add(WithTooltip(Banner(BannerKind.Warning,
                        "You cannot see the experiences of this world. It needs the Owner role in the world, or the \"Manage my core experiences\" feature."), experiences.Message));
                    return section;

                case EnvironmentExperiencesStatus.NotSupportedByTenant:
                    VisualElement[] actions = connection.HasBackoffice ? new VisualElement[] { BackofficeButton(connection, worldId) } : Array.Empty<VisualElement>();
                    bodyElement.Add(WithTooltip(Banner(BannerKind.Info,
                        experiences_not_offered, actions), experiences.Message));
                    return section;

                case EnvironmentExperiencesStatus.LoginRequired:
                    bodyElement.Add(WithTooltip(Banner(BannerKind.Info, log_in_message, Btn("Log in", () => LoginConnection(connection))), experiences.Message));
                    return section;

                case EnvironmentExperiencesStatus.NotPublishedInWorld:
                    bodyElement.Add(WithTooltip(Banner(BannerKind.Warning, no_longer_published,
                        Btn("Refresh", Refresh, "Reads every connection again.")), experiences.Message));
                    return section;

                default:
                    bodyElement.Add(WithTooltip(Banner(BannerKind.Error, "The experiences of this world could not be read.",
                        Btn("Retry", () => _ = ReloadExperiencesAsync(connection, worldId), "Reads them again.")), experiences.Message));
                    return section;
            }

            EnvironmentExperiences data = experiences.Data;
            ExperienceCounts counts = data.Counts;

            VisualElement summary = new();
            summary.AddToClassList("vd-counts");
            summary.Add(Rich(("Core ", false), (counts.CoreTotal.ToString(CultureInfo.InvariantCulture), true),
                (counts.CoreTotal > 0 ? $" ({counts.CorePublished} published)" : string.Empty, false),
                (" · Authored ", false), ((counts.AuthoredPublished + counts.AuthoredDraft).ToString(CultureInfo.InvariantCulture), true),
                (" · External app ", false), (counts.ExternalApp.ToString(CultureInfo.InvariantCulture), true)));
            bodyElement.Add(summary);

            if (data.HasNoCore)
            {
                List<VisualElement> create = new();
                if (experiences.Creating)
                    create.Add(Spinner());
                else if (data.CanCreateCore && experiences.CreateStatus != EnvironmentExperiencesStatus.WorldHasNoCapacity)
                    create.Add(Btn("Create Core experience", () => CreateCore(connection, target), "Creates the Core experience of this environment in this world, as a draft.", primary: true));

                bodyElement.Add(Banner(BannerKind.Warning, "No Core experience uses this environment here, so nobody can start it from the catalog.", create.ToArray()));
                if (experiences.CreateStatus != null)
                    bodyElement.Add(CreateProblemBanner(connection, worldId, experiences));
            }
            else
            {
                foreach (CoreExperienceItem item in data.CoreExperiences)
                    bodyElement.Add(BuildExperienceRow(connection, worldId, experiences, item));

                if (data.CoreHiddenDrafts > 0)
                {
                    Label hidden = Text($"+ {data.CoreHiddenDrafts} draft{(data.CoreHiddenDrafts == 1 ? string.Empty : "s")} by other authors, not visible to you.", "muted", "vd-wrap");
                    hidden.style.marginTop = 4;
                    bodyElement.Add(hidden);
                }
            }

            Label footer = Text("Authored and external-app experiences are counted, not listed: open the Back Office for them.", "muted", "vd-wrap");
            footer.style.marginTop = 4;
            bodyElement.Add(footer);
            return section;
        }

        /// <summary>Why the Core experience was not created, in the window's own words; the service's message is the tooltip.</summary>
        private VisualElement CreateProblemBanner(DetailsConnection connection, int worldId, WorldExperiences experiences)
        {
            switch (experiences.CreateStatus)
            {
                case EnvironmentExperiencesStatus.WorldHasNoCapacity:
                    return Banner(BannerKind.Warning, no_capacity_message);
                case EnvironmentExperiencesStatus.NotAllowed:
                    return WithTooltip(Banner(BannerKind.Warning, "You cannot create a Core experience in this world."), experiences.CreateMessage);
                case EnvironmentExperiencesStatus.LoginRequired:
                    return WithTooltip(Banner(BannerKind.Info, log_in_message, Btn("Log in", () => LoginConnection(connection))), experiences.CreateMessage);
                case EnvironmentExperiencesStatus.NotSupportedByTenant:
                    VisualElement[] actions = connection.HasBackoffice ? new VisualElement[] { BackofficeButton(connection, worldId) } : Array.Empty<VisualElement>();
                    return WithTooltip(Banner(BannerKind.Info, experiences_not_offered, actions), experiences.CreateMessage);
                case EnvironmentExperiencesStatus.NotPublishedInWorld:
                    return WithTooltip(Banner(BannerKind.Warning, no_longer_published, Btn("Refresh", Refresh, "Reads every connection again.")), experiences.CreateMessage);
                default:
                    return WithTooltip(Banner(BannerKind.Error, "The Core experience was not created."), experiences.CreateMessage);
            }
        }

        /// <summary>The window's own text for a failed Core creation, for the lines of Create all: the service's message is never in it.</summary>
        private static string CreateProblemText(EnvironmentExperiencesStatus? status) => status switch
        {
            EnvironmentExperiencesStatus.WorldHasNoCapacity => no_capacity_message,
            EnvironmentExperiencesStatus.NotAllowed => "You cannot create a Core experience in this world.",
            EnvironmentExperiencesStatus.LoginRequired => log_in_message,
            EnvironmentExperiencesStatus.NotSupportedByTenant => experiences_not_offered,
            EnvironmentExperiencesStatus.NotPublishedInWorld => no_longer_published,
            _ => "The Core experience was not created.",
        };

        private VisualElement BuildExperienceRow(DetailsConnection connection, int worldId, WorldExperiences experiences, CoreExperienceItem item)
        {
            VisualElement row = new();
            row.AddToClassList("vd-exp-row");

            Label label = Text(item.Label, "vd-grow");
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.tooltip = item.Label;
            row.Add(label);

            Label you = Text(item.IsMine ? "you" : string.Empty, "muted", "vd-exp-you");
            row.Add(you);

            bool published = string.Equals(item.Status, nameof(ExperiencePublicationStatus.Published), StringComparison.OrdinalIgnoreCase);
            DropdownField state = new(new List<string> { "Published", "Draft" }, published ? 0 : 1)
            {
                tooltip = item.CanChangeStatus ? "The experience's state: Published shows it in the catalog." : "Only the experience's author or the world's Owner can change its state.",
            };
            bool changing = experiences.Changing.Contains(item.Id) || changingExperiences.Contains(ExperienceKey(connection, worldId, item.Id));
            state.SetEnabled(item.CanChangeStatus && !changing);
            state.RegisterValueChangedCallback(evt =>
            {
                ExperiencePublicationStatus next = evt.newValue == "Published" ? ExperiencePublicationStatus.Published : ExperiencePublicationStatus.Draft;
                SetExperienceState(connection, worldId, item, next);
            });
            row.Add(state);

            if (changing)
            {
                row.Add(Spinner());
            }
            else if (experiences.StateErrors.TryGetValue(item.Id, out string problem))
            {
                Label error = Text("×", "vd-exp-err");
                error.tooltip = problem;
                row.Add(error);
            }

            return row;
        }

        // Experience state changes in flight, by connection, world and experience: a reload of a world replaces its WorldExperiences, and the popup must stay off meanwhile.
        private readonly HashSet<string> changingExperiences = new();

        private static string ExperienceKey(DetailsConnection connection, int worldId, int experienceId) =>
            $"{connection.Key}|{worldId.ToString(CultureInfo.InvariantCulture)}|{experienceId.ToString(CultureInfo.InvariantCulture)}";

        /// <summary>Sets an experience's state at once: a spinner on its row while the platform answers, and, if it refuses, the popup back to what it was with the reason in a tooltip.</summary>
        private async void SetExperienceState(DetailsConnection connection, int worldId, CoreExperienceItem item, ExperiencePublicationStatus status)
        {
            string key = ExperienceKey(connection, worldId, item.Id);
            if (!connection.Experiences.ContainsKey(worldId) || !changingExperiences.Add(key))
                return;

            connection.Experiences[worldId].StateErrors.Remove(item.Id);

            // Drawn once the popup's own event is over: the popup is rebuilt with the rest.
            ScheduleRender();

            EnvironmentExperiencesResult<ExperienceStatusOutcome> result;
            try
            {
                result = await EnvironmentExperiencesService.SetStatusAsync(connection.Key, worldId, item.Id, status);
            }
            catch (Exception ex)
            {
                result = new EnvironmentExperiencesResult<ExperienceStatusOutcome> { Status = EnvironmentExperiencesStatus.Failed, Message = ex.Message, WorldId = worldId };
            }

            changingExperiences.Remove(key);
            if (!IsCurrent(connection))
                return;

            // The world's experiences may have been read again while the request was out: the answer belongs to what is there now.
            if (connection.Experiences.TryGetValue(worldId, out WorldExperiences current) && current.Data != null)
            {
                CoreExperienceItem now = current.Data.CoreExperiences.FirstOrDefault(i => i.Id == item.Id);
                if (result.Ok && now != null)
                {
                    bool nowPublished = status == ExperiencePublicationStatus.Published;
                    bool wasPublished = string.Equals(now.Status, nameof(ExperiencePublicationStatus.Published), StringComparison.OrdinalIgnoreCase);
                    if (nowPublished != wasPublished)
                    {
                        ExperienceCounts counts = current.Data.Counts;
                        counts.CorePublished += nowPublished ? 1 : -1;
                        counts.CoreDraft += nowPublished ? -1 : 1;
                    }

                    now.Status = status.ToString();
                }
                else if (!result.Ok)
                {
                    current.StateErrors[item.Id] = result.Message;
                    if (result.Status == EnvironmentExperiencesStatus.NotPublishedInWorld)
                    {
                        current.Status = result.Status;
                        current.Message = result.Message;
                    }
                }
            }

            Render();
        }

        /// <summary>Creates the Core experience of the environment in a world, as a draft: one click, no dialog. Idempotent: a Core experience that exists is reported, not duplicated.</summary>
        private async void CreateCore(DetailsConnection connection, FilesTarget target)
        {
            if (!connection.Experiences.TryGetValue(target.WorldId.Value, out WorldExperiences experiences) || experiences.Creating)
                return;

            experiences.Creating = true;
            experiences.CreateStatus = null;
            experiences.CreateMessage = null;
            Render();

            await EnsureCoreIn(connection, target, experiences);
            Render();
        }

        /// <summary>One call of <see cref="EnvironmentExperiencesService.EnsureCoreAsync"/> for a world, its answer put in the world's experiences. True when it worked.</summary>
        private async Task<bool> EnsureCoreIn(DetailsConnection connection, FilesTarget target, WorldExperiences experiences)
        {
            EnvironmentExperiencesResult<CoreExperienceOutcome> result;
            try
            {
                result = await EnvironmentExperiencesService.EnsureCoreAsync(connection.Key, target.WorldId.Value, target.EnvId);
            }
            catch (Exception ex)
            {
                result = new EnvironmentExperiencesResult<CoreExperienceOutcome> { Status = EnvironmentExperiencesStatus.Failed, Message = ex.Message, WorldId = target.WorldId.Value };
            }

            experiences.Creating = false;
            if (!IsCurrent(connection))
                return false;

            if (result.Ok)
            {
                experiences.Data = result.Data;
                experiences.Status = EnvironmentExperiencesStatus.Ok;
                return true;
            }

            experiences.CreateStatus = result.Status;
            experiences.CreateMessage = result.Message;
            if (result.Status == EnvironmentExperiencesStatus.NotPublishedInWorld)
            {
                experiences.Status = result.Status;
                experiences.Message = result.Message;
            }

            return false;
        }

        #endregion
    }
}
#endif
