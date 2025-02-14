﻿using System;
using System.Collections.Generic;
using TypeCobol.Compiler.Concurrency;
using TypeCobol.Compiler.Directives;
using TypeCobol.Compiler.File;
using TypeCobol.Compiler.Scanner;

namespace TypeCobol.Compiler.Text
{
    /// <summary>
    /// Line types defined in the Cobol reference format
    /// </summary>
    public enum CobolTextLineType
    {
        /// <summary>
        /// Blank indicator in column 7
        /// </summary>
        Source,
        /// <summary>
        /// 'D' or 'd' indicator in colunm 7
        /// </summary>
        Debug,
        /// <summary>
        /// '*' or '/' indicator in column 7
        /// </summary>
        Comment,
        /// <summary>
        /// '-' indicator in column 7
        /// </summary>
        Continuation,
        /// <summary>
        /// '%' indicator in column 7
        /// </summary>
        MultiFormalizedComment,
        /// <summary>
        /// A blank line contains nothing but spaces in column 7 through column 72
        /// </summary>
        Blank,
        /// <summary>
        /// Any other indicator char is invalid
        /// </summary>
        Invalid
    }

    /// <summary>
    /// Partition of a COBOL text line into reference format areas
    /// </summary>
    public class CobolTextLine : ICobolTextLine
    {
        public CobolTextLine(ITextLine textLine, ColumnsLayout columnsLayout)
        {
            // Reuse the external text line object
            this.textLine = textLine;
            LineIndex = textLine.LineIndex;
            ColumnsLayout = columnsLayout;

            // Scan the line to find the indexes of the different areas
            // - 72 columns reference format
            if (columnsLayout == ColumnsLayout.CobolReferenceFormat)
            {
                MapVariableLengthLineWithReferenceFormat();
            }
            // - free format and unlimited line length
            else
            {
                MapVariableLengthLineWithFreeFormat();
            }

            // Study the indicator char to determine the type of the line
            ComputeLineTypeFromIndicator();

            // First text analysis phase of the incremental compilation process
            CompilationStep = CompilationStep.Text;
        }

        /// <summary>
        /// Create an isolated CobolTextLine, not based on a real line of a TextDocument.
        /// Useful only for unit tests, or to compute intermediate results.
        /// </summary>
        public static CobolTextLine Create(string text)
        {
            ITextLine isolatedTextLine = new TextLineSnapshot(-1, text, null);
            return new CobolTextLine(isolatedTextLine, ColumnsLayout.FreeTextFormat);
        }

        public static ICollection<ITextLine> Create(string text, ColumnsLayout layout, TypeCobolOptions scannerOptions, int index = -1)
        {
            if (layout == ColumnsLayout.FreeTextFormat)
            {
                var result = new List<ITextLine>();
                result.Add(new TextLineSnapshot(index, text, null));
                return result;
            }
            if (layout == ColumnsLayout.CobolReferenceFormat)
            {
                char indicator = ' ';
                string indent = "";
                bool wasComment = text.Trim().StartsWith("*");
                if (wasComment)
                {
                    indicator = '*';
                    int i = text.IndexOf('*');
                    indent = text.Substring(0, i);
                    text = text.Substring(i + 1);
                }
                return CreateCobolLines(layout, scannerOptions, index, indicator, indent, text);
            }
            throw new System.NotImplementedException("Unsuported ITextLine type: " + layout);
        }

        public static ICollection<ITextLine> CreateCobolLines(ColumnsLayout layout, TypeCobolOptions scannerOptions, int index, char indicator, string indent, string text)
        {
            return CreateCobolLines(layout, scannerOptions, index, indicator, indent, text, 65, 61, true);
        }

        public static ICollection<ITextLine> CreateCobolLines(ColumnsLayout layout, TypeCobolOptions scannerOptions, int index, char indicator, string indent, string text, int pmax, int pmin, bool bConvertFirstLine)
        {
            var result = new List<ITextLine>();
            foreach (var part in text.Split(new[] { Environment.NewLine, "\n", "\r" }, StringSplitOptions.None))
            {
                int max = pmax;
                int min = pmax;
                int nLine = (part.Length / max) + ((part.Length % max) != 0 ? 1 : 0);
                if (nLine == 1)
                {
                    result.Add(new TextLineSnapshot(index, bConvertFirstLine ? Convert(layout, part, indicator, indent, pmax) : part, null));
                }
                else
                {
                    if (indicator == ' ')
                    {
                        max = pmax;
                        min = pmin;
                        indicator = '-';
                    }

                    IList<Tuple<string, bool>> lines = Split(part, max, min, scannerOptions);

                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (index > -1) index++;
                        result.Add(new TextLineSnapshot(index, (i == 0 && !bConvertFirstLine) ? lines[i].Item1 : Convert(layout, lines[i].Item1,
                            indicator != '-'
                                ? indicator
                                : ((lines[i].Item2 && i != 0) ? indicator : (i == 0 ? NoIndicator : NoOneIndicator)), indent, pmax), null));
                    }
                }
            }
            return result;
        }

