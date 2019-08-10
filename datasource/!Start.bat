@echo off

if not exist SaintCoinach goto MISSING_SAINT
if not exist SetUserVars.bat goto MISSING_PATH
call SetUserVars.bat
if %GamePath%=="" goto MISSING_PATH 

echo Game path: %GamePath%
echo.
echo Export data using commands:
echo   ui 82500 82999
echo   exd
echo   exit
echo.

pushd SaintCoinach
if exist SaintCoinach.History.zip ( del SaintCoinach.History.zip > nul )
SaintCoinach.Cmd.exe %GamePath%
popd

FOR /F "tokens=* USEBACKQ" %%F IN (`dir SaintCoinach\2* /A:D /B`) DO (
SET DataPath=%%F
)

echo.
rmdir export /s /q > nul
echo Copying exported data from: %DataPath%

xcopy SaintCoinach\%DataPath%\*.* export\ /e > nul

del ..\assets\icons\*.png
xcopy export\ui\icon\082000\*.png ..\assets\icons\ /s > nul

rmdir SaintCoinach\%DataPath% /s /q > nul

:EXPORTED
echo Done! 
echo.
echo Run DEBUG build with -dataConvert cmdline to process data tables.
echo Output logs will show all needed information
goto FINISHED

:MISSING_PATH
echo Game path not set, edit SetUserVars.bat and update GamePath variable
echo.
echo Example: "C:\Games\SquareEnix\FINAL FANTASY XIV - A Realm Reborn"
echo set GamePath="C:\Games\SquareEnix\FINAL FANTASY XIV - A Realm Reborn" > SetUserVars.bat
goto FINISHED

:MISSING_SAINT
echo Can't find SaintCoinach binaries!
echo.
echo Grab latest release of SaintCoinach.Cmd from github:
echo    https://github.com/ufx/SaintCoinach/releases
echo.
echo Expected path: datasource/SaintCoinach/SaintCoinach.Cmd.exe
goto FINISHED

:FINISHED
echo.
pause