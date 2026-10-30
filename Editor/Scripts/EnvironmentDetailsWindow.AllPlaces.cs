#if !VIRTUADEMY_APPLICATION
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

namespace Virtuademy.SDK.Environments.Editor
{
    // The pane of All places, and the two layouts that replace the list when there is nothing to choose from (one place only, not published).
    // Everything that acts on many places is here and only here: save the online files into the project, replace in all places, create the missing Core experiences.
    internal sealed partial class EnvironmentDetailsWindow
    {
        private ReplaceAllFlow allFlow = new();

        private bool savingAll;
        private string saveAllMessage;
        private bool saveAllFailed;

        private bool creatingAll;
        private readonly List<(string Text, BannerKind Kind, string Tooltip)> createAllMessages = new();

        // Notes under the preview table: what the tenants' languages make of the localization file.
        private readonly List<string> previewNotes = new();

        // The platform's small card of a place's thumbnail, as a file status of its own, for the small pictures of the preview table.
        private readonly Dictionary<FilesTarget, FileStatus> cards = new();

        private FileStatus CardOf(FilesTarget target)
        {
            if (string.IsNullOrEmpty(target.CardUri))
                return null;

            if (!cards.TryGetValue(target, out FileStatus card))
                cards[target] = card = new FileStatus { Kind = FileKind.Thumbnail, Uri = target.CardUri, Present = true };

            return card;
        }

        private static string JoinOr(IEnumerable<string> names)
        {
            List<string> list = names.ToList();
            return list.Count <= 1 ? string.Join(string.Empty, list) : $"{string.Join(", ", list.Take(list.Count - 1))} or {list.Last()}";
        }

        #region Other layouts

        /// <summary>One place only: every connection was read and the scene is in exactly one place. No list: a line on what the list would have said, then the place.</summary>
        private void BuildOnePlaceOnly(VisualElement content)
        {
            (DetailsConnection connection, FilesTarget target) = AllPlaces().First();
            List<string> others = connections.Where(c => c != connection).Select(c => c.Label).ToList();
            string text = $"Published in one place only: {PlaceName(target)} on {ConnectionName(connection)}." +
                          (others.Count > 0 ? $" Not published on {JoinOr(others)}." : string.Empty);
            content.Add(Banner(BannerKind.Info, text));

            selected = new PlaceKey(connection.Key, target.WorldId);
            allSelected = false;
            BuildPlacePane(content, connection, target);
        }

        /// <summary>Not published: nothing to show online. A line pointing to Publish Environments, and a warning when the scene has no default files yet.</summary>
        private void BuildNotPublished(VisualElement content)
        {
            content.Add(Banner(BannerKind.Info, $"{scene.Name} is not published on {JoinOr(connections.Select(c => c.Label))}. " +
                                                "Publish it from Publish Environments: each new place gets the defaults above."));

            (string thumbnail, string localization) = EnvironmentFilesService.DefaultsOf(scene.Key, out _);
            if (thumbnail == null && localization == null)
                content.Add(Banner(BannerKind.Warning, "No defaults yet: pick a thumbnail and a localization file above, or the scene will be published without them."));
            else if (thumbnail == null)
                content.Add(Banner(BannerKind.Warning, "No default thumbnail yet: pick one above, or the scene will be published without it."));
            else if (localization == null)
                content.Add(Banner(BannerKind.Warning, "No default localization file yet: pick one above, or the scene will be published without it."));
        }

        #endregion

        #region All places

        private void BuildAllPlacesPane(VisualElement pane)
        {
            List<(DetailsConnection Connection, FilesTarget Target)> places = AllPlaces().ToList();
            int where = places.Select(p => p.Connection).Distinct().Count();

            VisualElement title = new();
            title.AddToClassList("vd-pane-title");
            title.Add(Text("All places", "vd-pane-title-name"));

            List<string> notes = new() { $"{AddressablesManagementWindow.Plural(places.Count, "place")} on {AddressablesManagementWindow.Plural(where, "connection")}" };
            notes.AddRange(connections.Where(c => c.Load == ConnectionLoad.LoggedOut).Select(c => $"{c.Label} is not logged in"));
            notes.AddRange(connections.Where(c => c.Load == ConnectionLoad.Failed).Select(c => $"{c.Label} could not be read"));
            title.Add(Text("· " + string.Join(" · ", notes), "muted", "vd-pane-title-where"));
            pane.Add(title);

            pane.Add(BuildSaveAllSection(places));
            pane.Add(BuildReplaceAllSection(places));
            pane.Add(BuildCreateAllSection(places));
        }

        #endregion

        #region Save to project

