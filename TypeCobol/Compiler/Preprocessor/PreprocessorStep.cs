﻿using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using TypeCobol.Compiler.AntlrUtils;
using TypeCobol.Compiler.Concurrency;
using TypeCobol.Compiler.CupCommon;
using TypeCobol.Compiler.Diagnostics;
using TypeCobol.Compiler.Directives;
using TypeCobol.Compiler.Parser;
using TypeCobol.Compiler.Scanner;

namespace TypeCobol.Compiler.Preprocessor
{
    /// <summary>
    /// Incrementally parse and execute compiler directives from a set of token changes
    /// </summary>
    static class PreprocessorStep
    {
        /// <summary>
        /// Initial preprocessing of a complete document
        /// </summary>
        internal static void ProcessDocument(CompilationDocument document, ISearchableReadOnlyList<ProcessedTokensLine> documentLines, IDocumentImporter documentImporter, PerfStatsForParserInvocation perfStatsForParserInvocation, out List<string> missingCopies)
        {
            ProcessChanges(document, documentLines, null, null, documentImporter, perfStatsForParserInvocation, out missingCopies);
        }

        /// <summary>
        /// Incremental preprocessing
        /// </summary>
        internal static IList<DocumentChange<IProcessedTokensLine>> ProcessChanges(CompilationDocument document,
            ISearchableReadOnlyList<ProcessedTokensLine> documentLines,
            IList<DocumentChange<ITokensLine>> tokensLinesChanges,
            PrepareDocumentLineForUpdate prepareDocumentLineForUpdate, IDocumentImporter documentImporter,
            PerfStatsForParserInvocation perfStatsForParserInvocation, out List<string> missingCopies)
        {
            return CupProcessTokensLinesChanges(document, documentLines, tokensLinesChanges,
                    prepareDocumentLineForUpdate,
                    documentImporter, perfStatsForParserInvocation, out missingCopies);
        }

        private static void ReportMissingCopy(CopyDirective copyDirective, ProcessedTokensLine tokensLineWithCopyDirective, Exception exception)
        {
            Token targetToken = tokensLineWithCopyDirective
                .TokensWithCompilerDirectives
                .First(token => token.TokenType == TokenType.COPY_IMPORT_DIRECTIVE && ((CompilerDirectiveToken) token).CompilerDirective == copyDirective);

            var diagnostic = new Diagnostic(MessageCode.FailedToLoadTextDocumentReferencedByCopyDirective, targetToken.Position(), exception.Message, exception);
            tokensLineWithCopyDirective.AddDiagnostic(diagnostic);
        }

