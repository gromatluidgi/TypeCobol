﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TypeCobol.LanguageServer.Utilities
{
    public static class Extensions
    {
        public static TypeCobol.ExecutionStep? ExecutionStep(this LsrTestingOptions lsrOptions, TypeCobol.ExecutionStep? defaultValue)
        {
            switch (lsrOptions)
            {
                case LsrTestingOptions.NoLsrTesting:
                case LsrTestingOptions.LsrSourceDocumentTesting:
                    return defaultValue;
                case LsrTestingOptions.LsrScanningPhaseTesting:
                    return TypeCobol.ExecutionStep.Scanner;
                case LsrTestingOptions.LsrPreprocessingPhaseTesting:
                    return TypeCobol.ExecutionStep.Preprocessor;
                case LsrTestingOptions.LsrParsingPhaseTesting:
                    return TypeCobol.ExecutionStep.SyntaxCheck;
                case LsrTestingOptions.LsrSemanticPhaseTesting:
                    return TypeCobol.ExecutionStep.CrossCheck;
                case LsrTestingOptions.LsrCodeAnalysisPhaseTesting:
                    return TypeCobol.ExecutionStep.QualityCheck;
            }
            return defaultValue;
        }

        public static string ToLanguageServerOption(this LsrTestingOptions lsrOptions)
        {
            switch (lsrOptions)
            {
                case LsrTestingOptions.LsrSourceDocumentTesting:
                    return "-tsource";
                case LsrTestingOptions.LsrScanningPhaseTesting:
                    return "-tscanner";
                case LsrTestingOptions.LsrPreprocessingPhaseTesting:
                    return "-tpreprocess";
                case LsrTestingOptions.LsrParsingPhaseTesting:
                    return "-tparser";
                case LsrTestingOptions.LsrSemanticPhaseTesting:
                    return "-tsemantic";
                case LsrTestingOptions.LsrCodeAnalysisPhaseTesting:
                    return "-tcodeanalysis";
                case LsrTestingOptions.NoLsrTesting:
                default:
                    return "";
            }
        }
    }
}
