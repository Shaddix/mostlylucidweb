@echo off
setlocal enabledelayedexpansion

REM Publish helper script for @mostlylucid/mermaid-enhancements (Windows)
REM Usage: scripts\publish.bat [version]
REM Example: scripts\publish.bat minor

REM Check if version is provided
if "%~1"=="" (
    echo [ERROR] Version is required
    echo.
    echo Usage: %~nx0 ^<version^>
    echo.
    echo Examples:
    echo   %~nx0 1.2.3           # Publish version 1.2.3
    echo   %~nx0 1.2.3-beta.1    # Publish pre-release version
    echo   %~nx0 patch           # Auto-increment patch version
    echo   %~nx0 minor           # Auto-increment minor version
    echo   %~nx0 major           # Auto-increment major version
    exit /b 1
)

set VERSION=%~1

REM If version is a bump type (patch/minor/major), calculate the new version
echo %VERSION% | findstr /r "^patch$ ^minor$ ^major$ ^prepatch$ ^preminor$ ^premajor$ ^prerelease$" >nul
if %errorlevel% equ 0 (
    echo [INFO] Calculating %VERSION% version bump...
    for /f "tokens=*" %%i in ('npm version %VERSION% --no-git-tag-version') do set NEW_VERSION=%%i
    set VERSION=!NEW_VERSION:v=!
    echo [SUCCESS] New version: !VERSION!
)

REM Check if working directory is clean
git status --porcelain > nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Git is not available
    exit /b 1
)

for /f %%i in ('git status --porcelain') do set HAS_CHANGES=1
if defined HAS_CHANGES (
    echo [WARNING] Working directory is not clean. You have uncommitted changes:
    git status --short
    echo.
    set /p CONTINUE="Do you want to continue? (y/n): "
    if /i not "!CONTINUE!"=="y" (
        echo [INFO] Aborted by user
        exit /b 1
    )
)

REM Show current branch
for /f "tokens=*" %%i in ('git branch --show-current') do set CURRENT_BRANCH=%%i
echo [INFO] Current branch: !CURRENT_BRANCH!

REM Ask for confirmation
echo.
echo This will:
echo   1. Update package.json to version %VERSION%
echo   2. Run tests
echo   3. Build the project
echo   4. Commit the version change
echo   5. Create and push tag ml-mermaidv%VERSION%
echo   6. Trigger GitHub Action to publish to npm
echo.
set /p CONFIRM="Continue? (y/n): "
if /i not "!CONFIRM!"=="y" (
    echo [INFO] Aborted by user
    exit /b 1
)

REM Update package.json
echo [INFO] Updating package.json to version %VERSION%...
call npm version %VERSION% --no-git-tag-version --allow-same-version
if %errorlevel% neq 0 (
    echo [ERROR] Failed to update package.json
    exit /b 1
)
echo [SUCCESS] Updated package.json

REM Run tests
echo [INFO] Running tests...
call npm test
if %errorlevel% neq 0 (
    echo [WARNING] Tests failed
    set /p CONTINUE_TESTS="Continue anyway? (y/n): "
    if /i not "!CONTINUE_TESTS!"=="y" (
        echo [INFO] Aborted due to test failures
        git checkout package.json package-lock.json 2>nul
        exit /b 1
    )
) else (
    echo [SUCCESS] Tests passed
)

REM Build
echo [INFO] Building project...
call npm run build
if %errorlevel% neq 0 (
    echo [ERROR] Build failed
    git checkout package.json package-lock.json 2>nul
    exit /b 1
)
echo [SUCCESS] Build successful

REM Commit version change
echo [INFO] Committing version change...
git add package.json package-lock.json
git commit -m "chore: bump version to %VERSION%"
if %errorlevel% neq 0 (
    echo [WARNING] No changes to commit (version might already be set)
)
echo [SUCCESS] Committed version change

REM Create and push tag
set TAG_NAME=ml-mermaidv%VERSION%
echo [INFO] Creating tag %TAG_NAME%...
git tag %TAG_NAME%
if %errorlevel% neq 0 (
    echo [ERROR] Failed to create tag
    exit /b 1
)

echo [INFO] Pushing commit and tag to origin...
git push origin %CURRENT_BRANCH%
git push origin %TAG_NAME%
if %errorlevel% neq 0 (
    echo [ERROR] Failed to push tag
    exit /b 1
)

echo [SUCCESS] Tag %TAG_NAME% pushed successfully!
echo.
echo [SUCCESS] Publishing workflow triggered!
echo.
echo Next steps:
echo   1. Monitor the GitHub Actions workflow:
echo      https://github.com/scottgal/mostlylucidweb/actions
echo.
echo   2. Once published, check npm:
echo      https://www.npmjs.com/package/@mostlylucid/mermaid-enhancements/v/%VERSION%
echo.
echo   3. Verify the GitHub Release:
echo      https://github.com/scottgal/mostlylucidweb/releases/tag/%TAG_NAME%
echo.

endlocal