        private static IEnumerable<(FilesTarget Target, FileKind Kind, FileStatus Status)> AllFiles(IEnumerable<(DetailsConnection Connection, FilesTarget Target)> places) =>
            places.SelectMany(p => new[] { FileKind.Thumbnail, FileKind.Localization }.Select(k => (p.Target, k, p.Target.StatusOf(k)))).Where(f => f.Item3.Present);

        private VisualElement BuildSaveAllSection(List<(DetailsConnection Connection, FilesTarget Target)> places)
        {
            VisualElement section = Section("Save to project", null, out VisualElement bodyElement);

            List<(FilesTarget Target, FileKind Kind, FileStatus Status)> files = AllFiles(places).ToList();
            List<(FilesTarget Target, FileKind Kind, FileStatus Status)> missing = files.Where(f => f.Status.Presence == Presence.OnlineOnly).ToList();

            // The same file held in several places is written once.
            int thumbnails = missing.Where(f => f.Kind == FileKind.Thumbnail).Select(f => f.Status.Sha256 ?? f.Status.Uri).Distinct().Count();
            int localizations = missing.Where(f => f.Kind == FileKind.Localization).Select(f => f.Status.Sha256 ?? f.Status.Uri).Distinct().Count();
            int toSave = thumbnails + localizations;
            int already = files.Count(f => f.Status.Presence == Presence.InProject);

            VisualElement line = Row();
            line.Add(Text($"Online files not in the project yet: {toSave} ({AddressablesManagementWindow.Plural(thumbnails, "thumbnail")}, " +
                          $"{AddressablesManagementWindow.Plural(localizations, "localization file")}). {already} {(already == 1 ? "is" : "are")} already there and {(already == 1 ? "is" : "are")} not written again.",
                "vd-small", "vd-grow"));
            line.Add(savingAll
                ? Spinner()
                : Btn($"Save {AddressablesManagementWindow.Plural(toSave, "file")}", SaveAllToProject,
                    "Keeps each online file that the project does not hold yet in " + EnvironmentFilesService.PublishFolder + ".", enabled: toSave > 0));
            bodyElement.Add(line);

            if (!string.IsNullOrEmpty(saveAllMessage))
            {
                Label message = Text(saveAllMessage, "vd-small", saveAllFailed ? "vd-err" : "vd-ok");
                message.style.marginTop = 3;
                bodyElement.Add(message);
            }

            Label hint = Text($"Into {EnvironmentFilesService.PublishFolder}/ · places you cannot read are left out", "muted", "vd-wrap");
            hint.style.marginTop = 3;
            bodyElement.Add(hint);
            return section;
        }

        private async void SaveAllToProject()
        {
            if (savingAll)
                return;

            // One token for the whole run: after a Refresh or a new scene it is cancelled, and FlowToken would give a fresh one.
            CancellationToken ct = FlowToken;
            savingAll = true;
            saveAllMessage = null;
            saveAllFailed = false;
            Render();

            int written = 0;
            int failed = 0;
            string firstProblem = null;
            try
            {
                foreach ((DetailsConnection connection, FilesTarget target) in AllPlaces().ToList())
                {
                    foreach (FileKind kind in new[] { FileKind.Thumbnail, FileKind.Localization })
                    {
                        ct.ThrowIfCancellationRequested();
                        FileStatus status = target.StatusOf(kind);

                        // Read again on each file: a file saved a moment ago may be the one this place holds too.
                        if (!status.Present || status.Presence != Presence.OnlineOnly)
                            continue;

                        SavedFile saved = await EnvironmentFilesService.SaveOnlineFileAsync(target, kind, null, false, ct);
                        ct.ThrowIfCancellationRequested();
                        if (saved.Status == SaveStatus.Written)
                        {
                            written++;
                        }
                        else if (saved.Status == SaveStatus.Failed)
                        {
                            failed++;
                            firstProblem ??= $"{PlaceName(target)} on {connection.Label}: {Sentence(saved.Error)}.";
                        }
                    }

                    ScheduleRender();
                }
            }
            catch (OperationCanceledException)
            {
                // Stopped by a new scene, a Refresh or the window closing, which have already reset the state of this section.
                return;
            }
            catch (Exception ex)
            {
                failed++;
                firstProblem ??= Sentence(ex.Message) + ".";
            }

            if (ct.IsCancellationRequested)
                return;

            savingAll = false;
            saveAllFailed = failed > 0;
            saveAllMessage = failed == 0
                ? $"✓ Saved {AddressablesManagementWindow.Plural(written, "file")} to the project."
                : $"Saved {AddressablesManagementWindow.Plural(written, "file")}; {failed} could not be saved. {firstProblem}";
            Render();
        }

