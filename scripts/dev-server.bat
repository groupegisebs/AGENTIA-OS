@echo off
REM Demarre Agentia OS en local (Windows)
cd /d "%~dp0.."
set PYTHONPATH=.

where py >nul 2>&1 && set PY=py -3 || set PY=python

echo Demarrage sur http://127.0.0.1:8000/connexion
echo Arretez l'ancien serveur sur le port 8000 si la page affiche {"detail":"Not Found"}
echo.

%PY% -m pip install -q -r requirements.txt 2>nul
%PY% -m uvicorn agent_creator.main:app --reload --host 127.0.0.1 --port 8000
