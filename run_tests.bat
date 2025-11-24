@echo off
REM ===============================
REM Run ExactAlgorythm and ApproxAlgorythm on all test files
REM ===============================

REM Folder z testami
set TESTDIR=RandomTestsGenerator/tests

REM Œcie¿ki do projektów
set EXACT=ExactAlgorythm
set APPROX=ApproxAlgorythm

echo Running all tests...
echo.

REM Pêtla po wszystkich plikach .txt w folderze testów
for %%F in (%TESTDIR%\*.txt) do (
    echo ===============================
    echo Test file: %%F
    echo ---- EXACT ----
    dotnet run --project %EXACT% -- %%F

    echo ---- APPROX ----

    dotnet run --project %APPROX% -- %%F


    echo.
)

echo ===============================
echo All tests finished.
pause