        #endregion

        #region Replace in all places

        private VisualElement BuildReplaceAllSection(List<(DetailsConnection Connection, FilesTarget Target)> places)
        {
            VisualElement section = Section("Replace in all places", null, out VisualElement bodyElement);
            VisualElement header = section.Q(className: "section-header");
            header.Add(Btn("Use the defaults", UseDefaultsForAll, "Fills both fields with this scene's defaults.", enabled: !allFlow.Applying && !allFlow.Previewing));

            bodyElement.Add(FilePick("Thumbnail", FileKind.Thumbnail, allFlow.Thumbnail, allFlow.ThumbnailFile));
            bodyElement.Add(FilePick("Localization file", FileKind.Localization, allFlow.Localization, allFlow.LocalizationFile));

            string conversion = ConversionNote(allFlow.ThumbnailFile);
            if (conversion != null)
            {
                Label note = Text(conversion, "muted", "vd-wrap");
                note.style.marginLeft = 126;
                bodyElement.Add(note);
            }

            foreach (PreparedFile file in new[] { allFlow.ThumbnailFile, allFlow.LocalizationFile }.Where(f => f != null))
            {
                string fileName = Path.GetFileName(file.SourceAsset);
                if (!file.Check.Valid)
                {
                    bodyElement.Add(BuildBadFile(fileName, file.SourceAsset, file.Check, null));
                    continue;
                }

                foreach (FileIssue warning in file.Check.Warnings)
                {
                    Label line = Text($"{fileName}: {warning}", "vd-small", "vd-warn");
                    line.style.marginLeft = 126;
                    bodyElement.Add(line);
                }
            }

            if (allFlow.Rows == null)
            {
                VisualElement bar = new();
                bar.AddToClassList("section-bar");
                bar.Add(Text("Pick one asset or both, then check what changes before anything is sent.", "section-note", "bar-text"));
                if (allFlow.Previewing || allFlow.Checking)
                    bar.Add(Spinner());
                bar.Add(Btn("Preview changes", PreviewReplaceAll, "Reads what each place holds now and lists what would change. Nothing is sent yet.",
                    enabled: allFlow.Valid && !allFlow.Previewing && !allFlow.Checking));
                section.Add(bar);
                return section;
            }

            // The preview, then the result, are sections of their own, right under.
            VisualElement wrapper = new();
            wrapper.Add(section);
            wrapper.Add(BuildPreviewSection());
            return wrapper;
        }

        private VisualElement FilePick(string label, FileKind kind, UnityEngine.Object value, PreparedFile prepared)
        {
            VisualElement row = new();
            row.AddToClassList("vd-filepick");
            row.Add(Text(label, "muted", "vd-filepick-label"));

            ObjectField field = new()
            {
                objectType = kind == FileKind.Thumbnail ? typeof(Texture2D) : typeof(TextAsset),
                allowSceneObjects = false,
                tooltip = kind == FileKind.Thumbnail ? "A PNG or JPEG image of the project." : "A .csv of the project.",
                value = value,
            };
            field.SetEnabled(!allFlow.Applying && !allFlow.Previewing);
            string emptyText = kind == FileKind.Thumbnail ? "None (Image)" : "None (Localization file)";
            field.RegisterValueChangedCallback(evt =>
            {
                UnityEngine.Object accepted = SceneDefaults.Accept(kind, new[] { evt.newValue }, out string refusal);
                if (evt.newValue != null && accepted == null)
                {
                    field.SetValueWithoutNotify(evt.previousValue);
                    ShowDefaultsRefusal(refusal);
                    return;
                }

                SetAllAsset(kind, accepted);
            });
            field.RegisterCallback<GeometryChangedEvent>(_ => FixEmptyDisplay(field, emptyText));
            field.schedule.Execute(() => FixEmptyDisplay(field, emptyText));
            row.Add(field);

            string info = null;
            if (prepared != null && prepared.Check.Valid)
            {
                if (prepared.Check is ThumbnailCheck image)
                    info = $"{(image.Format ?? "image").ToUpperInvariant()} · {image.Width} × {image.Height}";
                else if (prepared.Check is LocalizationCsvCheck csv)
                    info = $"{csv.TermCount} keys · {(csv.Languages.Count == 0 ? "no language" : string.Join(", ", csv.Languages))}";
            }

            row.Add(Text(info ?? string.Empty, "muted", "vd-filepick-info"));
            return row;
        }

        private void SetAllAsset(FileKind kind, UnityEngine.Object asset)
        {
            if (kind == FileKind.Thumbnail)
                allFlow.Thumbnail = asset as Texture2D;
            else
                allFlow.Localization = asset as TextAsset;

            CheckAllFilesAsync();
        }

