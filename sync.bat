@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo ====== 同步到 GitHub ======
git add -A
git commit -m "sync: %date% %time%"
git push origin master
echo.
echo ====== 同步完成 ======
pause
