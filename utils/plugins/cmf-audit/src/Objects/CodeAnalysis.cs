using System.Text;
using System.Text.RegularExpressions;

namespace audit.CodeAnalysis
{
    public static class TSComplexityCalculator
    {
        // Pre-compiled regexes over *code with comments/strings removed*
        private static readonly Regex IfRegex = new(@"\bif\b", RegexOptions.Compiled);
        private static readonly Regex ForRegex = new(@"\bfor\b", RegexOptions.Compiled);
        private static readonly Regex WhileRegex = new(@"\bwhile\b", RegexOptions.Compiled);
        private static readonly Regex DoRegex = new(@"\bdo\b", RegexOptions.Compiled);
        private static readonly Regex SwitchRegex = new(@"\bswitch\b", RegexOptions.Compiled);
        private static readonly Regex CaseRegex = new(@"\bcase\b", RegexOptions.Compiled);
        private static readonly Regex CatchRegex = new(@"\bcatch\b", RegexOptions.Compiled);

        // Ternary operators: ? ... :
        private static readonly Regex TernaryRegex = new(@"\?[^?:]+:", RegexOptions.Compiled);

        // Logical operators
        private static readonly Regex AndRegex = new(@"&&", RegexOptions.Compiled);
        private static readonly Regex OrRegex = new(@"\|\|", RegexOptions.Compiled);
        private static readonly Regex NullishRegex = new(@"\?\?", RegexOptions.Compiled);

        // Arrow functions
        private static readonly Regex ArrowRegex = new(@"=>", RegexOptions.Compiled);

        /// <summary>
        /// Calculates the total cyclomatic complexity of all detected top-level
        /// methods/functions in the given TypeScript code snippet.
        /// </summary>
        public static int CalculateClassComplexity(string[] tsClassLines)
        {
            if (tsClassLines == null || tsClassLines.Length == 0)
                return 0;

            var totalComplexity = 0;
            var i = 0;

            while (i < tsClassLines.Length)
            {
                var line = tsClassLines[i];
                if (IsMethodStart(line))
                {
                    var (methodLines, endIndex) = CollectFunctionBlock(tsClassLines, i);

                    if (methodLines.Count > 0)
                    {
                        var methodText = string.Join(Environment.NewLine, methodLines);
                        totalComplexity += CalculateCyclomaticComplexity(methodText);
                    }

                    i = endIndex + 1;
                }
                else
                {
                    i++;
                }
            }

            return totalComplexity;
        }

        /// <summary>
        /// Calculates cyclomatic complexity for a given method body.
        /// This is where we approximate AST-level behavior.
        /// </summary>
        private static int CalculateCyclomaticComplexity(string methodText)
        {
            if (string.IsNullOrWhiteSpace(methodText))
                return 0;

            // Remove comments and strings/templates so regexes don't see them.
            var code = StripCommentsAndStrings(methodText);

            // Base complexity: 1 for the method entry.
            var complexity = 1;

            // Structural decision points
            complexity += IfRegex.Matches(code).Count;
            complexity += ForRegex.Matches(code).Count;
            complexity += WhileRegex.Matches(code).Count;
            complexity += DoRegex.Matches(code).Count;
            complexity += SwitchRegex.Matches(code).Count;
            complexity += CaseRegex.Matches(code).Count;
            complexity += CatchRegex.Matches(code).Count;

            // Expression-level decision points
            complexity += TernaryRegex.Matches(code).Count;
            complexity += AndRegex.Matches(code).Count;
            complexity += OrRegex.Matches(code).Count;
            complexity += NullishRegex.Matches(code).Count;

            // Arrow functions often encapsulate additional control flow.
            complexity += ArrowRegex.Matches(code).Count;

            return complexity;
        }