        private const int NoIndicator = 0;
        private const int NoOneIndicator = -1;
        private const string ContinuationLinePrefix = "      -    ";
        private const string NoContinuationLinePrefix = "       ";
        private const string NoOneContinuationLinePrefix = "           ";
        private static string Convert(ColumnsLayout layout, string text, int indicator, string indent, int pmax)
        {
            string result = "";
            if (layout == ColumnsLayout.FreeTextFormat)
            {
                result = (indicator == '*' ? "*" : "") + indent + text;
            }
            else {
                var end = text.Length < pmax ? new string(' ', pmax - text.Length) : "";
                if (indicator != '-') {
                    if (indicator == NoIndicator)
                        result = NoContinuationLinePrefix + indent + text + end + "      ";
                    else if (indicator == NoOneIndicator)
                        result = NoOneContinuationLinePrefix + indent + text + end + "      ";
                    else
                        result = "      " + (char)indicator + indent + text + end + "      ";
                } else {
                    result = ContinuationLinePrefix + indent + text + end + "      ";
                }
            }
            return result;
        }

        private static IList<Tuple<string, bool> > Split(string line, int max, int min, TypeCobolOptions scannerOptions)
        {
            var lines = new List<Tuple<string, bool>>();
            int nLine = (line.Length / max) + ((line.Length % max) != 0 ? 1 : 0);
            if (nLine == 1)
            {
                lines.Add(new Tuple<string, bool>(line, false));
                return lines;
            }
            if (line.Length < 1) lines.Add(new Tuple<string, bool>(line, false));
            else {
                for (int i = 0; i < line.Length; i += (i == 0 ? max : min))
                {
                    lines.Add(new Tuple<string, bool>(line.Substring(i, Math.Min((i == 0 ? max : min), line.Length - i)), true));
                }
            }
            TokensLine tempTokensLine = TokensLine.CreateVirtualLineForInsertedToken(0, line);
            tempTokensLine.InitializeScanState(new MultilineScanState(IBMCodePages.GetDotNetEncodingFromIBMCCSID(1147)));

            Scanner.Scanner scanner = new Scanner.Scanner(line, 0, line.Length - 1, tempTokensLine, scannerOptions, false);
            Token t = null;
            int nCurLength = 0;
            int nSpan = max;
            int index = 0;
            bool bNextNoIndicator = false;
            while ((t = scanner.GetNextToken()) != null)
            {
                nCurLength += t.Length;
                if (nCurLength >= nSpan)
                {
                    if (t.TokenFamily == TokenFamily.Whitespace || (nCurLength == nSpan))
                        bNextNoIndicator = true;
                    else if (t.TokenFamily == TokenFamily.AlphanumericLiteral && t.TokenType == TokenType.AlphanumericLiteral)
                    {
                        if (t.SourceText.IndexOf('\'') == 0 || t.SourceText.IndexOf('"') == 0)
                        {//The next continuation line must have a '\''
                            if (lines.Count > (index + 1))
                            {
                                string item = lines[index + 1].Item1;
                                lines[index + 1] = new Tuple<string, bool>(t.SourceText[0] + item, lines[index + 1].Item2);
                            }
                        }
                    }
                    nSpan += min;
                    index++;
                }
                else if (bNextNoIndicator)
                {
                    lines[index] = new Tuple<string, bool>(lines[index].Item1, false);
                    bNextNoIndicator = false;
                }                
            }
            return lines;
        }

        // --- Cobol text line scanner

