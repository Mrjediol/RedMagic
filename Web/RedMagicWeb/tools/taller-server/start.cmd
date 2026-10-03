@echo off
rem Taller de RedMagic: la web + la cola de tareas de Claude. Deja esta ventana abierta mientras trabajas.
title Taller de RedMagic
cd /d "%~dp0"
start "" cmd /c "ping -n 3 127.0.0.1 >NUL & start http://localhost:3000/"
node server.mjs
pause
