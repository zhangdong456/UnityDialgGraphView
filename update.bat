@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo ====== 从 GitHub 更新(冲突时以远端为准) ======
git fetch origin
git merge -X theirs origin/master
echo.
echo ====== 更新完成 ======
pause