        /// <summary>
        /// Quick heuristic to decide if a line looks like a TS method/function start.
        /// Handles "public main(...", "private foo(...", "async bar(...", "function baz(...", and
        /// simple class methods like "main(inputs: any) {"
        /// </summary>
        private static bool IsMethodStart(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            var trimmed = line.TrimStart();

            // Must have a parameter list
            if (!trimmed.Contains('('))
                return false;

            // Ignore declarations like: "if (...)", "for (...)" etc.
            if (trimmed.StartsWith("if ") ||
                trimmed.StartsWith("for ") ||
                trimmed.StartsWith("while ") ||
                trimmed.StartsWith("switch ") ||
                trimmed.StartsWith("catch ") ||
                trimmed.StartsWith("case ") ||
                trimmed.StartsWith("do "))
            {
                return false;
            }

            // Common TS method/function shapes
            if (trimmed.StartsWith("public ") ||
                trimmed.StartsWith("private ") ||
                trimmed.StartsWith("protected ") ||
                trimmed.StartsWith("async ") ||
                trimmed.StartsWith("public async ") ||
                trimmed.StartsWith("private async ") ||
                trimmed.StartsWith("protected async ") ||
                trimmed.StartsWith("function "))
            {
                return true;
            }

            // Bare class method: "main(inputs: any, outputs: any) {"
            // Heuristic: starts with identifier, then '(', not starting with keyword like "if".
            // A bit loose, but good enough for methods.
            var firstTokenEnd = IndexOfFirstNonIdentifierChar(trimmed);
            if (firstTokenEnd > 0 &&
                firstTokenEnd < trimmed.Length &&
                trimmed[firstTokenEnd] == '(')
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Collects the block of a function/method starting at startIndex.
        /// Uses brace depth from the first '{' onward.
        /// </summary>
        private static (List<string> Lines, int EndIndex) CollectFunctionBlock(string[] lines, int startIndex)
        {
            var result = new List<string>();
            var depth = 0;
            var seenOpeningBrace = false;

            for (var i = startIndex; i < lines.Length; i++)
            {
                var line = lines[i];
                result.Add(line);

                foreach (var ch in line)
                {
                    if (ch == '{')
                    {
                        depth++;
                        seenOpeningBrace = true;
                    }
                    else if (ch == '}')
                    {
                        depth--;
                    }
                }

                // If we have seen an opening brace and returned to depth 0, we assume the method ended.
                if (seenOpeningBrace && depth <= 0)
                {
                    return (result, i);
                }
            }

            // Reached end of file without closing all braces; return what we have.
            return (result, lines.Length - 1);
        }

        /// <summary>
        /// Strips comments (//, /* */) and string/template literals ('', "", ``)
        /// while preserving line breaks and code layout as much as possible,
        /// so that regexes don't see tokens inside them.
        /// </summary>
        private static string StripCommentsAndStrings(string code)
        {
            if (string.IsNullOrEmpty(code))
                return string.Empty;

            var sb = new StringBuilder(code.Length);
            bool inSingleLineComment = false;
            bool inMultiLineComment = false;
            bool inSingleQuote = false;
            bool inDoubleQuote = false;
            bool inTemplate = false;
            bool escape = false;

            for (int i = 0; i < code.Length; i++)
            {
                char c = code[i];
                char next = i + 1 < code.Length ? code[i + 1] : '\0';

                // Inside single-line comment
                if (inSingleLineComment)
                {
                    if (c == '\n')
                    {
                        inSingleLineComment = false;
                        sb.Append(c); // keep newline
                    }
                    else
                    {
                        sb.Append(' ');
                    }
                    continue;
                }

                // Inside multi-line comment
                if (inMultiLineComment)
                {
                    if (c == '*' && next == '/')
                    {
                        inMultiLineComment = false;
                        sb.Append(' ');
                        i++; // skip '/'
                    }
                    else
                    {
                        sb.Append(c == '\n' ? '\n' : ' ');
                    }
                    continue;
                }

                // Inside string/template
                if (inSingleQuote || inDoubleQuote || inTemplate)
                {
                    sb.Append(c == '\n' ? '\n' : ' ');

                    if (escape)
                    {
                        escape = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escape = true;
                        continue;
                    }

                    if (inSingleQuote && c == '\'')
                    {
                        inSingleQuote = false;
                    }
                    else if (inDoubleQuote && c == '"')
                    {
                        inDoubleQuote = false;
                    }
                    else if (inTemplate && c == '`')
                    {
                        inTemplate = false;
                    }

                    continue;
                }

                // Not inside any comment or string

                // Start single-line comment
                if (c == '/' && next == '/')
                {
                    inSingleLineComment = true;
                    sb.Append(' ');
                    i++; // skip second '/'
                    continue;
                }

                // Start multi-line comment
                if (c == '/' && next == '*')
                {
                    inMultiLineComment = true;
                    sb.Append(' ');
                    i++; // skip '*'
                    continue;
                }

                // Start strings/templates
                if (c == '\'')
                {
                    inSingleQuote = true;
                    sb.Append(' ');
                    continue;
                }

                if (c == '"')
                {
                    inDoubleQuote = true;
                    sb.Append(' ');
                    continue;
                }

                if (c == '`')
                {
                    inTemplate = true;
                    sb.Append(' ');
                    continue;
                }

                sb.Append(c);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns the index of the first character that is not a valid identifier
        /// start/part (A-Z, a-z, 0-9, _, $). Used to detect a leading method name.
        /// </summary>
        private static int IndexOfFirstNonIdentifierChar(string text)
        {
            if (string.IsNullOrEmpty(text))
                return -1;

            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '$'))
                {
                    return i;
                }
                i++;
            }
            return i;
        }
    }