        /// <summary>
        /// Incremental preprocessing of a set of tokens lines changes
        /// </summary>
        private static IList<DocumentChange<IProcessedTokensLine>> CupProcessTokensLinesChanges(
            CompilationDocument document, ISearchableReadOnlyList<ProcessedTokensLine> documentLines,
            IList<DocumentChange<ITokensLine>> tokensLinesChanges,
            PrepareDocumentLineForUpdate prepareDocumentLineForUpdate,
            IDocumentImporter documentImporter,
            PerfStatsForParserInvocation perfStatsForParserInvocation, out List<string> missingCopies)
        {
            var textSourceInfo = document.TextSourceInfo;

            // Collect all changes applied to the processed tokens lines during the incremental scan
            IList<DocumentChange<IProcessedTokensLine>> processedTokensLinesChanges = new List<DocumentChange<IProcessedTokensLine>>();

            // There are 2 reasons to a preprocess a tokens line after a change :
            // 1. A tokens line changed : these lines were already reset during the previous steps
            // 2. If a tokens line that changed was involved in the parsing of a multiline compiler directive, the whole group of lines must be parsed again
            // Then, if a new COPY directive was parsed : the CompilationDocument to include must be prepared

            // --- PREPARATION PHASE : reset all processed tokens lines which were involved in a multiline compiler directive where at least one line changed  ---

            // Iterate over all tokens changes detected by the ScannerStep :
            // refresh all the adjacent lines participating in a ContinuationTokensGroup
            if (tokensLinesChanges != null)
            {
                int lastLineIndexReset = -1;
                foreach (DocumentChange<ITokensLine> tokensChange in tokensLinesChanges)
                {
                    processedTokensLinesChanges.Add(new DocumentChange<IProcessedTokensLine>(tokensChange.Type, tokensChange.LineIndex, (IProcessedTokensLine)tokensChange.NewLine));
                    if (tokensChange.LineIndex > lastLineIndexReset)
                    {
                        lastLineIndexReset = CheckIfAdjacentLinesNeedRefresh(tokensChange.Type, tokensChange.LineIndex, documentLines, prepareDocumentLineForUpdate, processedTokensLinesChanges, lastLineIndexReset);
                    }
                }
            }

            // --- COMPILER DIRECTIVES PHASE : Find and parse all compiler directives ---

            // Init. Prepare a compiler directive parser

            // Create a Cup Cobol Words token iterator on top of tokens lines
            CobolWordsTokenizer tokensIterator = new CobolWordsTokenizer(
                textSourceInfo.Name,
                documentLines,
                null,
                Token.CHANNEL_SourceTokens);

            // Init a CUP compiler directive parser
            CupCobolErrorStrategy cupCobolErrorStrategy = new CupPreprocessor.CompilerDirectiveErrorStrategy();

            // Prepare to analyze the parse tree            
            TypeCobol.Compiler.CupPreprocessor.CompilerDirectiveBuilder directiveBuilder = new TypeCobol.Compiler.CupPreprocessor.CompilerDirectiveBuilder(document);

            // 1. Iterate over all compiler directive starting tokens found in the lines which were updated 
            foreach (Token compilerDirectiveStartingToken in documentLines
                .Where(line => line.PreprocessingState == ProcessedTokensLine.PreprocessorState.NeedsCompilerDirectiveParsing)
                .SelectMany(line => line.SourceTokens)
                .Where(token => token.TokenFamily == TokenFamily.CompilerDirectiveStartingKeyword))
            {
                // 2. Reset the compiler directive parser state

                // Reset tokens iterator position before parsing
                // -> seek just before the compiler directive starting token
                tokensIterator.SeekToToken(compilerDirectiveStartingToken);
                tokensIterator.PreviousToken();
                // Special case : for efficiency reasons, in EXEC SQL INCLUDE directives
                // only the third token INCLUDE is recognized as a compiler directive
                // starting keyword by the scanner. In this case, we must rewind the 
                // iterator two tokens backwards to start parsing just before the EXEC token.
                if (compilerDirectiveStartingToken.TokenType == TokenType.EXEC_SQL)
                {
                    tokensIterator.PreviousToken();
                    tokensIterator.PreviousToken();
                }

                // Reset Cup Tokenizer
                tokensIterator.Reset(false);

                // Reset parsing error diagnostics            
                cupCobolErrorStrategy.Diagnostics = null;

                // 3. Try to parse a compiler directive starting with the current token
                perfStatsForParserInvocation.OnStartParsing();
                TypeCobol.Compiler.CupPreprocessor.CobolCompilerDirectivesParser directivesParser =
                    new TypeCobol.Compiler.CupPreprocessor.CobolCompilerDirectivesParser(tokensIterator);
                directivesParser.ErrorReporter = cupCobolErrorStrategy;
                directivesParser.Builder = directiveBuilder;
                TUVienna.CS_CUP.Runtime.Symbol ppSymbol = directivesParser.parse();
                perfStatsForParserInvocation.OnStopParsing(0, 0);

#if EUROINFO_RULES
                if (document.CompilerOptions.ReportUsedCopyNamesPath != null && document.CompilerOptions.ExecToStep <= ExecutionStep.Preprocessor)
                {
                    //HACK ! Skipping the rest of preprocessing to gain speed.
                    //However the resulting document is broken so this is designed for users only interested in copy extraction
                    continue;
                }
#endif

                perfStatsForParserInvocation.OnStartTreeBuilding();
                // 4. Visit the parse tree to build a first class object representing the compiler directive
                CompilerDirective compilerDirective = directiveBuilder.CompilerDirective;
                bool errorFoundWhileParsingDirective = compilerDirective == null || compilerDirective.Diagnostics != null || cupCobolErrorStrategy.Diagnostics != null;

                // 5. Get all tokens consumed while parsing the compiler directive
                //    and partition them line by line 
                Token startToken = tokensIterator.FirstToken;
                Token stopToken = tokensIterator.LastToken;
                if (stopToken == null) stopToken = startToken;
                MultilineTokensGroupSelection tokensSelection = tokensIterator.SelectAllTokensBetween(startToken, stopToken);

                if (compilerDirective != null)
                {
                    // 5. a Set consumed tokens of the compiler directive
                    compilerDirective.ConsumedTokens = tokensSelection;

                    // 6. Replace all matched tokens by :
                    // - a CompilerDirectiveToken on the first line
                    ProcessedTokensLine firstProcessedTokensLine = documentLines[tokensSelection.FirstLineIndex];
                    if (tokensSelection.SelectedTokensOnSeveralLines.Length == 1)
                    {
                        firstProcessedTokensLine.InsertCompilerDirectiveTokenOnFirstLine(
                            tokensSelection.TokensOnFirstLineBeforeStartToken,
                            compilerDirective, errorFoundWhileParsingDirective,
                            tokensSelection.SelectedTokensOnSeveralLines[0],
                            tokensSelection.TokensOnLastLineAfterStopToken, false);
                    }
                    else
                    {
                        TokensGroup continuedTokensGroup = firstProcessedTokensLine.InsertCompilerDirectiveTokenOnFirstLine(
                            tokensSelection.TokensOnFirstLineBeforeStartToken,
                            compilerDirective, errorFoundWhileParsingDirective,
                            tokensSelection.SelectedTokensOnSeveralLines[0],
                            null, true);

                        // - a ContinuationTokensGroup on the following lines
                        int selectionLineIndex = 1;
                        int lastLineIndex = tokensSelection.FirstLineIndex + tokensSelection.SelectedTokensOnSeveralLines.Length - 1;
                        for (int nextLineIndex = tokensSelection.FirstLineIndex + 1; nextLineIndex <= lastLineIndex; nextLineIndex++, selectionLineIndex++)
                        {
                            IList<Token> compilerDirectiveTokensOnNextLine = tokensSelection.SelectedTokensOnSeveralLines[selectionLineIndex];
                            if (compilerDirectiveTokensOnNextLine.Count > 0)
                            {
                                ProcessedTokensLine nextProcessedTokensLine = documentLines[nextLineIndex];
                                if (nextLineIndex != lastLineIndex)
                                {
                                    continuedTokensGroup = nextProcessedTokensLine.InsertCompilerDirectiveTokenOnNextLine(
                                        continuedTokensGroup,
                                        compilerDirectiveTokensOnNextLine,
                                        null, true);
                                }
                                else
                                {
                                    continuedTokensGroup = nextProcessedTokensLine.InsertCompilerDirectiveTokenOnNextLine(
                                        continuedTokensGroup,
                                        compilerDirectiveTokensOnNextLine,
                                        tokensSelection.TokensOnLastLineAfterStopToken, false);
                                }
                            }
                        }
                    }
                }

                // 7. Register compiler directive parse errors
                if (errorFoundWhileParsingDirective)
                {
                    ProcessedTokensLine compilerDirectiveLine = documentLines[tokensSelection.FirstLineIndex];
                    if (compilerDirective != null && compilerDirective.Diagnostics != null)
                    {
                        foreach (Diagnostic directiveDiag in compilerDirective.Diagnostics)
                        {
                            compilerDirectiveLine.AddDiagnostic(directiveDiag);
                        }
                    }
                    else if (cupCobolErrorStrategy.Diagnostics != null)
                    {
                        foreach (Diagnostic directiveDiag in cupCobolErrorStrategy.Diagnostics)
                        {
                            if (compilerDirective != null)
                            {
                                compilerDirective.AddDiagnostic(directiveDiag);
                            }
                            compilerDirectiveLine.AddDiagnostic(directiveDiag);
                        }
                    }
                }
            }

            // 8. Advance the state off all ProcessedTokensLines : 
            // NeedsCompilerDirectiveParsing => NeedsCopyDirectiveProcessing if it contains a COPY directive
            IList<ProcessedTokensLine> parsedLinesWithCopyDirectives = null;
            // NeedsCompilerDirectiveParsing => Ready otherwise
            foreach (ProcessedTokensLine parsedLine in documentLines
                .Where(line => line.PreprocessingState == ProcessedTokensLine.PreprocessorState.NeedsCompilerDirectiveParsing))
            {
                if (parsedLine.ImportedDocuments != null)
                {
                    if (parsedLinesWithCopyDirectives == null)
                    {
                        parsedLinesWithCopyDirectives = new List<ProcessedTokensLine>();
                    }
                    parsedLine.PreprocessingState = ProcessedTokensLine.PreprocessorState.NeedsCopyDirectiveProcessing;
                    parsedLinesWithCopyDirectives.Add(parsedLine);
                }
                else
                {
                    parsedLine.PreprocessingState = ProcessedTokensLine.PreprocessorState.Ready;
                }
            }

            perfStatsForParserInvocation.OnStopTreeBuilding();

            // --- COPY IMPORT PHASE : Process COPY (REPLACING) directives ---
            missingCopies = new List<string>();

            // 1. Iterate over all updated lines containing a new COPY directive
            if (parsedLinesWithCopyDirectives != null)
            {
                foreach (ProcessedTokensLine tokensLineWithCopyDirective in parsedLinesWithCopyDirectives)
                {
                    // Iterate over all COPY directives found on one updated line
                    foreach (
                        CopyDirective copyDirective in
                        tokensLineWithCopyDirective.ImportedDocuments.Keys.Where(
                            c => c.TextName != null || c.COPYToken.TokenType == TokenType.EXEC).ToArray())
                    {
                        if (copyDirective.TextName == null || tokensLineWithCopyDirective.ScanStateBeforeCOPYToken == null)
                            continue;
                        try
                        {
                            // Load (or retrieve in cache) the document referenced by the COPY directive
                            //Issue #315: tokensLineWithCopyDirective.ScanState must be passed because special names paragraph such as "Decimal point is comma" are declared in the enclosing program and can affect the parsing of COPY
                            CompilationDocument importedDocumentSource = documentImporter.Import(copyDirective.LibraryName, copyDirective.TextName,
                                    tokensLineWithCopyDirective.ScanStateBeforeCOPYToken[copyDirective.COPYToken],
                                    document.CopyTextNamesVariations, out var perfStats);

                            // Copy diagnostics from document
                            foreach (var diagnostic in importedDocumentSource.AllDiagnostics())
                            {
                                var position = new Diagnostic.Position(diagnostic.LineStart, diagnostic.ColumnStart, diagnostic.LineEnd, diagnostic.ColumnEnd, copyDirective);
                                var copyDiagnostic = diagnostic.CopyAt(position);
                                tokensLineWithCopyDirective.AddDiagnostic(copyDiagnostic);
                            }

                            // The copy itself has missing copies, add them into the list
                            foreach (var missingCopy in importedDocumentSource.MissingCopies)
                            {
                                missingCopies.Add(missingCopy);
                            }

                            // Store it on the current line after applying the REPLACING directive
                            ImportedTokensDocument importedDocument = new ImportedTokensDocument(copyDirective, importedDocumentSource.ProcessedTokensDocumentSnapshot, perfStats, document.CompilerOptions);
                            tokensLineWithCopyDirective.ImportedDocuments[copyDirective] = importedDocument;
                        }
                        catch (Exception e)
                        {
                            // The copy itself is missing
                            missingCopies.Add(copyDirective.TextName);

                            // Text name referenced by COPY directive was not found
                            // => register a preprocessor error on this line                            
                            ReportMissingCopy(copyDirective, tokensLineWithCopyDirective, e);
                        }
                    }

                    // Advance processing status of the line
                    tokensLineWithCopyDirective.PreprocessingState = ProcessedTokensLine.PreprocessorState.Ready;
                }
            }



            // --- REPLACE PHASE : REPLACE directives are implemented in ReplaceTokensLinesIterator ---

                /* Algorithm :
                 * 
                 * one REPLACE directive can express several replacement operations
                 * one replacement operation can be of several types (distinguished for optimization purposes)
                 * - SimpleTokenReplace : one source token / zero or one replacement token
                 * - PartialWordReplace : one pure partial word / zero or one replacement token
                 * - SimpleToMultipleTokenReplace : one source token / several replacement tokens
                 * - MultipleTokenReplace : one first + several following source tokens / zero to many replacement tokens
                 * 
                 * an iterator maintains a current set of replacement operations
                 * 
                 * if nextToken is replace directive
                 *    the current set of replacement operations is updated
                 * else 
                 *    nextToken is compared to each replacement operation in turn
                 *       if single -> single source token operation : return replacement token
                 *       if single -> multiple operation : setup a secondary iterator with the list of replacement tokens
                 *       if multiple -> multiple operation
                 *          snapshot of the underlying iterator
                 *          try to match all of the following source tokens
                 *          if failure : restore snapshot and try next operation
                 *          if success : setup a secondary iterator
                 * 
                 * token comparison sourceToken / replacementCandidate :
                 * 1. Compare Token type
                 * 2. If same token type and for families
                 *   AlphanumericLiteral
                 *   NumericLiteral
                 *   SyntaxLiteral
                 *   Symbol
                 *  => compare also Token text
                 *  
                 * PartialCobolWord replacement :
                 * p535 : The COPY statement with REPLACING phrase can be used to replace parts of words.
                 * By inserting a dummy operand delimited by colons into the program text, 
                 * the compiler will replace the dummy operand with the required text. 
                 * Example 3 shows how this is used with the dummy operand :TAG:. 
                 * The colons serve as separators and make TAG a stand-alone operand. 
                 */

                return processedTokensLinesChanges;
        }

