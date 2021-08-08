@echo off
setlocal

if not exist SaintCoinach goto MISSING_SAINT
if not exist SetUserVars.bat goto MISSING_PATH
call SetUserVars.bat
if %GamePath%=="" goto MISSING_PATH 

echo Game path: %GamePath%
echo.
echo Export data using commands:
echo   ui 82100 82999
echo   allexd
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

:GIT_MIRRORS
SET /P CANDOWNLOAD=Do you want to download from github mirrors (curl required) (Y/[N])? 
IF /I "%CANDOWNLOAD%" NEQ "Y" GOTO EXPORTED

rem raw blobs with URL like:
rem https://raw.githubusercontent.com/thewakingsands/ffxiv-datamining-cn/master/ENpcResident.csv
call :CURL_WORKER cn thewakingsands/ffxiv-datamining-cn master
call :CURL_WORKER ko Ra-Workspace/ffxiv-datamining-ko master/csv
goto EXPORTED

:CURL_WORKER
echo Downloading from: %2...
for %%F in (ENpcResident Item PlaceName TripleTriadCard TripleTriadCardType TripleTriadRule TripleTriadCompetition TripleTriadCardResident TripleTriadResident) do ( 
	curl https://raw.githubusercontent.com/%2/%3/%%F.csv --output export\exd-all\%%F.%1.csv --silent
)
exit /b

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
echo    https://github.com/xivapi/SaintCoinach/releases
echo.
echo Expected path: datasource/SaintCoinach/SaintCoinach.Cmd.exe
goto FINISHED

:FINISHED
echo.
pause