        private void UseDefaultsForAll()
        {
            SceneListScriptableObject.SceneConfiguration config = scene == null ? null : SceneDefaults.ConfigOf(list, scene.Key);
            allFlow.Thumbnail = config?.DefaultThumbnail;
            allFlow.Localization = config?.DefaultLocalization;
            CheckAllFilesAsync();
        }

        /// <summary>
        /// The local checks of the two assets (image: decodes, size; localization file: the platform's rules), before anything is sent. They run on the
        /// main thread, so "Checking..." is drawn first; if the assets change meanwhile the later call wins.
        /// </summary>
        private async void CheckAllFilesAsync()
        {
            ReleaseNewPreviews();
            ReplaceAllFlow flow = allFlow;
            Texture2D thumbnail = flow.Thumbnail;
            TextAsset localization = flow.Localization;
            flow.Rows = null;
            flow.Applied = false;
            flow.Applying = false;
            flow.ThumbnailFile = null;
            flow.LocalizationFile = null;
            previewNotes.Clear();

            if (thumbnail == null && localization == null)
            {
                flow.Checking = false;
                ScheduleRender();
                return;
            }

            flow.Checking = true;
            ScheduleRender();
            await Task.Delay(60);
            if (allFlow != flow || flow.Thumbnail != thumbnail || flow.Localization != localization)
                return;

            flow.ThumbnailFile = Prepare(thumbnail, FileKind.Thumbnail);
            flow.LocalizationFile = Prepare(localization, FileKind.Localization);
            flow.Checking = false;
            ScheduleRender();
        }

        private static PreparedFile Prepare(UnityEngine.Object asset, FileKind kind)
        {
            if (asset == null)
                return null;

            string path = AssetDatabase.GetAssetPath(asset);
            try
            {
                return kind == FileKind.Thumbnail
                    ? EnvironmentFilesService.PrepareThumbnail(path, write: false)
                    : EnvironmentFilesService.PrepareLocalization(path, null);
            }
            catch (Exception ex)
            {
                FileCheckResult check = kind == FileKind.Thumbnail ? new ThumbnailCheck() : new LocalizationCsvCheck();
                check.Errors.Add(new FileIssue { Code = "unreadable", Message = $"The file could not be checked: {Sentence(ex.Message)}." });
                return new PreparedFile { Kind = kind, SourceAsset = path, Check = check };
            }
        }

        /// <summary>
        /// Reads what every place holds now (the files go to the cache folder), works out what each would do, and shows the table. Nothing is sent.
        /// </summary>
        private async void PreviewReplaceAll()
        {
            if (!allFlow.Valid || allFlow.Previewing)
                return;

            ReplaceAllFlow flow = allFlow;
            flow.Previewing = true;
            flow.Rows = null;
            Render();

            List<ReplaceRow> rows = new();
            previewNotes.Clear();
            try
            {
                List<(DetailsConnection Connection, FilesTarget Target)> places = AllPlaces().ToList();

                // The files the places hold now: the pictures and the key counts of the table come from them.
                List<(DetailsConnection Connection, FileStatus Status)> toRead = new();
                foreach ((DetailsConnection connection, FilesTarget target) in places)
                {
                    // The small picture of the table is the platform's card when it has one: much lighter than the hero image.
                    FileStatus picture = CardOf(target) ?? target.Thumbnail;
                    if (flow.ThumbnailFile != null && picture.Present && picture.CachePath == null)
                        toRead.Add((connection, picture));
                    if (flow.LocalizationFile != null && target.Localization.Present && target.Localization.CachePath == null)
                        toRead.Add((connection, target.Localization));
                }

                using (SemaphoreSlim slots = new(max_parallel_experiences))
                {
                    await Task.WhenAll(toRead.Select(async read =>
                    {
                        await slots.WaitAsync(FlowToken);
                        try
                        {
                            await EnvironmentFilesService.EnsureCachedAsync(read.Status, FlowToken);
                        }
                        finally
                        {
                            slots.Release();
                        }
                    }));
                }

                foreach ((DetailsConnection connection, FilesTarget target) in places)
                {
                    ReplaceRow row = new() { Connection = connection, Target = target, Status = RowStatusOf(connection, target, flow) };
                    row.Ticked = row.CanChange;
                    rows.Add(row);
                }

                if (flow.LocalizationFile != null)
                    await NoteTenantLanguagesAsync(flow, places.Select(p => p.Connection).Distinct().ToList());
            }
            catch (OperationCanceledException)
            {
                flow.Previewing = false;
                return;
            }
            catch (Exception ex)
            {
                flow.Previewing = false;
                previewNotes.Add($"The places could not all be read: {Sentence(ex.Message)}.");
                Render();
                return;
            }

            flow.Previewing = false;
            if (allFlow != flow)
                return;

            flow.Rows = rows;
            flow.Applied = false;
            Render();
        }

