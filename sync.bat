@echo off
setlocal EnableExtensions
title Sync project to GitHub

cd /d "%~dp0"
if errorlevel 1 goto :fail

echo ========================================
echo Sync project to GitHub
echo ========================================
echo.

set "GIT=git"
where git >nul 2>&1
if errorlevel 1 (
    if exist "%ProgramFiles%\Git\cmd\git.exe" (
        set "GIT=%ProgramFiles%\Git\cmd\git.exe"
    ) else if exist "%LocalAppData%\Programs\Git\cmd\git.exe" (
        set "GIT=%LocalAppData%\Programs\Git\cmd\git.exe"
    ) else (
        echo ERROR: Git was not found.
        goto :fail
    )
)

"%GIT%" rev-parse --show-toplevel >nul 2>&1
if errorlevel 1 (
    echo ERROR: This folder is not a Git repository.
    echo Folder: %CD%
    goto :fail
)

for /f "delims=" %%B in ('"%GIT%" branch --show-current') do set "BRANCH=%%B"
if not defined BRANCH (
    echo ERROR: Could not determine the current branch.
    goto :fail
)

echo Repository: %CD%
echo Branch: %BRANCH%
echo.
echo Staging changes...
"%GIT%" add -A
if errorlevel 1 goto :fail

"%GIT%" diff --cached --quiet
if errorlevel 1 (
    echo Committing changes...
    "%GIT%" commit -m "sync: %DATE% %TIME%"
    if errorlevel 1 goto :fail
) else (
    echo No local changes to commit.
)

echo Pushing to origin/%BRANCH%...
"%GIT%" push origin "%BRANCH%"
if errorlevel 1 goto :fail

echo.
echo ========================================
echo Sync completed successfully.
echo ========================================
echo.
pause
exit /b 0

:fail
echo.
echo ========================================
echo Sync failed. Read the error above.
echo ========================================
echo.
pause
exit /b 1