        private void MapVariableLengthLineWithReferenceFormat()
        {
            string line = textLine.Text;
            int lastIndexOfLine = line.Length - 1;

            // Test for free format compiler directives embedded in a reference format file
            int compilerDirectiveIndex = FindFirstCharOfCompilerDirectiveBeforeColumn8(line);
            if (compilerDirectiveIndex >= 0)
            {
                // Free text format line embedded in reference format file
                SequenceNumber = new TextArea(TextAreaType.SequenceNumber, 0, compilerDirectiveIndex - 1);
                Indicator = new TextArea(TextAreaType.Indicator, compilerDirectiveIndex, compilerDirectiveIndex - 1);
                Source = new TextArea(TextAreaType.Source, compilerDirectiveIndex, lastIndexOfLine > 71 ? 71 : lastIndexOfLine);
                Comment = new TextArea(TextAreaType.Comment, lastIndexOfLine > 71 ? 72 : lastIndexOfLine + 1, lastIndexOfLine);
            }
            else
            {
                // Cobol reference format
                if (lastIndexOfLine >= 7)
                {
                    SequenceNumber = new TextArea(TextAreaType.SequenceNumber, 0, 5);
                    Indicator = new TextArea(TextAreaType.Indicator, 6, 6);
                    Source = new TextArea(TextAreaType.Source, 7, lastIndexOfLine > 71 ? 71 : lastIndexOfLine);
                    Comment = new TextArea(TextAreaType.Comment, lastIndexOfLine > 71 ? 72 : lastIndexOfLine + 1, lastIndexOfLine);
                }
                else if (lastIndexOfLine == 6)
                {
                    SequenceNumber = new TextArea(TextAreaType.SequenceNumber, 0, 5);
                    Indicator = new TextArea(TextAreaType.Indicator, 6, 6);
                    Source = new TextArea(TextAreaType.Source, 7, 6);
                    Comment = new TextArea(TextAreaType.Comment, 7, 6);
                }
                else
                {
                    SequenceNumber = new TextArea(TextAreaType.SequenceNumber, 0, lastIndexOfLine);
                    Indicator = new TextArea(TextAreaType.Indicator, lastIndexOfLine + 1, lastIndexOfLine);
                    Source = new TextArea(TextAreaType.Source, lastIndexOfLine + 1, lastIndexOfLine);
                    Comment = new TextArea(TextAreaType.Comment, lastIndexOfLine + 1, lastIndexOfLine);
                }
            }
        }

        private void MapVariableLengthLineWithFreeFormat()
        {
            string line = textLine.Text;
            int lastIndexOfLine = line.Length - 1;

            // No SequenceNumber area in free format text 
            SequenceNumber = new TextArea(TextAreaType.SequenceNumber, 0, -1);

            // In free format source text :
            // - a line starting with char * is a comment line or a compiler directive            
            if (lastIndexOfLine >= 0 && line[0] == '*')
            {
                // Check for compiler directives
                if ((line.Length >= 5 && (line.StartsWith("*CBL ") || line.StartsWith("*CBL,"))) ||
                    (line.Length >= 9 && (line.StartsWith("*CONTROL ") || line.StartsWith("*CONTROL,"))))
                {
                    Indicator = new TextArea(TextAreaType.Indicator, 0, -1);
                    Source = new TextArea(TextAreaType.Source, 0, lastIndexOfLine);
                }
                else
                {
                    Indicator = new TextArea(TextAreaType.Indicator, 0, 0);
                    Source = new TextArea(TextAreaType.Source, 1, lastIndexOfLine);
                }
            }
            // - a line starting with char / is a comment line 
            // - a line starting with char - is a continuation line
            // => a free format program line cannot start with one of these three chars, insert a space before if needed
            else if (lastIndexOfLine >= 0 && (line[0] == '/' || line[0] == '-'))
            {
                Indicator = new TextArea(TextAreaType.Indicator, 0, 0);
                Source = new TextArea(TextAreaType.Source, 1, lastIndexOfLine);
            }
            // - a line starting with d or D + space char is a debug ligne
            else if (lastIndexOfLine >= 1 && ((line[0] == 'd' || line[0] == 'D') & line[1] == ' '))
            {
                Indicator = new TextArea(TextAreaType.Indicator, 0, 1);
                Source = new TextArea(TextAreaType.Source, 2, lastIndexOfLine);
            }
            else // no indicator
            {
                Indicator = new TextArea(TextAreaType.Indicator, 0, -1);
                Source = new TextArea(TextAreaType.Source, 0, lastIndexOfLine);
            }

            // No Comment area in free format text
            Comment = new TextArea(TextAreaType.Comment, lastIndexOfLine + 1, lastIndexOfLine);
        }

