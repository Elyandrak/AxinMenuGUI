@echo off
:: AxinMenuGUI — build.bat
:: Patrón 1 AXIN: compilación explícita por .csproj, copia forzada del DLL, pause final.
:: Requiere variable de entorno VINTAGE_STORY apuntando a la instalación del juego.

echo.
echo ==========================================
echo  AxinMenuGUI — BUILD
echo ==========================================
echo.

:: Verificar que VINTAGE_STORY está configurado
if "%VINTAGE_STORY%"=="" (
    echo [ERROR] Variable de entorno VINTAGE_STORY no definida.
    echo Ejecuta: setx VINTAGE_STORY "C:\Users\TU_USUARIO\AppData\Roaming\Vintagestory1.21.6"
    echo Luego abre una CMD nueva y vuelve a ejecutar este script.
    pause
    exit /b 1
)

:: Verificar que el DLL de la API existe
if not exist "%VINTAGE_STORY%\VintagestoryAPI.dll" (
    echo [ERROR] No se encontro VintagestoryAPI.dll en: %VINTAGE_STORY%
    echo Verifica que VINTAGE_STORY apunta a la instalacion correcta del juego.
    pause
    exit /b 1
)

echo [OK] VINTAGE_STORY = %VINTAGE_STORY%
echo.

:: Compilar
dotnet build src\AxinMenuGUI\AxinMenuGUI.csproj -c Release
if errorlevel 1 (
    echo.
    echo [ERROR] Compilacion fallida. Revisa los errores anteriores.
    pause
    exit /b 1
)

echo.
echo [OK] Compilacion exitosa.
echo.

:: ── Carpeta de deploy ──────────────────────────────────────────────
set VS_DATA=%APPDATA%\VintagestoryData
set MOD_DEST=%VS_DATA%\Mods\AxinMenuGUI

echo [INFO] Destino del mod: %MOD_DEST%
if exist "%MOD_DEST%" (
    rmdir /S /Q "%MOD_DEST%"
)
mkdir "%MOD_DEST%"

:: Copiar DLL compilado — buscar en rutas posibles
set DLL_SRC=src\AxinMenuGUI\bin\Release\AxinMenuGUI.dll
if not exist "%DLL_SRC%" set DLL_SRC=src\AxinMenuGUI\bin\Release\net8.0\AxinMenuGUI.dll
if not exist "%DLL_SRC%" set DLL_SRC=src\AxinMenuGUI\bin\Release\net7.0\AxinMenuGUI.dll
if not exist "%DLL_SRC%" (
    echo [ERROR] No se encontro AxinMenuGUI.dll. Rutas buscadas:
    echo   src\AxinMenuGUI\bin\Release\AxinMenuGUI.dll
    echo   src\AxinMenuGUI\bin\Release\net8.0\AxinMenuGUI.dll
    echo   src\AxinMenuGUI\bin\Release\net7.0\AxinMenuGUI.dll
    pause
    exit /b 1
)
copy /Y "%DLL_SRC%" "%MOD_DEST%\AxinMenuGUI.dll"
echo [OK] DLL copiado desde: %DLL_SRC%

:: Copiar modinfo.json
copy /Y "modinfo.json" "%MOD_DEST%\modinfo.json"
echo [OK] modinfo.json copiado.

:: Copiar assets
if exist "assets" (
    xcopy /E /Y /I "assets" "%MOD_DEST%\assets"
    echo [OK] Assets copiados.
)

:: Leer versión desde modinfo.json
for /f "delims=" %%V in ('powershell -NoProfile -Command "(Get-Content modinfo.json | ConvertFrom-Json).version"') do set MOD_VERSION=%%V

set ZIP_NAME=AxinMenuGUI_v%MOD_VERSION%.zip
set ZIP_PATH=%CD%\%ZIP_NAME%

echo.
echo [INFO] Generando ZIP de distribucion: %ZIP_NAME%

if exist "%ZIP_PATH%" del /F /Q "%ZIP_PATH%"

:: ── ZIP COMPATIBLE CON LINUX / EXTERNAL ORIGINS ───────────────────
:: NO usar Compress-Archive para este caso.
:: Se crea el ZIP con .NET ZipArchive y rutas normalizadas con "/".
powershell -NoProfile -Command ^
  "$ErrorActionPreference = 'Stop';" ^
  "Add-Type -AssemblyName System.IO.Compression;" ^
  "Add-Type -AssemblyName System.IO.Compression.FileSystem;" ^
  "$root = [System.IO.Path]::GetFullPath('%MOD_DEST%');" ^
  "$zipPath = [System.IO.Path]::GetFullPath('%ZIP_PATH%');" ^
  "if (Test-Path $zipPath) { Remove-Item $zipPath -Force }" ^
  "$fs = [System.IO.File]::Open($zipPath, [System.IO.FileMode]::CreateNew);" ^
  "try {" ^
  "  $zip = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Create, $false);" ^
  "  try {" ^
  "    Get-ChildItem -Path $root -Recurse -File | ForEach-Object {" ^
  "      $full = $_.FullName;" ^
  "      $rel = $full.Substring($root.Length).TrimStart('\','/').Replace('\','/');" ^
  "      [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $full, $rel) | Out-Null;" ^
  "    }" ^
  "  } finally { $zip.Dispose() }" ^
  "} finally { $fs.Dispose() }"

if errorlevel 1 (
    echo [ERROR] Fallo al generar el ZIP compatible.
    pause
    exit /b 1
)

if exist "%ZIP_PATH%" (
    echo [OK] ZIP generado: %ZIP_PATH%
) else (
    echo [WARN] No se pudo generar el ZIP.
)

echo.
echo ==========================================
echo  DEPLOY COMPLETADO
echo  %MOD_DEST%
echo  ZIP: %ZIP_NAME%
echo ==========================================
echo.
pause
