       IDENTIFICATION DIVISION.
       PROGRAM-ID. TCOMFL01.
       data division.
       working-storage section.
       01 var1 PIC X(120) VALUE "start1
      -    "end1".
       01 var2 PIC X(120) VALUE 'start2
      *comment
      -    'end2'.
       01 var3 PIC X(120) VALUE "start3
      
      -    "end3".
       01 var4 PIC X(120) VALUE 'start4
      -
      -    'end4'.
       01 var5 PIC X(120) VALUE "start5
      -    "extension
      -    "end5".
       procedure division.
           display var1
           display var2
           display var3
           display var4
           display var5
           goback
           .
       end program TCOMFL01.