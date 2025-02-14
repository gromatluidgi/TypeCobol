﻿using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypeCobol.Test.Compiler.Parser;
using TypeCobol.Test.Parser;
using TypeCobol.Test.Parser.FileFormat;
using TypeCobol.Test.Parser.Performance;
using TypeCobol.Test.Parser.Preprocessor;
using TypeCobol.Test.Parser.Scanner;
using TypeCobol.Test.Parser.Text;
using TypeCobol.Test.Utils;

namespace TypeCobol.Test {

    [TestClass]
    public class TestCollection
    {
        static readonly string root = PlatformUtils.GetPathForProjectFile("Parser" + Path.DirectorySeparatorChar + "Programs");

        [TestMethod]
        [TestProperty("Time", "fast")]
        public void CheckFileFormat()
        {
            TestIBMCodePages.Check_GetDotNetEncoding();
            TestIBMCodePages.Check_IsEBCDICCodePage();
            TestIBMCodePages.Check_DBCSCodePageNotSupported();

            TestCobolFile.Check_EBCDICCobolFile();
            TestCobolFile.Check_EBCDICCobolFileWithUnsupportedChar();
            TestCobolFile.Check_ASCIICobolFile_ReferenceFormat();
            TestCobolFile.Check_ASCIICobolFile_LinuxReferenceFormat();
            TestCobolFile.Check_ASCIICobolFile_FreeTextFormat();

            TestCobolFile.Check_UTF8File();
        }

        [TestMethod]
        [TestProperty("Time", "fast")]
        public void CheckText()
        {
            TestReadOnlyTextDocument.Check_DocumentFormatExceptions();

            TestReadOnlyTextDocument.Check_EmptyDocument();
            TestReadOnlyTextDocument.Check_ReferenceFormatDocument();
            TestReadOnlyTextDocument.Check_FreeFormatDocument();
        }

        [TestMethod]
        [TestProperty("Time", "fast")]
        public void CheckScanner()
        {
            //System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

            TestTokenTypes.CheckSeparators();
            TestTokenTypes.CheckComments();
            TestTokenTypes.CheckOperators();
            TestTokenTypes.CheckAlphanumericLiterals();
            TestTokenTypes.CheckPseudoText();
            TestTokenTypes.CheckPseudoText2();
            TestTokenTypes.CheckNumericLiterals();
            TestTokenTypes.CheckKeywordsAndUserDefinedWords();
            TestTokenTypes.CheckPartialCobolWords(true);//For Cobol
            TestTokenTypes.CheckPartialCobolWords(false);//For TypeCobol
            TestTokenTypes.CheckPictureCharacterString();
            TestTokenTypes.CheckCommentEntry();
            TestTokenTypes.CheckExecStatement();
            TestTokenTypes.CheckContextSensitiveKeywords();
            TestTokenTypes.CheckSymbolicCharacters();
            TestTokenTypes.CheckCblProcessCompilerDirective();

            TestContinuations.CheckSeparators();
            TestContinuations.CheckComments();
            TestContinuations.CheckOperators();
            TestContinuations.CheckAlphanumericLiterals();
            TestContinuations.CheckNumericLiterals();
            TestContinuations.CheckKeywordsAndUserDefinedWords();
            TestContinuations.CheckPictureCharacterString();
            TestContinuations.CheckCommentEntry();

            TestRealPrograms.CheckAllFilesForExceptions();
        }

        [TestMethod]
        [TestProperty("Time", "fast")]
        public void CheckPreprocessor()
        {
            TestCompilerDirectiveBuilder.CheckBASIS();
            TestCompilerDirectiveBuilder.CheckCBL_PROCESS();
            TestCompilerDirectiveBuilder.CheckASTERISK_CONTROL_CBL();
#if EUROINFO_RULES
            TestCompilerDirectiveBuilder.CheckCOPY(); //Because of the presence of remarks directive and non use of comprarator
#endif
            TestCompilerDirectiveBuilder.CheckDELETE();
            TestCompilerDirectiveBuilder.CheckEJECT();
            TestCompilerDirectiveBuilder.CheckENTER();
            TestCompilerDirectiveBuilder.CheckEXEC_SQL_INCLUDE();
            TestCompilerDirectiveBuilder.CheckINSERT();
            TestCompilerDirectiveBuilder.CheckREADY_RESET_TRACE();
            TestCompilerDirectiveBuilder.CheckREPLACE();
            TestCompilerDirectiveBuilder.CheckSERVICE_LABEL();
            TestCompilerDirectiveBuilder.CheckSERVICE_RELOAD();
            TestCompilerDirectiveBuilder.CheckSKIP1_2_3();
            TestCompilerDirectiveBuilder.CheckTITLE();
            TestCompilerDirectiveBuilder.CheckRealFiles();

            TestCopyDirective.CheckCopy();
            TestCopyDirective.CheckCopyReplacing();

            TestReplaceDirective.CheckReplaceSingle();
            TestReplaceDirective.CheckReplacePartial(true);
            TestReplaceDirective.CheckReplacePartial(false);
            TestReplaceDirective.CheckReplaceSingleToMultiple();
            TestReplaceDirective.CheckReplaceMultiple();
            TestReplaceDirective.CheckReplaceNested();
            TestReplaceDirective.CheckReplaceFunction();
            TestReplaceDirective.CheckEmptyPartialWordReplace();
            TestReplaceDirective.CheckEmptyPartialWordReplace2();
        }

