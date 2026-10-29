@echo off
setlocal EnableExtensions
chcp 65001 >nul
cd /d "%~dp0"
title PS3Recomp - Build Windows x64 (BAT puro)
set "ROOT=%~dp0"
set "SDK=%ROOT%ps3recomp-master"
set "TOOLS=%SDK%\tools"
set "TEMPLATE=%SDK%\templates\project"
set "EBoot=%ROOT%input\EBOOT.ELF"
set "PORT=%ROOT%ports\EBOOT"
set "ANALYSIS=%PORT%\analysis"
set "RECOMP=%PORT%\recompiled"
set "BUILD=%PORT%\build"

:menu
cls
echo ============================================================
echo    PS3Recomp - Compilacao Windows x64 - SOMENTE BAT
echo ============================================================
echo.
echo  [1] Preparar e compilar jogo.exe a partir do EBOOT.ELF
echo  [2] Abrir solucao no Visual Studio 2022
echo  [3] Verificar ferramentas instaladas
echo  [4] Sair
echo.
choice /c 1234 /n /m "Escolha uma opcao [1-4]: "
if errorlevel 4 goto :eof
if errorlevel 3 goto checktools
if errorlevel 2 goto opensln
if errorlevel 1 goto build

:build
cls
echo [INFO] Pipeline sem PowerShell e sem arquivos PS1.
echo.
if not exist "%EBoot%" (
  echo [ERRO] Nao encontrei input\EBOOT.ELF
  echo Coloque o EBOOT.ELF descriptografado nessa pasta.
  pause
  goto menu
)
if not exist "%TOOLS%\ppu_loader.py" (
  echo [ERRO] Ferramentas do ps3recomp nao encontradas em ps3recomp-master\tools.
  pause
  goto menu
)
call :selectpython || goto pythonerror
call :require cmake || goto toolerror
call :require ninja || goto toolerror
call :require clang-cl || goto toolerror
if not exist "%ANALYSIS%" mkdir "%ANALYSIS%"
if not exist "%RECOMP%" mkdir "%RECOMP%"
if not exist "%PORT%\CMakeLists.txt" (
  echo [INFO] Copiando arquivos do template...
  xcopy "%TEMPLATE%\*" "%PORT%\" /E /I /Y >nul
  if errorlevel 1 (echo [ERRO] Falha ao copiar template.& pause & goto menu)
)
echo.
echo [1/4] Analisando EBOOT.ELF...
%PYTHON% "%TOOLS%\ppu_loader.py" "%EBoot%" --output "%ANALYSIS%"
if errorlevel 1 goto buildfailed
if not exist "%ANALYSIS%\EBOOT.functions.json" (echo [ERRO] O loader nao gerou EBOOT.functions.json.& goto buildfailed)
if not exist "%ANALYSIS%\EBOOT.imports.json" (echo [ERRO] O loader nao gerou EBOOT.imports.json.& goto buildfailed)
echo.
echo [2/4] Convertendo instrucoes PPU para C/C++...
%PYTHON% "%TOOLS%\ppu_lifter.py" "%EBoot%" --functions "%ANALYSIS%\EBOOT.functions.json" --hle-stubs "%ANALYSIS%\EBOOT.imports.json" --output "%RECOMP%"
if errorlevel 1 goto buildfailed
if not exist "%RECOMP%\ppu_recomp.h" (echo [ERRO] O lifter nao gerou ppu_recomp.h.& goto buildfailed)
echo.
echo [3/4] Gerando tabela de imports HLE...
%PYTHON% "%TOOLS%\gen_hle_nids.py" --all --out "%RECOMP%\ppu_hle_nids.cpp"
if errorlevel 1 goto buildfailed
echo.
echo [4/4] Configurando e compilando para Windows...
cmake -S "%PORT%" -B "%BUILD%" -G Ninja -DCMAKE_BUILD_TYPE=Release "-DPS3RECOMP_DIR=%SDK%" "-DRECOMP_DIR=%RECOMP%" -DCMAKE_C_COMPILER=clang-cl -DCMAKE_CXX_COMPILER=clang-cl
if errorlevel 1 goto buildfailed
cmake --build "%BUILD%"
if errorlevel 1 goto buildfailed
echo.
echo [OK] Compilacao concluida. Procure o executavel na pasta ports\EBOOT\build.
echo A compilacao nao garante compatibilidade completa do jogo.
pause
goto menu

:selectpython
set "PYTHON="
where py >nul 2>&1
if not errorlevel 1 (
  py -3 -c "import sys; raise SystemExit(0 if sys.version_info >= (3,10) else 1)" >nul 2>&1
  if not errorlevel 1 set "PYTHON=py -3"
)
if not defined PYTHON (
  where python >nul 2>&1
  if not errorlevel 1 (
    python -c "import sys; raise SystemExit(0 if sys.version_info >= (3,10) else 1)" >nul 2>&1
    if not errorlevel 1 set "PYTHON=python"
  )
)
if not defined PYTHON exit /b 1
echo [OK] Python 3.10+ selecionado: %PYTHON%
exit /b 0

:pythonerror
echo.
echo [ERRO] Python 3.10 ou superior nao encontrado.
echo Instale Python 3.11 x64 e marque a opcao de adicionar ao PATH, ou use o launcher py.
echo Download: https://www.python.org/downloads/windows/
pause
goto menu

:require
where %~1 >nul 2>&1
if errorlevel 1 (echo [AUSENTE] %~1 & exit /b 1) else (echo [OK] %~1 & exit /b 0)

:toolerror
echo.
echo [ERRO] Instale as ferramentas ausentes: Python 3, CMake, Ninja e LLVM/clang-cl.
echo Para clang-cl e Windows SDK, abra o BAT pelo Developer Command Prompt do VS 2022.
pause
goto menu

:buildfailed
echo.
echo [ERRO] Uma etapa falhou. Leia as mensagens acima; o jogo pode exigir ajustes de compatibilidade.
pause
goto menu

:opensln
if exist "%ROOT%PS3RecompWindows.sln" (start "" "%ROOT%PS3RecompWindows.sln") else (echo [ERRO] PS3RecompWindows.sln nao encontrado.& pause)
goto menu

:checktools
cls
echo Verificacao das ferramentas no PATH:
echo.
call :checkone py
call :checkone python
python --version 2>nul
echo Python precisa ser versao 3.10 ou superior.
call :checkone cmake
call :checkone ninja
call :checkone clang-cl
echo.
echo Nenhuma etapa deste BAT chama PowerShell ou arquivo PS1.
echo clang-cl e o Windows SDK podem exigir o Developer Command Prompt do VS 2022.
pause
goto menu

:checkone
where %~1 >nul 2>&1
if errorlevel 1 (echo [AUSENTE] %~1) else (echo [OK] %~1)
exit /b 0
