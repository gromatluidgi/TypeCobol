﻿       IDENTIFICATION DIVISION.
       PROGRAM-ID. Pgm.
       DATA DIVISION.
       WORKING-STORAGE SECTION.
       01 WS-FOO PIC X.
       01 WS-BAR PIC X.
      *EXHIBIT, NAMED and CHANGED are not reserved keywords
       01 EXHIBIT PIC X.
       01 CHANGED PIC X.
       01 NAMED PIC X.
       PROCEDURE DIVISION.
      *EXHIBIT statement is not supported
           EXHIBIT NAMED WS-FOO WS-BAR
           .
       END PROGRAM Pgm.