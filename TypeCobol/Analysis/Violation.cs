﻿using TypeCobol.Compiler.Diagnostics;

namespace TypeCobol.Analysis
{
    /// <summary>
    /// Custom diagnostic to represent a Quality Rule Violation
    /// </summary>
    public class Violation : Diagnostic
    {
        private const int DIAGNOSTIC_CODE = 47; //Reserved code for violations (See MessageCode.cs for full enum).

        private static DiagnosticMessage ToDiagnosticMessage(Severity severity, string message, string ruleId)
        {
            //RuleId is stored in ReferenceText, it will then be transferred through LSP as 'source'.
            //Diagnostic code is fixed, it means that all diagnostics with code '47' are in fact quality rule violations created by an analyzer.
            return new DiagnosticMessage(Category.CodeAnalysis, DIAGNOSTIC_CODE, severity, message, null, 0, ruleId);
        }

        public string RuleId => Info.ReferenceText;

        public Violation(string ruleId, Severity severity, Position position, string message)
            : base(ToDiagnosticMessage(severity, message, ruleId), position)
        {

        }

        //Violation does not define any new data, so no need for Duplicate() overload and copy constructor.
    }
}