        /// <summary>What the file's languages mean for each tenant it would go to: the check's warnings, and the one problem that makes it useless there.</summary>
        private async Task NoteTenantLanguagesAsync(ReplaceAllFlow flow, List<DetailsConnection> involved)
        {
            byte[] bytes = flow.LocalizationFile.Bytes;
            string name = Path.GetFileName(flow.LocalizationFile.SourceAsset);
            foreach (DetailsConnection connection in involved)
            {
                List<string> languages = await LanguagesOfAsync(connection);
                if (languages == null)
                    continue;

                LocalizationCsvCheck check = LocalizationCsvCheck.Check(bytes, languages);
                foreach (FileIssue issue in check.Errors.Concat(check.Warnings))
                    previewNotes.Add($"{connection.Label}: {name}: {issue.Message}");
            }
        }

        private static ReplaceRowStatus RowStatusOf(DetailsConnection connection, FilesTarget target, ReplaceAllFlow flow)
        {
            if (connection.Support == WriteSupport.NotSupported)
                return ReplaceRowStatus.SkippedUnsupported;

            if (EnvironmentFilesService.RefusalFor(target) != null)
                return ReplaceRowStatus.SkippedNotAllowed;

            bool same = new[] { flow.ThumbnailFile, flow.LocalizationFile }.Where(f => f != null).All(f => HoldsAlready(target.StatusOf(f.Kind), f));
            if (same)
                return ReplaceRowStatus.Unchanged;

            return target.IsTenant ? ReplaceRowStatus.WillChangeShared : ReplaceRowStatus.WillChange;
        }

        private static bool HoldsAlready(FileStatus status, PreparedFile file) =>
            status.Present && status.Sha256 != null
            && (string.Equals(status.Sha256, file.Sha256, StringComparison.OrdinalIgnoreCase)
                || (file.StoredSha256 != null && string.Equals(status.Sha256, file.StoredSha256, StringComparison.OrdinalIgnoreCase)));

        private static string StatusText(ReplaceRowStatus status) => status switch
        {
            ReplaceRowStatus.WillChange => "Will change",
            ReplaceRowStatus.WillChangeShared => "Will change · shared by its worlds",
            ReplaceRowStatus.Unchanged => "Unchanged: same file",
            ReplaceRowStatus.SkippedNotAllowed => "Skipped: not allowed",
            _ => "Skipped: tenant doesn't accept it yet",
        };

        private VisualElement BuildPreviewSection()
        {
            List<ReplaceRow> rows = allFlow.Rows;
            bool applied = allFlow.Applied;

            int willChange = rows.Count(r => r.CanChange && r.Ticked);
            string summary;
            if (applied)
            {
                int updated = rows.Count(r => r.Result == ReplaceRowResult.Updated);
                int failedRows = rows.Count(r => r.Result == ReplaceRowResult.Failed);
                int unchanged = rows.Count(r => r.Result == ReplaceRowResult.Unchanged || (r.Result == ReplaceRowResult.None && r.Status == ReplaceRowStatus.Unchanged));
                int skipped = rows.Count(r => r.Result == ReplaceRowResult.Skipped || (r.Result == ReplaceRowResult.None && r.Status != ReplaceRowStatus.Unchanged));
                summary = $"{updated} updated · {failedRows} failed · {skipped} skipped" + (unchanged > 0 ? $" · {unchanged} unchanged" : string.Empty);
            }
            else
            {
                summary = $"{AddressablesManagementWindow.Plural(willChange, "place")} will change";
            }

            VisualElement section = Section(applied ? "Result" : "Preview", summary, out VisualElement bodyElement);
            bodyElement.style.paddingLeft = 0;
            bodyElement.style.paddingRight = 0;
            bodyElement.style.paddingTop = 0;
            bodyElement.style.paddingBottom = 0;

            if (applied && !allFlow.Applying)
            {
                int failed = rows.Count(r => r.Result == ReplaceRowResult.Failed);
                if (failed > 0)
                {
                    bodyElement.Add(Banner(BannerKind.Error,
                        $"{failed} place{(failed == 1 ? " was" : "s were")} not updated. The others are already changed; their previous files were saved to the project first.",
                        Btn("Retry failed (" + failed + ")", RetryFailed, "Sends the files again to the places that failed. Nothing else is touched.")));
                }
            }

            VisualElement head = new();
            head.AddToClassList("vd-pv-head");
            head.Add(new VisualElement().WithClass("vd-pv-tick"));
            head.Add(Text("Place", "vd-pv-place"));
            head.Add(Text("Thumbnail", "vd-pv-thumb"));
            head.Add(Text("Localization file", "vd-pv-loc"));
            head.Add(Text(applied ? "Result" : string.Empty, "vd-pv-result"));
            bodyElement.Add(head);

            foreach (ReplaceRow row in rows)
                bodyElement.Add(BuildPreviewRow(row, applied));

            foreach (string note in previewNotes)
            {
                Label line = Text(note, "vd-small", "vd-warn");
                line.style.marginTop = line.style.marginBottom = line.style.marginLeft = line.style.marginRight = 4;
                line.style.whiteSpace = WhiteSpace.Normal;
                bodyElement.Add(line);
            }

            VisualElement bar = new();
            bar.AddToClassList("section-bar");
            if (!applied)
            {
                bar.Add(Text("The files these places hold now are saved to the project first, unless they are already there.", "section-note", "bar-text"));
                bar.Add(Btn("Cancel", () => { allFlow.Rows = null; Render(); }));
                bar.Add(Btn($"Apply to {AddressablesManagementWindow.Plural(willChange, "place")}", () => ApplyReplaceAll(rows.Where(r => r.CanChange && r.Ticked).ToList()),
                    "Sends the files to the ticked places. Each place's previous file is saved to the project first. Nothing is rolled back if one fails.", primary: true, enabled: willChange > 0));
            }
            else
            {
                bar.Add(allFlow.Applying ? Row(Spinner(), Text("Applying…", "muted")) : new VisualElement().WithClass("bar-text"));
                bar.Add(Btn("Close", () => { allFlow.Rows = null; allFlow.Applied = false; Render(); }, enabled: !allFlow.Applying));
            }

            section.Add(bar);
            return section;
        }