    public static class CSharpComplexityCalculator
    {
        public static int CalculateClassComplexity(string[] csLines)
        {
            var total = 0;
            var inMethod = false;
            var braceCount = 0;
            int methodStartIndex = -1;

            for (int i = 0; i < csLines.Length; i++)
            {
                var line = csLines[i].Trim();

                // Detect C# method start
                if (!inMethod && IsMethodSignature(line))
                {
                    inMethod = true;
                    methodStartIndex = i;

                    // The signature line might contain '{' (e.g. one-liner methods)
                    braceCount = CountBraces(csLines[i]);

                    // If method body is on same line and closes immediately, handle here
                    if (braceCount == 0)
                    {
                        total += CalculateCyclomaticComplexity(methodStartIndex, csLines);
                        inMethod = false;
                        methodStartIndex = -1;
                    }

                    continue;
                }

                if (inMethod)
                {
                    braceCount += CountBraces(csLines[i]);

                    if (braceCount == 0)
                    {
                        total += CalculateCyclomaticComplexity(methodStartIndex, csLines);
                        inMethod = false;
                        methodStartIndex = -1;
                    }
                }
            }

            return total;
        }

        // Very simple method signature matcher:
        //   - starts with an access modifier or 'static'/'async'
        //   - has '(' and ')' and ends with '{' or will have '{' on subsequent lines.
        private static bool IsMethodSignature(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            // ignore attributes like [Something]
            if (line.StartsWith("["))
                return false;

            if (!line.Contains("(") || !line.Contains(")"))
                return false;

            var modifiers = new[]
            {
            "public ", "private ", "protected ", "internal ",
            "static ", "async "
        };

            // crude, but works reasonably well for normal code
            return modifiers.Any(m => line.StartsWith(m, StringComparison.Ordinal));
        }

        private static int CountBraces(string line)
        {
            int open = line.Count(c => c == '{');
            int close = line.Count(c => c == '}');
            return open - close;
        }

        private static int CalculateCyclomaticComplexity(int methodStartIndex, string[] csLines)
        {
            var body = ExtractMethodBody(methodStartIndex, csLines);
            if (string.IsNullOrWhiteSpace(body))
                return 1; // base complexity

            var cleanedBody = StripComments(body);
            int decisions = CountDecisionPoints(cleanedBody);

            return 1 + decisions;
        }

        private static string ExtractMethodBody(int startIndex, string[] csLines)
        {
            int braceCount = CountBraces(csLines[startIndex]);
            int line = startIndex + 1;
            var bodyLines = new List<string>();

            // If the opening brace is on the next line, advance until we've seen it
            while (braceCount == 0 && line < csLines.Length)
            {
                braceCount += CountBraces(csLines[line]);
                bodyLines.Add(csLines[line]);
                line++;
            }

            while (braceCount > 0 && line < csLines.Length)
            {
                bodyLines.Add(csLines[line]);
                braceCount += CountBraces(csLines[line]);
                line++;
            }

            return string.Join(Environment.NewLine, bodyLines);
        }

        private static string StripComments(string code)
        {
            // Remove // line comments
            code = Regex.Replace(code, @"//.*", "");

            // Remove /* ... */ block comments (non-greedy)
            code = Regex.Replace(code, @"/\*.*?\*/", "", RegexOptions.Singleline);

            return code;
        }

        private static int CountDecisionPoints(string body)
        {
            int count = 0;

            // Normalize whitespace a bit
            var normalized = body;

            // if / else if
            count += Regex.Matches(normalized, @"(?<!else\s)\bif\s*\(").Count;  // only plain "if"
            count += Regex.Matches(normalized, @"\belse\s+if\s*\(").Count;       // explicit "else if"

            // loops
            count += Regex.Matches(normalized, @"\bfor\s*\(").Count;
            count += Regex.Matches(normalized, @"\bforeach\s*\(").Count;
            count += Regex.Matches(normalized, @"\bwhile\s*\(").Count;
            count += Regex.Matches(normalized, @"\bdo\b").Count; // do { ... } while (...)

            // switch / case
            // typical convention: each case adds one
            count += Regex.Matches(normalized, @"\bcase\b").Count;

            // catch
            count += Regex.Matches(normalized, @"\bcatch\s*\(").Count;

            // ternary operator ?:  (rough heuristic: ? followed by something and a :)
            // avoid catching generics or nullable types too aggressively.
            count += Regex.Matches(normalized, @"\?[^:;\n]+\:").Count;

            // LINQ (optional decision-like behavior)
            count += Regex.Matches(body, @"\.Where\s*\(").Count;
            count += Regex.Matches(body, @"\.Select\s*\(").Count;

            return count;
        }
    }
}