        // List of compiler directives keywords which can be encountered before column 8
        // even in Cobol reference format, and which force a certain form a free format
        // text lines in otherwise reference format files
        private static readonly string[] COMPILER_DIRECTIVE_KEYWORDS_STARTING_BEFORE_COLUMN_8 = new string[] {
            "CBL",
            "PROCESS",
            "*CBL",
            "*CONTROL",
            "BASIS",
            "DELETE",
            "INSERT",
        };

        private static int FindFirstCharOfCompilerDirectiveBeforeColumn8(string line)
        {
            bool compilerDirectiveFound = false;

            // We are interested in compiler directives starting before column 8
            int endOfSearchIndex = line.Length >= 8 ? 6 : (line.Length - 1);

            // Scan the first seven chars of the line to find a compiler directive start
            int firstCharIndex;
            for (firstCharIndex = 0; (firstCharIndex <= endOfSearchIndex && !compilerDirectiveFound); firstCharIndex++)
            {
                char firstChar = line[firstCharIndex];

                // If the char is numeric or space (regular sequence number), abort the search immediately
                if (Char.IsDigit(firstChar) || firstChar == ' ') continue;

                // Try to match each one of the potential compiler directives in turn
                for (int compilerDirectiveIndex = 0; (compilerDirectiveIndex < COMPILER_DIRECTIVE_KEYWORDS_STARTING_BEFORE_COLUMN_8.Length && !compilerDirectiveFound); compilerDirectiveIndex++)
                {
                    string compilerDirectiveKeyword = COMPILER_DIRECTIVE_KEYWORDS_STARTING_BEFORE_COLUMN_8[compilerDirectiveIndex];

                    // Try to match the first character of a compiler directive
                    if (Char.ToUpper(firstChar) == compilerDirectiveKeyword[0])
                    {
                        // Check to see if the line is long enough to hold the compiler directive
                        if (line.Length >= firstCharIndex + compilerDirectiveKeyword.Length)
                        {
                            int afterLastCharIndex = firstCharIndex + compilerDirectiveKeyword.Length;

                            // Try to match the following characters
                            int nextCharIndex = firstCharIndex + 1;
                            int keywordCharIndex = 1;
                            for (; nextCharIndex < afterLastCharIndex; nextCharIndex++, keywordCharIndex++)
                            {
                                if (Char.ToUpper(line[nextCharIndex]) != compilerDirectiveKeyword[keywordCharIndex])
                                {
                                    // Exit the loop as soon as a char does not match
                                    break;
                                }
                            }
                            // Complete keyword matched => exit both loops
                            if (nextCharIndex == afterLastCharIndex)
                            {
                                if (nextCharIndex == line.Length || line[nextCharIndex] == ' ' || line[nextCharIndex] == ',')
                                {
                                    compilerDirectiveFound = true;
                                }
                            }
                        }
                    }
                }
            }

            if (compilerDirectiveFound)
            {
                return firstCharIndex - 1;
            }
            else
            {
                return -1;
            }
        }

        private void ComputeLineTypeFromIndicator()
        {
            // Compute line type
            switch (IndicatorChar)
            {
                case ' ':
                    Type = CobolTextLineType.Source;
                    break;
                case '*':
                case '/':
                    Type = CobolTextLineType.Comment;
                    break;
                case 'D':
                case 'd':
                    Type = CobolTextLineType.Debug;
                    break;
                case '-':
                    Type = CobolTextLineType.Continuation;
                    break;
                case '%':
                    Type = CobolTextLineType.MultiFormalizedComment;
                    break;
                default:
                    Type = CobolTextLineType.Invalid;
                    break;
            }

            if (Type == CobolTextLineType.Source || Type == CobolTextLineType.Debug || Type == CobolTextLineType.Continuation)
            {
                // Detect blank lines
                if (Source.IsEmpty || string.IsNullOrWhiteSpace(SourceText))
                {
                    Type = CobolTextLineType.Blank;
                }
            }
        }

        // --- Cobol text line properties ---

        /// <summary>
        /// Format of the Cobol text line
        /// </summary>
        public ColumnsLayout ColumnsLayout { get; private set; }

        /// <summary>
        /// Cobol text line type : Source, Debug, Comment or Continuation
        /// </summary>
        public CobolTextLineType Type { get; private set; }

        /// <summary>
        /// Sequence number area : Columns 1 through 6
        /// </summary>
        public TextArea SequenceNumber { get; private set; }