        private VisualElement BuildPreviewRow(ReplaceRow row, bool applied)
        {
            VisualElement element = new();
            element.AddToClassList("vd-pv-row");
            bool dim = !row.CanChange || (!applied && !row.Ticked);
            element.EnableInClassList("vd-pv-row--off", dim);

            Toggle tick = new() { value = row.Ticked };
            tick.AddToClassList("vd-pv-tick");
            tick.SetEnabled(row.CanChange && !applied);
            tick.RegisterValueChangedCallback(evt =>
            {
                row.Ticked = evt.newValue;
                ScheduleRender();
            });
            element.Add(tick);

            element.Add(Text($"{row.Connection.Label} · {PlaceName(row.Target)}", "vd-pv-place"));

            VisualElement thumbnail = new();
            thumbnail.AddToClassList("vd-pv-thumb");
            FileStatus nowThumbnail = row.Target.Thumbnail;
            FileStatus picture = CardOf(row.Target) ?? nowThumbnail;
            RequestCache(row.Connection, picture);
            thumbnail.Add(Thumb(picture.Present ? PreviewOf(picture.CachePath) : null, 40, 22));
            PreparedFile next = allFlow.ThumbnailFile;
            if (next != null && row.CanChange && !HoldsAlready(nowThumbnail, next))
            {
                Label arrow = Text("→", "vd-strip-arrow");
                arrow.style.marginLeft = 3;
                arrow.style.marginRight = 3;
                thumbnail.Add(arrow);
                thumbnail.Add(Thumb(PreviewOfBytes("new|" + next.Sha256, next.Bytes), 40, 22));
            }

            element.Add(thumbnail);

            FileStatus nowLocalization = row.Target.Localization;
            LocalizationCsvCheck before = nowLocalization.Present ? ContentOf(nowLocalization.CachePath) : null;
            string localization;
            if (allFlow.LocalizationFile != null && row.CanChange && !HoldsAlready(nowLocalization, allFlow.LocalizationFile))
                localization = Difference(before, (LocalizationCsvCheck)allFlow.LocalizationFile.Check, nowLocalization.Present);
            else
                localization = before != null ? $"{before.TermCount} keys" : nowLocalization.Present ? "…" : "none";
            element.Add(Text(localization, "vd-small", "vd-pv-loc"));

            element.Add(BuildRowResult(row, applied));
            return element;
        }

