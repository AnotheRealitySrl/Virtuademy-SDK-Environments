using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Virtuademy.SDK.Environments.Editor
{
    /// <summary>
    /// What the SDK checks in an environment's localization file before it sends one: the rules the platform enforces (a file that
    /// breaks one is refused there, so it is refused here and nothing is sent), then what the tenant's languages make of it, as
    /// warnings. A C# mirror of <c>EnvironmentLocalizationCsvValidator</c> (Virtuademy-Backend-Core, <c>SPACS-Virtuademy/Helpers</c>), message
    /// for message: change both together. Where the two differ, the platform's answer wins and the file is refused there.
    /// </summary>
    /// <remarks>
    /// The rules are those of the player's I2 reader (<c>LocalizationReader.ReadCSV</c>, <c>LanguageSourceData.Import_CSV</c>), which is not an RFC 4180
    /// parser; the player runs it after turning every CR LF and CR into LF. <b>Encoding</b>: UTF-8 (a BOM is dropped by the platform; the file is sent as
    /// it is). <b>Lines</b>: LF, CR LF and a lone CR end a row; row 1 is the header and must be the first line; no empty line after it, no row of only commas;
    /// one line end after the last row is fine, a second is an empty line. <b>Cells</b>: <c>,</c> separates; a cell is unquoted (no <c>"</c> in it) or starts
    /// with <c>"</c> and ends at the closing one, <c>""</c> inside being a quote and line breaks allowed; a quote elsewhere, an empty quoted cell, text after
    /// a closing quote and a quote never closed are errors. <b>I2 cross-check</b>: the text is also read with a line-for-line port of I2's reader, and the
    /// first row it reads differently from the rules is an error. <b>Header</b>: <c>Key</c> exactly; <c>Type</c> and <c>Desc</c> only in columns 2 and 3, once
    /// each; every other column <c>Name [code]</c> (no brackets in the name, not starting with <c>$</c>, code <c>^[a-z]{2,3}(-[A-Za-z0-9]{2,8})?$</c>, no code
    /// twice, and in columns 2 and 3 no "type" or "desc" anywhere in the cell). <b>Rows</b>: more cells than the header is an error; fewer is fine, but with
    /// Type or Desc a row needs at least <c>1 + their number</c>; the Key is required, <c>^[A-Za-z0-9/_-]+$</c> with a character that is not <c>/</c>,
    /// unique exactly and unique once the slashes are removed (I2 drops every slash of a term but the first). At most <see cref="MaxErrors"/> errors are
    /// kept, each with its spreadsheet row (the header is row 1, an empty line is a row, a quoted cell with line breaks is one row).
    /// <para>
    /// <b>The tenant's languages</b> (when given; the platform does not know them): a language the tenant offers and the file lacks is a warning that says
    /// which language those users will see instead (the player's fallback, see <see cref="FallbackFor"/>); a column for a language the tenant does not offer
    /// is a warning that it is ignored there (the file is sent unchanged); a file with no language in common with the tenant is an error, because nobody on
    /// that tenant would see any of its texts.
    /// </para>
    /// </remarks>
    internal sealed class LocalizationCsvCheck : FileCheckResult
    {
        /// <summary>The largest file the platform accepts, in bytes.</summary>
        public const long MaxBytes = 2L * 1024 * 1024;

        public const int MaxErrors = 50;

        /// <summary>
        /// The order the player takes a missing translation from, the same for every tenant (the order of the languages in the
        /// platform's I2 Localization source): the first language with a text, after the other regions of the same language.
        /// </summary>
        public static readonly string[] FallbackOrder = { "it", "en", "fr", "de", "es" };

        private static readonly Regex language_code = new(@"^[a-z]{2,3}(-[A-Za-z0-9]{2,8})?$", RegexOptions.CultureInvariant);
        private static readonly Regex language_header = new(@"^(?<name>[^\[\]]*\S)\s*\[(?<code>[^\[\]]*)\]$", RegexOptions.CultureInvariant);
        private static readonly Regex key_pattern = new(@"^[A-Za-z0-9/_-]+$", RegexOptions.CultureInvariant);

        /// <summary>The language codes of the header, as written and in order.</summary>
        public List<string> Languages = new();

        /// <summary>How many texts (keys) the file defines.</summary>
        public int TermCount;

        /// <summary>The tenant languages the check compared the file with; null when none were given.</summary>
        public List<string> TenantLanguages;

        /// <summary>
        /// Checks a file. <paramref name="tenantLanguages"/> are the languages the tenant offers (<c>supportedLanguages</c> of its public
        /// configuration, an empty list standing for English alone, as the app reads it); null skips the tenant comparison.
        /// </summary>
        public static LocalizationCsvCheck Check(byte[] bytes, IReadOnlyList<string> tenantLanguages)
        {
            LocalizationCsvCheck result = new();

            if (bytes != null && bytes.Length > MaxBytes)
                result.Error("too-large", $"The file is {bytes.Length / (1024.0 * 1024.0):0.#} MB: the platform accepts a localization file of at most {MaxBytes / (1024 * 1024)} MB.");

            result.Validate(bytes ?? Array.Empty<byte>());

            if (tenantLanguages != null && result.Errors.Count == 0 && result.Languages.Count > 0)
                result.CompareWithTenant(tenantLanguages);

            return result;
        }

        private sealed class Record
        {
            public Record(int row) { Row = row; }

            public int Row { get; }
            public List<string> Cells { get; } = new();
            public bool IsBlank => Cells.Count == 1 && Cells[0].Length == 0;
        }

        private void AddRowError(int row, string message)
        {
            if (Errors.Count >= MaxErrors)
            {
                ErrorsTruncated = true;
                return;
            }

            Error("row", message, row);
        }

        private void Validate(byte[] data)
        {
            byte[] content = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? data.Skip(3).ToArray() : data;

            string text;
            try
            {
                text = new UTF8Encoding(false, true).GetString(content);
            }
            catch (DecoderFallbackException ex)
            {
                // The row of the bad byte: read the valid text before it with the same reader.
                string valid = Encoding.UTF8.GetString(content, 0, Math.Min(ex.Index < 0 ? 0 : ex.Index, content.Length));
                ReadRecords(Normalize(valid), new LocalizationCsvCheck(), out int badRow);
                AddRowError(badRow, "The file is not valid UTF-8. Save it as UTF-8 (CSV UTF-8 in Excel) and try again.");
                return;
            }

            // what the player does before it parses
            string normalized = Normalize(text);
            List<Record> records = ReadRecords(normalized, this, out _);

            if (records.Count == 0 || records[0].IsBlank)
            {
                if (Errors.Count == 0)
                {
                    AddRowError(1, records.Count == 0
                        ? "The file is empty: it needs a header line (Key, then Name [code] for each language)."
                        : "The first line must be the header (Key, then Name [code] for each language): remove the empty line above it.");
                }

                return;
            }

            Record header = records[0];
            int firstLanguageColumn = ReadHeader(header);

            // what I2 would read from this text: any row it reads differently from the rules above is refused
            HashSet<int> rowsWithErrors = new(Errors.Where(e => e.Row.HasValue).Select(e => e.Row.Value));
            CompareWithThePlayer(normalized, records, rowsWithErrors);

            int minimumCells = firstLanguageColumn; // Key, plus the Type and Desc columns I2 reads by index
            HashSet<string> keys = new(StringComparer.Ordinal);
            Dictionary<string, string> terms = new(StringComparer.Ordinal);

            foreach (Record record in records.Skip(1))
            {
                if (record.IsBlank)
                {
                    AddRowError(record.Row, "The line is empty. Remove it: the player would read it as a term with no name.");
                    continue;
                }

                if (record.Cells.Count > header.Cells.Count)
                    AddRowError(record.Row, $"The row has {record.Cells.Count} cells but the header has {header.Cells.Count} columns. A cell that contains a comma must be between quotes.");

                if (record.Cells.Count < minimumCells)
                    AddRowError(record.Row, $"The row has {record.Cells.Count} cells: with the Type or Desc column of the header it needs at least {minimumCells}.");

                string key = record.Cells[0];
                if (key.Length == 0)
                {
                    AddRowError(record.Row, "The Key is missing.");
                    continue;
                }

                if (!key_pattern.IsMatch(key))
                {
                    AddRowError(record.Row, $"The Key \"{key}\" has characters other than letters, digits, '/', '_' and '-'.");
                    continue;
                }

                string term = key.Replace("/", string.Empty);
                if (term.Length == 0)
                    AddRowError(record.Row, $"The Key \"{key}\" has nothing but slashes.");
                else if (keys.Contains(key))
                    AddRowError(record.Row, $"The Key \"{key}\" is already used by a previous row.");
                else if (terms.TryGetValue(term, out string other))
                    AddRowError(record.Row, $"The Key \"{key}\" is the same term as \"{other}\" in the player, which drops every slash of a key but the first: rename one of them.");
                else
                    terms[term] = key;

                keys.Add(key);
            }

            TermCount = keys.Count;
        }

        // The player's line ends: CR LF and CR become LF before the text is parsed.
        private static string Normalize(string text) => text.Replace("\r\n", "\n").Replace("\r", "\n");

        // header: Key, then Type and/or Desc in columns 2 and 3, then the languages. Returns the 0-based index of the first language.
        private int ReadHeader(Record header)
        {
            HashSet<string> seenCodes = new(StringComparer.OrdinalIgnoreCase);
            bool languageProblem = false;

            if (header.Cells[0] != "Key")
                AddRowError(header.Row, $"Column 1 of the header must be exactly \"Key\" (it is \"{header.Cells[0]}\").");

            // Type and Desc: only right after Key, in columns 2 and 3
            int column = 1;
            bool hasType = false;
            bool hasDesc = false;
            while (column < header.Cells.Count && column <= 2)
            {
                string cell = header.Cells[column];
                if (cell == "Type" && !hasType)
                    hasType = true;
                else if (cell == "Desc" && !hasDesc)
                    hasDesc = true;
                else
                    break;

                column++;
            }

            int firstLanguageColumn = column;

            for (int i = column; i < header.Cells.Count; i++)
            {
                string cell = header.Cells[i];

                if (cell == "Type" || cell == "Desc" || cell == "Key")
                {
                    AddRowError(header.Row, i < 3 && cell != "Key" && (hasType || hasDesc || i == column)
                        ? $"Column {i + 1}: \"{cell}\" is listed twice."
                        : $"Column {i + 1}: \"{cell}\" can only come right after Key, in column 2 or 3, before the languages.");
                    languageProblem = true;
                    continue;
                }

                Match match = language_header.Match(cell);
                if (!match.Success)
                {
                    string what = cell.Length == 0
                        ? "has no name"
                        : $"\"{cell}\" is not Type, Desc or a language written as Name [code] (the name has no square brackets)";
                    AddRowError(header.Row, $"Column {i + 1} {what}.");
                    languageProblem = true;
                    continue;
                }

                string name = match.Groups["name"].Value;
                string code = match.Groups["code"].Value;

                if (name.StartsWith("$", StringComparison.Ordinal))
                {
                    AddRowError(header.Row, $"Column {i + 1}: a language name cannot start with '$' (the player would leave the language disabled).");
                    languageProblem = true;
                    continue;
                }

                if ((i == 1 || i == 2) && (cell.IndexOf("type", StringComparison.OrdinalIgnoreCase) >= 0 || cell.IndexOf("desc", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    AddRowError(header.Row, $"Column {i + 1}: \"{cell}\" contains \"type\" or \"desc\", which the player reads as the Type or Desc column in column 2 or 3. Rename the language.");
                    languageProblem = true;
                    continue;
                }

                if (!language_code.IsMatch(code))
                {
                    AddRowError(header.Row, $"Column {i + 1}: \"{code}\" is not a language code (two or three lower-case letters, optionally followed by a region, like it, en, pt-BR or zh-Hans).");
                    languageProblem = true;
                    continue;
                }

                if (!seenCodes.Add(code))
                {
                    AddRowError(header.Row, $"Column {i + 1}: the language \"{code}\" is already in an earlier column.");
                    languageProblem = true;
                    continue;
                }

                Languages.Add(code);
            }

            if (Languages.Count == 0 && !languageProblem)
                AddRowError(header.Row, "The header has no language column: add at least one, like \"Italiano [it]\".");

            return firstLanguageColumn;
        }

        /// <summary>
        /// Splits the (normalized) text into records, RFC 4180 style with the extra refusals of the rules above. Stops at a quoted cell that
        /// is never closed, which swallows the rest of the file.
        /// </summary>
        private static List<Record> ReadRecords(string text, LocalizationCsvCheck errors, out int nextRow)
        {
            List<Record> records = new();
            Record current = new(1);
            StringBuilder cell = new();
            int row = 1;
            int i = 0;
            bool recordStarted = false;
            bool cellStarted = false;
            bool quoteReported = false;

            void EndCell()
            {
                current.Cells.Add(cell.ToString());
                cell.Clear();
                cellStarted = false;
                quoteReported = false;
            }

            void EndRecord()
            {
                EndCell();
                records.Add(current);
                row++;
                current = new Record(row);
                recordStarted = false;
            }

            while (i < text.Length)
            {
                char c = text[i];
                recordStarted = true;

                if (c == '"' && !cellStarted)
                {
                    i++;
                    cellStarted = true;
                    bool closed = false;

                    while (i < text.Length)
                    {
                        if (text[i] == '"')
                        {
                            if (i + 1 < text.Length && text[i + 1] == '"')
                            {
                                cell.Append('"');
                                i += 2;
                                continue;
                            }

                            closed = true;
                            i++;
                            break;
                        }

                        cell.Append(text[i]);
                        i++;
                    }

                    if (!closed)
                    {
                        errors.AddRowError(current.Row, "A quoted cell is not closed: a quote is missing, or a quote inside the cell is not doubled (\"\").");
                        nextRow = row;
                        current.Cells.Add(cell.ToString());
                        records.Add(current);
                        return records;
                    }

                    if (cell.Length == 0)
                        errors.AddRowError(current.Row, "An empty cell is written with nothing between the commas, not \"\": the player would read a quote character.");

                    // after the closing quote only a separator or a line end may follow
                    if (i < text.Length && text[i] != ',' && text[i] != '\n')
                    {
                        errors.AddRowError(current.Row, "Text follows the closing quote of a cell. A quote inside a quoted cell must be doubled (\"\").");

                        while (i < text.Length && text[i] != ',' && text[i] != '\n')
                        {
                            cell.Append(text[i]);
                            i++;
                        }
                    }

                    continue;
                }

                if (c == '"')
                {
                    // in the middle of a cell: I2 opens a quoted run here and swallows the commas that follow
                    if (!quoteReported)
                    {
                        errors.AddRowError(current.Row, "A quote inside a cell that does not start with one. Put the whole cell between quotes and double each quote inside it (\"\").");
                        quoteReported = true;
                    }

                    cell.Append(c);
                    i++;
                    continue;
                }

                if (c == ',')
                {
                    EndCell();
                    cellStarted = false;
                    i++;
                    continue;
                }

                if (c == '\n')
                {
                    EndRecord();
                    i++;
                    continue;
                }

                cell.Append(c);
                cellStarted = true;
                i++;
            }

            if (recordStarted)
            {
                // the last line has no line end
                current.Cells.Add(cell.ToString());
                records.Add(current);
            }

            nextRow = row;
            return records;
        }

        // ---- the player's reader ------------------------------------------------------------------------------
        // A port of I2's LocalizationReader.ReadCSV / ParseCSVline / AddCSVtoken (Virtuademy-Unity, Assets/_ThirdParty/I2), kept
        // line for line (as the platform's validator keeps it) so that what it makes of a text is what the player will see. It is
        // not used to read the file: it checks that the reader and the rules above agree on every row.

        private static List<string[]> ReadLikeThePlayer(string text)
        {
            List<string[]> rows = new();
            int start = 0;

            while (start < text.Length)
                rows.Add(ParsePlayerLine(text, ref start));

            return rows;
        }

        private static string[] ParsePlayerLine(string line, ref int start)
        {
            List<string> list = new();
            int length = line.Length;
            int wordStart = start;
            bool insideQuote = false;

            while (start < length)
            {
                char c = line[start];

                if (insideQuote)
                {
                    if (c == '"')
                    {
                        if (start + 1 >= length || line[start + 1] != '"')
                        {
                            insideQuote = false;
                        }
                        else if (start + 2 < length && line[start + 2] == '"')
                        {
                            insideQuote = false;
                            start += 2;
                        }
                        else
                        {
                            start++;
                        }
                    }
                }
                else if (c == '\n' || c == ',')
                {
                    AddPlayerToken(list, line, start, ref wordStart);
                    if (c == '\n')
                    {
                        start++;
                        break;
                    }
                }
                else if (c == '"')
                {
                    insideQuote = true;
                }

                start++;
            }

            if (start > wordStart)
                AddPlayerToken(list, line, start, ref wordStart);

            return list.ToArray();
        }

        private static void AddPlayerToken(List<string> list, string line, int end, ref int wordStart)
        {
            string text = line.Substring(wordStart, end - wordStart);
            wordStart = end + 1;

            text = text.Replace("\"\"", "\"");
            if (text.Length > 1 && text[0] == '"' && text[text.Length - 1] == '"')
                text = text.Substring(1, text.Length - 2);

            list.Add(text);
        }

        // The first row the player reads differently from the rules above, if it has no error of its own yet. One report is enough:
        // after a divergence the rows no longer line up.
        private void CompareWithThePlayer(string normalized, List<Record> records, HashSet<int> rowsWithErrors)
        {
            List<string[]> player = ReadLikeThePlayer(normalized);

            // a text that ends inside a quote has already been reported
            if (rowsWithErrors.Count > 0 && Errors.Any(e => e.Message.StartsWith("A quoted cell is not closed", StringComparison.Ordinal)))
                return;

            for (int i = 0; i < Math.Max(records.Count, player.Count); i++)
            {
                Record mine = i < records.Count ? records[i] : null;
                string[] theirs = i < player.Count ? player[i] : null;
                int row = mine?.Row ?? records[records.Count - 1].Row;

                if (mine == null || theirs == null || !SameCells(mine.Cells, theirs))
                {
                    if (!rowsWithErrors.Contains(row))
                        AddRowError(row, "The player would read this row differently from what is written (look at the quotes in it and in the rows above).");

                    return;
                }
            }
        }

        // trailing empty cells are not compared: the player drops a last empty cell at the end of the text, and ignores the ones past the header
        private static bool SameCells(List<string> mine, string[] theirs)
        {
            int a = mine.Count;
            while (a > 0 && mine[a - 1].Length == 0)
                a--;

            int b = theirs.Length;
            while (b > 0 && theirs[b - 1].Length == 0)
                b--;

            if (a != b)
                return false;

            for (int i = 0; i < a; i++)
                if (!string.Equals(mine[i], theirs[i], StringComparison.Ordinal))
                    return false;

            return true;
        }

        // ---- the tenant's languages (the SDK's own: the platform does not know them) --------------------------

        private void CompareWithTenant(IReadOnlyList<string> tenantLanguages)
        {
            List<string> tenant = tenantLanguages.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (tenant.Count == 0)
                tenant.Add("en");

            TenantLanguages = tenant;
            bool InFile(string code) => Languages.Any(l => string.Equals(l, code, StringComparison.OrdinalIgnoreCase));

            if (!tenant.Any(InFile))
            {
                Error("no-common-language", $"The file has no column for any language this tenant offers (the tenant offers {string.Join(", ", tenant)}; the file has {string.Join(", ", Languages)}): " +
                                            "nobody on this tenant would see its texts.");
                return;
            }

            foreach (string code in tenant.Where(t => !InFile(t)))
            {
                string fallback = FallbackFor(code, Languages);
                Warn("tenant-language-missing", fallback == null
                    ? $"The file has no '{code}' column: users of this tenant who choose it will see no text, and the file has none of {string.Join(", ", FallbackOrder)} to fall back to."
                    : $"The file has no '{code}' column: users of this tenant who choose it will see the '{fallback}' text.");
            }

            foreach (string code in Languages.Where(l => !tenant.Contains(l, StringComparer.OrdinalIgnoreCase)))
                Warn("language-not-offered", $"The file has a '{code}' column, but this tenant does not offer it: it is ignored on this tenant.");
        }

        /// <summary>
        /// The language a user who chose <paramref name="code"/> sees when the file has no text for it: another region of the same
        /// language first, then the first of <see cref="FallbackOrder"/> that the file has. Null when there is none.
        /// </summary>
        public static string FallbackFor(string code, IReadOnlyList<string> fileLanguages)
        {
            string baseOf(string c) => c.Split('-')[0].ToLowerInvariant();

            string sameLanguage = fileLanguages.FirstOrDefault(l => !string.Equals(l, code, StringComparison.OrdinalIgnoreCase) && baseOf(l) == baseOf(code));
            if (sameLanguage != null)
                return sameLanguage;

            return FallbackOrder.FirstOrDefault(o => fileLanguages.Any(l => string.Equals(l, o, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
