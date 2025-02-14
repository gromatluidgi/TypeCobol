﻿using TypeCobol.Compiler.Directives;

namespace TypeCobol.Compiler.Scanner
{
    /// <summary>
    /// Base class for Cobol Scanner and SQL Scanner
    /// </summary>
    public abstract class AbstractScanner
    {
        protected readonly string line;
        protected int currentIndex;
        protected readonly int lastIndex;
        protected readonly TokensLine tokensLine;
        protected readonly TypeCobolOptions compilerOptions;

        protected AbstractScanner(string line, int startIndex, int lastIndex, TokensLine tokensLine, TypeCobolOptions compilerOptions)
        {
            this.line = line;
            this.currentIndex = startIndex;
            this.lastIndex = lastIndex;
            this.tokensLine = tokensLine;
            this.compilerOptions = compilerOptions;
        }

        /// <summary>
        /// Current position in the line being parsed.
        /// This is the index of the next character to be read by this scanner.
        /// </summary>
        public int CurrentIndex => currentIndex;

        /// <summary>
        /// Scan from current index and return the token found.
        /// </summary>
        /// <returns>Token instance, null if no token could be scanned.</returns>
        public Token GetNextToken() => GetTokenStartingFrom(currentIndex);

        /// <summary>
        /// Scan from given index and return the token found.
        /// </summary>
        /// <param name="startIndex">Index to start scanning from.</param>
        /// <returns>Token instance, null if no token could be scanned.</returns>
        public abstract Token GetTokenStartingFrom(int startIndex);
    }
}