        private VisualElement BuildRowResult(ReplaceRow row, bool applied)
        {
            VisualElement cell = new();
            cell.AddToClassList("vd-pv-result");
            cell.style.flexDirection = FlexDirection.Row;
            cell.style.alignItems = Align.Center;

            if (applied)
            {
                switch (row.Result)
                {
                    case ReplaceRowResult.Pending:
                        cell.Add(Spinner());
                        cell.Add(Text("Sending…", "muted"));
                        return cell;
                    case ReplaceRowResult.Updated:
                        cell.Add(Text("✓ Updated", "vd-small", "vd-ok"));
                        if (!string.IsNullOrEmpty(row.ResultMessage))
                        {
                            Label warning = Text(row.ResultMessage, "vd-small", "vd-warn");
                            warning.style.marginLeft = 6;
                            warning.tooltip = row.ResultMessage;
                            cell.Add(warning);
                        }

                        return cell;
                    case ReplaceRowResult.Unchanged:
                        cell.Add(Text("Unchanged: same file", "vd-small", "muted"));
                        return cell;
                    case ReplaceRowResult.Failed:
                        Label failed = Text($"× Failed: {Sentence(row.ResultMessage)}", "vd-small", "vd-err");
                        failed.tooltip = row.ResultMessage ?? string.Empty;
                        cell.Add(failed);
                        return cell;
                    case ReplaceRowResult.Skipped:
                        cell.Add(Text(row.ResultMessage ?? StatusText(row.Status), "vd-small", "muted"));
                        return cell;
                }

                // A row that was not sent: not ticked, or one that could not change.
                cell.Add(Text(row.CanChange ? "Skipped: not ticked" : StatusText(row.Status), "vd-small", "muted"));
                return cell;
            }

            cell.Add(row.CanChange ? Text(StatusText(row.Status), "vd-small") : Text(StatusText(row.Status), "vd-small", "muted"));
            return cell;
        }

        private void RetryFailed()
        {
            if (allFlow.Rows == null || allFlow.Applying)
                return;

            ApplyReplaceAll(allFlow.Rows.Where(r => r.Result == ReplaceRowResult.Failed).ToList());
        }

        /// <summary>
        /// Sends the files to the given rows, place by place, the thumbnail first. A place that fails is reported and the others go on: nothing is rolled back. Each place's
        /// previous file is saved to the project first by <see cref="EnvironmentFilesService.ReplaceOneAsync"/>, unless the project already holds it.
        /// </summary>
        private async void ApplyReplaceAll(List<ReplaceRow> rows)
        {
            ReplaceAllFlow flow = allFlow;
            if (flow.Applying || rows.Count == 0)
                return;

            // One token for the whole run, read once: after a Refresh or a new scene it is cancelled, and FlowToken would give a fresh one.
            CancellationToken ct = FlowToken;
            flow.Applying = true;
            flow.Applied = true;
            foreach (ReplaceRow row in rows)
            {
                row.Result = ReplaceRowResult.Pending;
                row.ResultMessage = null;
            }

            Render();

            try
            {
                foreach (ReplaceRow row in rows)
                {
                    List<FileOutcome> outcomes = new();
                    foreach (PreparedFile file in new[] { flow.ThumbnailFile, flow.LocalizationFile }.Where(f => f != null))
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            outcomes.Add(await EnvironmentFilesService.ReplaceOneAsync(row.Target, file, false, ct));
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            outcomes.Add(new FileOutcome { Target = row.Target, Kind = file.Kind, Result = OutcomeKind.Failed, Message = ex.Message });
                        }

                        // The window moved on while the file was out: what is shown is not this run's any more.
                        if (allFlow != flow || ct.IsCancellationRequested)
                            return;
                    }

                    Summarize(row, outcomes);
                    cards.Remove(row.Target);
                    ScheduleRender();
                }
            }
            catch (OperationCanceledException)
            {
                flow.Applying = false;
                foreach (ReplaceRow row in rows.Where(r => r.Result == ReplaceRowResult.Pending))
                    row.Result = ReplaceRowResult.None;
                return;
            }

