﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using JetBrains.Annotations;
using TypeCobol.Analysis;
using TypeCobol.Compiler;
using TypeCobol.Compiler.Directives;
using TypeCobol.Compiler.Parser;
using TypeCobol.Compiler.Text;
using TypeCobol.Compiler.Concurrency;
using TypeCobol.Compiler.Diagnostics;
using TypeCobol.Compiler.File;
using TypeCobol.Compiler.CodeModel;
using TypeCobol.CustomExceptions;
using TypeCobol.Tools.APIHelpers;

namespace TypeCobol
{
	public class Parser
	{
        public static readonly string Version;

        static Parser()
        {
            //Read current version from assembly attribute
            Version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
        }

        public IEnumerable<string> MissingCopys { get; private set; }
        protected Dictionary<string,bool> Inits;
        protected Dictionary<string,FileCompiler> Compilers;
        protected FileCompiler Compiler = null;
		/// <summary>Optional custom symbol table to use for name and type resolution.</summary>
		public SymbolTable CustomSymbols = null;

		public Parser() {
			Inits = new Dictionary<string,bool>();
			Compilers = new Dictionary<string,FileCompiler>();
		}

        public Parser(SymbolTable custmSymbols) :this()
        {
            CustomSymbols = custmSymbols;
        }

		private static DocumentFormat GetFormat(string filename) {
			return DocumentFormat.FreeUTF8Format;//TODO autodetect
		}

		public void Init([NotNull] string path, bool isCopy, TypeCobolOptions options, DocumentFormat format = null, IList<string> copies = null, IAnalyzerProvider analyzerProvider = null) {
			FileCompiler compiler;
			if (Compilers.TryGetValue(path, out compiler)) return;
			string filename = Path.GetFileName(path);
			var root = new DirectoryInfo(Directory.GetParent(path).FullName);
			if (format == null) format = GetFormat(path);
            
            CompilationProject project = new CompilationProject(path, root.FullName, Helpers.DEFAULT_EXTENSIONS,
				format, options, analyzerProvider);
			//Add copy folder into sourceFileProvider
			SourceFileProvider sourceFileProvider = project.SourceFileProvider;
			copies = copies ?? new List<string>();
			foreach (var folder in copies) {
				sourceFileProvider.AddLocalDirectoryLibrary(folder, false, Helpers.DEFAULT_COPY_EXTENSIONS, format.Encoding, format.EndOfLineDelimiter, format.FixedLineLength);
			}
			compiler = new FileCompiler(null, filename, format.ColumnsLayout, isCopy, project.SourceFileProvider, project, options, CustomSymbols, project);
            
			Compilers.Add(path, compiler);
			Inits.Add(path, false);
		}


		public void Parse(string path, TextChangedEvent e=null)
		{
            if (!Compilers.TryGetValue(path, out Compiler))
            {
                throw new InvalidOperationException($"Parser error: compiler for path '{path}' has not been initialized.");
            }

            Compiler.CompilationResultsForProgram.TextLinesChanged += OnTextLine;
			Compiler.CompilationResultsForProgram.CodeElementsLinesChanged += OnCodeElementLine;

            if (!Inits[path]) Inits[path] = true;// no need to update with the same content as at compiler creation
            else if (e != null) Compiler.CompilationResultsForProgram.UpdateTextLines(e);

            try { Compiler.CompileOnce(); }
			catch(Exception ex) {
                throw new ParsingException(MessageCode.SyntaxErrorInParser, ex.Message, path, ex, true, true);
			}

		    MissingCopys = Compiler.CompilationResultsForProgram.MissingCopies;

			Compiler.CompilationResultsForProgram.TextLinesChanged -= OnTextLine;
			Compiler.CompilationResultsForProgram.CodeElementsLinesChanged -= OnCodeElementLine;
		}

		private void OnCodeElementLine(object sender, DocumentChangedEvent<ICodeElementsLine> e)
		{
			System.Console.WriteLine("+++ OnCodeElementLine(..):");
			int c = 0;
			if (e.DocumentChanges != null)
			foreach(var change in e.DocumentChanges) {
				System.Console.WriteLine(" - "+change.Type+"@"+change.LineIndex);
				if (change.NewLine == null) System.Console.WriteLine("Line NIL");
				else
				if (change.NewLine.CodeElements == null) System.Console.WriteLine("CodeElements NIL");
				else {
					int i = 0;
					foreach(var ce in change.NewLine.CodeElements) {
						System.Console.WriteLine("   - "+ce);
						i++;
					}
					System.Console.WriteLine("   "+i+" CodeElements");
				}
				c++;
			}
			System.Console.WriteLine("+++ --> "+(c>0?(""+c):(e.DocumentChanges==null?"?":"0"))+" changes");
		}

		private void OnTextLine(object sender, DocumentChangedEvent<ICobolTextLine> e)
		{
			System.Console.WriteLine("--- OnTextLine(..):");
			int c = 0;
			if (e.DocumentChanges != null)
			foreach(var change in e.DocumentChanges) {
				System.Console.Write(" + "+change.Type+"@"+change.LineIndex+": ");
				if (change.NewLine == null) System.Console.WriteLine("?");
				else System.Console.WriteLine("\""+change.NewLine.SourceText+"\"");
				c++;
			}
			System.Console.WriteLine("--- --> "+(c>0?(""+c):(e.DocumentChanges==null?"?":"0"))+" changes");
		}


		public CompilationUnit Results {
			get { return Compiler.CompilationResultsForProgram; }
		}

        public static Parser Parse(string path, bool isCopy, TypeCobolOptions options, DocumentFormat format, IList<string> copies = null, IAnalyzerProvider analyzerProvider = null)
        {
            var parser = new Parser();
            options.ExecToStep = ExecutionStep.Generate;
            parser.Init(path, isCopy, options, format, copies, analyzerProvider);
            parser.Parse(path);
			return parser;
		}
	}
}
