@echo off

set ZipTool="%ProgramFiles%/7-Zip/7z.exe"

if not exist %ZipTool% goto MISSING_ZIP

set version=1
findstr /c:"AssemblyFileVersion(" sources\Properties\AssemblyInfo.cs > version.txt
for /f tokens^=2^ delims^=^". %%G IN (version.txt) DO set version=%%G
del /q version.txt > nul
echo Version: [%version%]

pushd assets
%ZipTool% a -r -tZip -bb0 FFTriadBuddy.zip *.* 
popd
move /y assets\FFTriadBuddy.zip FFTriadBuddy.pkg > nul

mkdir releases > nul

copy /y FFTriadBuddy.pkg releases\FFTriadBuddy.pkg > nul
copy /y sources\bin\Release\FFTriadBuddy.exe releases\FFTriadBuddy.exe > nul
pushd releases
%ZipTool% a -r -tZip -bb0 release-v%version%.zip FFTriadBuddy.pkg FFTriadBuddy.exe
del /q FFTriadBuddy.* > nul
popd

:PACKED
echo Done! 
echo.
echo Saved latest Release binary and assets to release-v%version%.zip file.
goto FINISHED

:MISSING_ZIP
echo Can't find 7-Zip tool!
echo.
echo Expected path: %ZipTool%
goto FINISHED

:FINISHED
echo.
pause