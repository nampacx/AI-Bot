@echo off
echo Starting AI Foundry Console Agent Client...
echo.

cd /d "c:\Projects\Others\GH\nampacx\AI-Bot\src\ConsoleAgentClient"

echo Building the application...
dotnet build --configuration Release

if %ERRORLEVEL% NEQ 0 (
    echo Build failed. Please check the errors above.
    pause
    exit /b 1
)

echo.
echo Running the console agent client...
echo.

dotnet run --configuration Release

echo.
echo Application ended.
pause