        /// <summary>
        /// Illustration : lines with directives continued from previous line or with a continuation on next line (before update) are marked with a cross
        /// [     ]
        /// [    x]
        /// [x   x]
        /// [x    ]
        /// [     ]
        /// A DocumentChange intersects with a previously parsed multiline compiler directive if : 
        /// * LineInserted :
        ///   - the next line was a continuation
        /// * LineUpdated / LineRemoved :
        ///   - the previous line was continued
        ///   - the next line was a continuation
        /// When navigating to previous or next line searching for a continuation, we must ignore all fresh insertions / updates.
        /// We must then reset all processed tokens lines involved in a multiline compiler directive.
        /// </summary>
        private static int CheckIfAdjacentLinesNeedRefresh(DocumentChangeType changeType, int lineIndex, ISearchableReadOnlyList<ProcessedTokensLine> documentLines, PrepareDocumentLineForUpdate prepareDocumentLineForUpdate, IList<DocumentChange<IProcessedTokensLine>> processedTokensLinesChanges, int lastLineIndexReset)
        {
            // Navigate backwards to the start of the multiline compiler directive
            if (lineIndex > 0)
            {
                int previousLineIndex = lineIndex;
                IEnumerator<ProcessedTokensLine> reversedEnumerator = documentLines.GetEnumerator(previousLineIndex - 1, -1, true);
                while (reversedEnumerator.MoveNext() && (--previousLineIndex > lastLineIndexReset))
                {
                    // Get the previous line until a non continued line is encountered
                    ProcessedTokensLine previousLine = reversedEnumerator.Current;

                    // A reset line was already treated by the previous call to CheckIfAdjacentLinesNeedRefresh : stop searching
                    if (previousLine?.PreprocessingState == ProcessedTokensLine.PreprocessorState.NeedsCompilerDirectiveParsing)
                    {
                        break;
                    }
                    // Previous line is a continuation : reset this line and continue navigating backwards
                    // Previous line is not a continuation but is continued : reset this line and stop navigating backwards
                    else if (previousLine != null && (previousLine.HasDirectiveTokenContinuationFromPreviousLine || previousLine.HasDirectiveTokenContinuedOnNextLine))
                    {
                        lineIndex = previousLineIndex;
                        previousLine = (ProcessedTokensLine)prepareDocumentLineForUpdate(previousLineIndex, previousLine, CompilationStep.Preprocessor);
                        processedTokensLinesChanges.Add(new DocumentChange<IProcessedTokensLine>(DocumentChangeType.LineUpdated, previousLineIndex, previousLine));
                        if (!previousLine.HasDirectiveTokenContinuationFromPreviousLine)
                        {
                            break;
                        }
                    }
                    // Previous line not involved in a multiline compiler directive : stop searching
                    else
                    {
                        break;
                    }
                }
            }

            // Navigate forwards to the end of the multiline compiler directive 
            if (lineIndex < (documentLines.Count - 1))
            {
                int nextLineIndex = lineIndex;
                IEnumerator<ProcessedTokensLine> enumerator = documentLines.GetEnumerator(nextLineIndex + 1, -1, true);
                while (enumerator.MoveNext())
                {
                    // Get the next line until non continuation line is encountered
                    nextLineIndex++;
                    ProcessedTokensLine nextLine = enumerator.Current;

                    // A reset line will be treated by the next call to CheckIfAdjacentLinesNeedRefresh : stop searching
                    if (nextLine?.PreprocessingState == ProcessedTokensLine.PreprocessorState.NeedsCompilerDirectiveParsing)
                    {
                        break;
                    }
                    // Next line is a continuation and is continued: reset this line and continue navigating forwards
                    // Next line is a continuation but is not continued : reset this line and stop navigating forwards
                    else if (nextLine != null && nextLine.HasDirectiveTokenContinuationFromPreviousLine)
                    {
                        nextLine = (ProcessedTokensLine)prepareDocumentLineForUpdate(nextLineIndex, nextLine, CompilationStep.Preprocessor);
                        processedTokensLinesChanges.Add(new DocumentChange<IProcessedTokensLine>(DocumentChangeType.LineUpdated, nextLineIndex, nextLine));
                        lastLineIndexReset = nextLineIndex;
                        if (!nextLine.HasDirectiveTokenContinuedOnNextLine)
                        {
                            break;
                        }
                    }
                    // Previous line not involved in a multiline compiler directive : stop searching
                    else
                    {
                        break;
                    }
                }
            }

            return lastLineIndexReset > lineIndex ? lastLineIndexReset : lineIndex;
        }
    }
}