        [TestMethod]
        [TestCategory("Parsing")]
        [TestProperty("Time", "fast")]
        public void CheckParserRobustness()
        {
            TestParserRobustness.CheckProgramCodeElements();
        }


        [TestMethod]
        [TestCategory("Parsing")]
        [TestProperty("Time", "fast")]
        public void CheckCodeElements() {
            // Test the recognition of potentially ambiguous CodeElements which begin with the same first Token
            TestCodeElements.Check_DISPLAYCodeElements();


            TestCodeElements.CheckCodeElements();
        }

        [TestMethod]
        [TestCategory("Parsing")]
        [TestProperty("Time", "fast")]
        public void CheckToken() {
            TestTokenSource.Check_CobolCharStream();
            TestTokenSource.Check_CobolTokenSource();
            TestTokenSource.Check_CobolTokenSource_WithStartToken();
        }

        /// <summary>
        /// Check Parser on Cobol85 source code
        /// </summary>
        /// <param name="cobol">true if it is really for pure Cobol85 language, rather than TypeCobol, false otherwise</param>
        private void CheckParserCobol85(bool cobol)
        {
            var errors = new System.Collections.Generic.List<Exception>();
            int nbOfTests = 0;
            string[] extensions = { ".cbl", ".pgm" };

            foreach (string directory in Directory.GetDirectories(root))
            {
                var dirname = Path.GetFileName(directory);

			    Console.WriteLine("Entering directory \"" + dirname + "\" [" + string.Join(", ", extensions) + "]:");
				var folderTester = new FolderTester(root, root, directory, extensions);
                try
                {
                    folderTester.Test(isCobolLanguage: cobol);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
                nbOfTests += folderTester.GetTestCount();
                Console.WriteLine();
            }

            Console.Write("Number of tests: " + nbOfTests + "\n");
            Assert.IsTrue(nbOfTests > 0, "No tests found");

            if (errors.Count > 0)
            {
                var str = new System.Text.StringBuilder();
                foreach (var ex in errors) str.Append(ex.Message);
                throw new Exception(str.ToString());
            }
        }

        [TestMethod]
        [TestCategory("Parsing")]
        [TestProperty("Time", "fast")]
        public void CheckParserCobol85()
        {
            CheckParserCobol85(true);
            CheckParserCobol85(false);
        }

        /// <summary>
        /// Check only files with *.tcbl extensions
        /// </summary>
        [TestMethod]
        [TestCategory("Parsing")]
        [TestProperty("Time", "fast")]
        public void CheckParserTcblPrograms()
        {
            int nbOfTests = 0;

            string[] extensions = { ".tcbl" };
            foreach (string directory in Directory.GetDirectories(root))
            {
                var dirname = Path.GetFileName(directory);

                Console.WriteLine("Entering directory \"" + dirname + "\" [" + string.Join(", ", extensions) + "]:");
                var folderTester = new FolderTester(root, root, directory, extensions);
                folderTester.Test();
                nbOfTests += folderTester.GetTestCount();
                Console.Write("\n");
            }
            Console.Write("Number of tests: " + nbOfTests + "\n");
            Assert.IsTrue(nbOfTests > 0, "No tests found");
        }

        /// <summary>
        /// Check only files with *.tcbl extensions
        /// </summary>
        [TestMethod]
        [TestCategory("Parsing")]
        [TestProperty("Time", "fast")]
        public void TCBLAutoReplaceSecurityTest()
        {
            TestUtils.compareLines(string.Empty, string.Empty, string.Empty, string.Empty);
        }


#if EUROINFO_RULES
        [TestMethod]
        [TestCategory("Parsing")]
        [TestProperty("Time", "fast")]
        public void EILegacyCheck()
        {
            string tempRoot = PlatformUtils.GetPathForProjectFile("Parser" + Path.DirectorySeparatorChar + "EILegacy");

            int nbOfTests = 0;
            string[] extensions = {".tcbl", ".cbl"};

            //Do not parse unsupported remarks, they are covered in a separate test
            var folderTester = new FolderTester(tempRoot, tempRoot, tempRoot, extensions, deep: false);

            folderTester.Test();

            nbOfTests += folderTester.GetTestCount();
            Console.Write("\n");

            Console.Write("Number of tests: " + nbOfTests + "\n");
            Assert.IsTrue(nbOfTests > 0, "No tests found");
        }

        [TestMethod]
        [TestCategory("Parsing")]
        [TestProperty("Time", "fast")]
        public void EILegacyUnsupportedRemarksCheck()
        {
            string tempRoot = PlatformUtils.GetPathForProjectFile("Parser" + Path.DirectorySeparatorChar + "EILegacy" + Path.DirectorySeparatorChar + "UnsupportedRemarks");

            int nbOfTests = 0;
            string[] extensions = { ".cbl" };

            var folderTester = new FolderTester(tempRoot, tempRoot, tempRoot, extensions);

            //In pure cobol mode, REMARKS directive is considered as regular comment so unsupported REMARKS formats won't break the parsing
            folderTester.Test(isCobolLanguage: true);

            nbOfTests += folderTester.GetTestCount();
            Console.Write("\n");

            Console.Write("Number of tests: " + nbOfTests + "\n");
            Assert.IsTrue(nbOfTests > 0, "No tests found");
        }
#endif

        /// <summary>
        /// Check for ExecToStep
        /// </summary>
        [TestMethod]
        //[Ignore]
        [TestCategory("Parsing")]
        [TestProperty("Time", "fast")]
        public void CheckExecToStep()
        {
            string fileName = "ExecToStepFile.rdz.cbl";

            var folder = "Parser" + Path.DirectorySeparatorChar + "ExecToStep";

            var compileResult = ParserUtils.ParseCobolFile(fileName, folder, execToStep: ExecutionStep.Scanner);
            //Verify that the option hasn't change during processing and that compilation process didn't go further than defined step
            if (compileResult.CompilerOptions.ExecToStep != ExecutionStep.Scanner 
                && compileResult.TokensLines.Count == 0 
                && compileResult.ProcessedTokensDocumentSnapshot != null 
                && compileResult.CodeElementsDocumentSnapshot != null 
                && compileResult.ProgramClassDocumentSnapshot.Root.Programs.Any())
                throw new Exception("Scanner Step failed");

            compileResult = ParserUtils.ParseCobolFile(fileName, folder, execToStep: ExecutionStep.Preprocessor);
            if (compileResult.CompilerOptions.ExecToStep != ExecutionStep.Preprocessor 
                && compileResult.ProcessedTokensDocumentSnapshot == null 
                && compileResult.CodeElementsDocumentSnapshot != null 
                && compileResult.ProgramClassDocumentSnapshot.Root.Programs.Any())
                throw new Exception("Preprocessor Step failed");

            compileResult = ParserUtils.ParseCobolFile(fileName, folder, execToStep: ExecutionStep.SyntaxCheck);
            if (compileResult.CompilerOptions.ExecToStep != ExecutionStep.SyntaxCheck 
                && compileResult.CodeElementsDocumentSnapshot == null 
                && compileResult.ProgramClassDocumentSnapshot.Root.Programs.Any())
                throw new Exception("SyntaxCheck Step failed");

            compileResult = ParserUtils.ParseCobolFile(fileName, folder, execToStep: ExecutionStep.SemanticCheck);
            if (compileResult.CompilerOptions.ExecToStep != ExecutionStep.SemanticCheck 
                && !compileResult.ProgramClassDocumentSnapshot.Root.Programs.Any())
                throw new Exception("SemanticCheck Step failed");
        }


        /// <summary>
        /// Check for Documentation generation
        /// </summary>
        [TestMethod]
        //[Ignore]
        [TestCategory("Parsing")]
        [TestProperty("Time", "fast")]
        public void CheckDocumentation()
        {
            int nbOfTests = 0;

            string[] extensions = { ".tcbl" };
            var directory =
                PlatformUtils.GetPathForProjectFile("Parser" + Path.DirectorySeparatorChar + "Documentation");

            var folderTester = new FolderTester(directory, directory, directory, extensions);
            folderTester.Test();
            nbOfTests += folderTester.GetTestCount();
            Console.Write("Number of tests: " + nbOfTests + "\n");
            Assert.IsTrue(nbOfTests > 0, "No tests found");
        }


        /// <summary>
        /// Direct copy parsing tests. Checks all ".cpy" files from "Parser\Copies".
        /// </summary>
        [TestMethod]
        [TestCategory("Parsing")]
        [TestProperty("Time", "fast")]
        public void CheckCopies()
        {
            int nbOfTests = 0;

            string[] extensions = { ".cpy" };
            var directory = PlatformUtils.GetPathForProjectFile("Parser" + Path.DirectorySeparatorChar + "Copies");
            var dirname = Path.GetFileName(directory);

            Console.WriteLine("Entering directory \"" + dirname + "\" [" + string.Join(", ", extensions) + "]:");
            var folderTester = new FolderTester(directory, directory, directory, extensions);
            folderTester.Test();
            nbOfTests += folderTester.GetTestCount();
            Console.Write("\n");
            Console.Write("Number of tests: " + nbOfTests + "\n");
            Assert.IsTrue(nbOfTests > 0, "No tests found");
        }
    }
}