            flow.Applying = false;
            if (allFlow == flow)
                Render();
        }

        private static void Summarize(ReplaceRow row, List<FileOutcome> outcomes)
        {
            FileOutcome failed = outcomes.FirstOrDefault(o => o.Result == OutcomeKind.Failed);
            if (failed != null)
            {
                row.Result = ReplaceRowResult.Failed;
                row.ResultMessage = failed.Message;
            }
            else if (outcomes.Any(o => o.Result == OutcomeKind.Refused))
            {
                row.Result = ReplaceRowResult.Skipped;
                row.ResultMessage = StatusText(ReplaceRowStatus.SkippedNotAllowed);
            }
            else if (outcomes.Any(o => o.Result == OutcomeKind.Unsupported))
            {
                row.Result = ReplaceRowResult.Skipped;
                row.ResultMessage = StatusText(ReplaceRowStatus.SkippedUnsupported);
            }
            else if (outcomes.All(o => o.Result == OutcomeKind.Unchanged))
            {
                row.Result = ReplaceRowResult.Unchanged;
            }
            else
            {
                row.Result = ReplaceRowResult.Updated;
                row.ResultMessage = string.Join(" ", outcomes.Where(o => o.Warning != null).Select(o => o.Warning));
            }
        }

        #endregion

        #region Experiences in all worlds

        private VisualElement BuildCreateAllSection(List<(DetailsConnection Connection, FilesTarget Target)> places)
        {
            VisualElement section = Section("Experiences in all worlds", null, out VisualElement bodyElement);

            List<(DetailsConnection Connection, FilesTarget Target)> missing = places
                .Where(p => !p.Target.IsTenant && p.Connection.Experiences.TryGetValue(p.Target.WorldId.Value, out WorldExperiences e) && e.Known && e.Data.HasNoCore && e.Data.CanCreateCore && e.CreateStatus != EnvironmentExperiencesStatus.WorldHasNoCapacity)
                .ToList();

            bool several = connections.Count(c => c.Load == ConnectionLoad.Loaded) > 1;
            IEnumerable<string> names = missing.Select(m => several ? $"{m.Target.WorldLabel} ({m.Connection.Label})" : m.Target.WorldLabel);

            VisualElement line = Row();
            line.Add(Text(missing.Count == 0
                    ? "No world is missing a Core experience for this scene."
                    : $"{missing.Count} world{(missing.Count == 1 ? " has" : "s have")} no Core experience for this scene: {string.Join(", ", names)}.", "vd-small", "vd-grow"));
            if (creatingAll)
                line.Add(Spinner());
            else if (missing.Count > 0)
                line.Add(Btn($"Create Core experiences ({missing.Count})", () => CreateAll(missing),
                    "Creates the Core experience of this environment, as a draft, in each of these worlds. A world that already has one is left as it is.", primary: true));
            bodyElement.Add(line);

            foreach ((string text, BannerKind kind, string tooltip) in createAllMessages)
                bodyElement.Add(WithTooltip(Banner(kind, text), tooltip));

            int cannotSee = places.Count(p => !p.Target.IsTenant && p.Connection.Experiences.TryGetValue(p.Target.WorldId.Value, out WorldExperiences e) && !e.Loading && e.Status == EnvironmentExperiencesStatus.NotAllowed);
            int notOffered = places.Where(p => !p.Target.IsTenant && p.Connection.Experiences.TryGetValue(p.Target.WorldId.Value, out WorldExperiences e) && !e.Loading && e.Status == EnvironmentExperiencesStatus.NotSupportedByTenant)
                .Select(p => p.Connection).Distinct().Count();
            Label left = Text($"Worlds you cannot see ({cannotSee}) and tenants that don't offer it yet ({notOffered}) are left out.", "muted", "vd-wrap");
            left.style.marginTop = 3;
            bodyElement.Add(left);
            return section;
        }

        /// <summary>Creates the missing Core experiences world by world (<see cref="EnvironmentExperiencesService.EnsureCoreAsync"/>: idempotent, so a repeat creates no duplicate).</summary>
        private async void CreateAll(List<(DetailsConnection Connection, FilesTarget Target)> missing)
        {
            if (creatingAll)
                return;

            // One token for the whole run, read once: after a Refresh or a new scene it is cancelled, and FlowToken would give a fresh one.
            CancellationToken ct = FlowToken;
            creatingAll = true;
            createAllMessages.Clear();
            Render();

            int created = 0;
            List<(string Text, string Tooltip)> problems = new();
            try
            {
                foreach ((DetailsConnection connection, FilesTarget target) in missing)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!connection.Experiences.TryGetValue(target.WorldId.Value, out WorldExperiences experiences))
                        continue;

                    experiences.Creating = true;
                    experiences.CreateStatus = null;
                    ScheduleRender();

                    bool ok = await EnsureCoreIn(connection, target, experiences);
                    if (ct.IsCancellationRequested)
                        return;

                    if (ok)
                        created++;
                    else
                        problems.Add(($"{target.WorldLabel}: {CreateProblemText(experiences.CreateStatus)}", experiences.CreateMessage));
                }
            }
            catch (OperationCanceledException)
            {
                // Stopped by a new scene, a Refresh or the window closing, which have already reset the state of this section.
                return;
            }

            creatingAll = false;
            if (created > 0)
                createAllMessages.Add(($"Created {AddressablesManagementWindow.Plural(created, "Core experience")}. They are drafts: set each one's state in its world.", BannerKind.Info, null));
            foreach ((string text, string tooltip) in problems)
                createAllMessages.Add((text, BannerKind.Warning, tooltip));
            Render();
        }

        #endregion
    }
}
#endif