        /// <summary>
        /// Sequence number text : Columns 1 through 6
        /// </summary>
        public string SequenceNumberText
        {
            get
            {
                return SequenceNumber.IsEmpty ? null : textLine.TextSegment(SequenceNumber.StartIndex, SequenceNumber.EndIndex);
            }
        }

        /// <summary>
        /// Indicator area : Column 7
        /// </summary>
        public TextArea Indicator { get; private set; }

        /// <summary>
        /// Indicator char : Column 7
        /// </summary>
        public char IndicatorChar
        {
            get
            {
                return Indicator.IsEmpty ? ' ' : textLine.TextSegment(Indicator.StartIndex, Indicator.EndIndex)[0];
            }
        }

        /// <summary>
        /// Area A : Columns 8 through 11 
        /// Area B : Columns 12 through 72 
        /// </summary>
        public TextArea Source { get; private set; }

        public string SourceText
        {
            get
            {
                return Source.IsEmpty ? null : textLine.TextSegment(Source.StartIndex, Source.EndIndex);
            }
        }

        /// <summary>
        /// Comment area : Columns 73 through 80+
        /// </summary>
        public TextArea Comment { get; private set; }

        /// <summary>
        /// Comment text : Columns 73 through 80+
        /// </summary>
        public string CommentText
        {
            get
            {
                return Comment.IsEmpty ? null : textLine.TextSegment(Comment.StartIndex, Comment.EndIndex);
            }
        }

        public override string ToString()
        {
            return "SequenceNumber" + SequenceNumber + " Indicator" + Indicator + " Source" + Source + " Comment" + Comment;
        }

        // --- ITextLine wrapper ---

        // Underlying text line
        protected ITextLine textLine;

        /// <summary>
        /// Text of the line, without the end of line delimiters
        /// </summary>
        public string Text { get { return textLine.Text; } }

        /// <summary>
        /// Part of the text of the line, from start index to end index (included)
        /// </summary>
        public string TextSegment(int startIndex, int endIndexInclusive) { return textLine.TextSegment(startIndex, endIndexInclusive); }

        /// <summary>
        /// Number of characters in the line, end of line delimiters excluded
        /// </summary>
        public int Length { get { return textLine.Length; } }

        /// <summary>
        /// True if the implementation of the text line is read-only and can be used as a snapshot
        /// </summary>
        public bool IsReadOnly { get { return textLine.IsReadOnly; } }

        /// <summary>
        /// Index of this line when it first appeared in the document.
        /// WARNING : if lines are later inserted or removed in the document before it,
        /// LineIndex no longer reflects the current position of the line.
        /// It can however provide a good starting point to start searching for a line
        /// in a snapshot of the document at a given point in time.
        /// When a line is created outside of a document, LineIndex = -1.
        /// </summary>
        public int LineIndex { get; protected set; }

        /// <summary>
        /// A text line instance can be reused simultaneously in different snapshots of the document
        /// (if it wasn't modified between two versions).
        /// You can NOT get a line number from an isolated text line, because this line instance can
        /// have different positions in two different snapshots of the document (if other lines were 
        /// inserted or removed before).
        /// 
        /// The line number is only defined :
        /// 
        /// 1. In a specific snapshot of the document :
        /// - pass the ITextLine object to the IndexOf method on the list of lines in a document snapshot 
        ///   (WARNING : expensive O(n) operation !)
        /// 
        /// 2. In the live text document (for example a text editor accessed in the specific thread where it lives) :
        /// - pass the property LineTrackingReferenceInSourceDocument to a dedicated method of the text source 
        ///   (much less expensive O(log n) operation)
        /// 
        /// This property returns an opaque reference to a line tracking object from the live text document,
        /// which will enable an efficient retrieval of the line number for this line in the document.
        /// </summary>
        public object LineTrackingReferenceInSourceDocument { get { return textLine.LineTrackingReferenceInSourceDocument; } }

        // --- Incremental compilation process ---

        /// <summary>
        /// Indicates which compiler step last updated the properties of this line
        /// </summary>
        public CompilationStep CompilationStep { get; set; }

        /// <summary>
        /// A line is freezed after the completion of each compiler step to enable reliable snapshots.
        /// If we need to update the properties of the line later, a new line must be allocated.
        /// This method returns true if the line can be updated in place, false if a new copy of the line must be allocated.
        /// </summary>
        public bool CanStillBeUpdatedBy(CompilationStep updatingStep)
        {
            if (CompilationStep >= updatingStep)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }
}
